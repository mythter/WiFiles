using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Domain.Models;
using Microsoft.AspNetCore.SignalR;
using Server.Services;
using Shared.Constants;

namespace Server.Hubs
{
    public class FileHub(ConnectionManager connectionManager, SessionManager sessionManager, ILogger<FileHub> logger) : Hub
    {
        public async Task SendRequest(long whomSessionId, GlobalRequestModel request)
        {
            if (connectionManager.GetBySessionId(whomSessionId) is string whomConnectionId)
            {
                LogRequest(request, whomConnectionId, whomSessionId);

                await Clients.Client(whomConnectionId).SendAsync(ServerConstants.FileHub.ReceiveRequest, request);
            }
        }

        public async Task SendResponse(long whomSessionId, bool accepted)
        {
            if (connectionManager.GetBySessionId(whomSessionId) is string whomConnectionId)
            {
                LogResponse(accepted, whomConnectionId, whomSessionId);

                if (accepted)
                {
                    await sessionManager.TryAddAsync(whomConnectionId, Context.ConnectionId);
                    logger.LogInformation(
                        "SESSION CREATED: sender: {SenderConnectionId}, receiver {ReceiverConnectionId}",
                        whomConnectionId, Context.ConnectionId);
                }

                await Clients.Client(whomConnectionId).SendAsync(ServerConstants.FileHub.ReceiveResponse, accepted);
            }
        }
        public async Task StartSendingFile(long whomSessionId, FileMetadata file, Guid fileId, bool isLast)
        {
            if (connectionManager.GetBySessionId(whomSessionId) is string whomConnectionId)
            {
                var session = await sessionManager.GetBySenderAndReceiverConnectionIdAsync(Context.ConnectionId, whomConnectionId);

                if (session is not null)
                {
                    //session.FileChannels.TryAdd(fileId, Channel.CreateBounded<byte[]>(20));
                    session.FileChannels.TryAdd(fileId, Channel.CreateUnbounded<byte[]>());
                    await Clients.Client(whomConnectionId).SendAsync(ServerConstants.FileHub.StartReceivingFile, file, fileId, isLast);
                }
            }
        }

        public async Task CancelSending(long receiverSessionId)
        {
            if (connectionManager.GetBySessionId(receiverSessionId) is string receiverConnectionId)
            {
                await Clients.Client(receiverConnectionId).SendAsync(ServerConstants.FileHub.CancelReceiving);

                LogSendingStopped(receiverConnectionId, receiverSessionId);

                await sessionManager.RemoveBySenderAsync(Context.ConnectionId);

                logger.LogInformation(
                    "SESSION DELETED: sender: {SenderConnectionId}, receiver {ReceiverConnectionId}",
                    Context.ConnectionId, receiverConnectionId);
            }
        }

        public async Task SendFile(long whomSessionId, IAsyncEnumerable<byte[]> stream, Guid fileId, bool isLast)
        {
            if (connectionManager.GetBySessionId(whomSessionId) is string whomConnectionId)
            {
                var session = await sessionManager.GetBySenderAndReceiverConnectionIdAsync(Context.ConnectionId, whomConnectionId);

                if (session is not null && session.FileChannels.TryGetValue(fileId, out var channel))
                {
                    Exception? exception = null;
                    try
                    {
                        await foreach (var chunk in stream)
                        {
                            await channel.Writer.WriteAsync(chunk);
                        }

                        if (isLast)
                        {
                            await sessionManager.RemoveBySenderAsync(Context.ConnectionId);
                        }
                    }
                    catch (HubException ex) when (ex.Message == "Stream canceled by client.")
                    {
                        logger.LogError(ex, "Sending cancelled");
                        await Clients.Client(session.ReceiverConnectionId).SendAsync(ServerConstants.FileHub.SendingCancelled);
                        exception = ex;
                    }
                    catch (OperationCanceledException ex)
                    {
                        logger.LogError(ex, "Sender disconnected");
                        exception = ex;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error while file sending");
                        exception = ex;
                    }
                    finally
                    {
                        var res = channel.Writer.TryComplete(exception);
                        logger.LogInformation("SendFile Channel writer completed: {IsChannelWriterCompleted}", res);

                        // close seesion when exception occurred or last file was transferred
                        if (exception is not null || isLast)
                        {
                            await sessionManager.RemoveBySenderAsync(Context.ConnectionId);
                        }
                    }
                }
            }
        }

        public async IAsyncEnumerable<byte[]> ReceiveFile(Guid fileId, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var session = await sessionManager.GetByReceiverConnectionIdAsync(Context.ConnectionId);

            if (session is not null && session.FileChannels.TryGetValue(fileId, out var channel))
            {
                byte[]? result = null;
                bool hasResult = true;
                Exception? exception = null;
                while (hasResult)
                {
                    try
                    {
                        await channel.Reader.WaitToReadAsync(cancellationToken);
                        hasResult = channel.Reader.TryRead(out result);
                    }
                    catch (OperationCanceledException ex) when (Context.ConnectionAborted.IsCancellationRequested)
                    {
                        logger.LogError(ex, "Receiver disconnected");
                        exception = ex;
                        break;
                    }
                    catch (OperationCanceledException ex)
                    {
                        logger.LogError(ex, "Receiving cancelled");
                        await Clients.Client(session.SenderConnectionId).SendAsync(ServerConstants.FileHub.ReceivingCancelled);
                        exception = ex;
                        break;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error while file receiving");
                        exception = ex;
                        break;
                    }
                    finally
                    {
                        if (exception is not null)
                        {
                            // we need to stop writing when receiver stopped reading or exception occurred
                            channel.Writer.TryComplete(exception);
                            // close session if exception occurred
                            await sessionManager.RemoveByReceiverAsync(Context.ConnectionId);
                        }
                    }

                    if (hasResult && result is not null)
                    {
                        yield return result;
                    }
                }
            }
        }

        public async Task CancelReceiving(long senderSessionId)
        {
            if (connectionManager.GetBySessionId(senderSessionId) is string senderConnectionId)
            {
                await Clients.Client(senderConnectionId).SendAsync(ServerConstants.FileHub.CancelSending);

                LogReceivingStopped(senderConnectionId, senderSessionId);

                await sessionManager.RemoveByReceiverAsync(Context.ConnectionId);

                logger.LogInformation(
                    "SESSION DELETED: sender: {SenderConnectionId}, receiver {ReceiverConnectionId}",
                    Context.ConnectionId, senderConnectionId);
            }
        }

        public override async Task OnConnectedAsync()
        {
            long sessionId = await connectionManager.AddConnectionAsync(Context.ConnectionId);

            logger.LogInformation("User CONNECTED with connectionId: {ConnectionId}, sessionId: {SessionId}", Context.ConnectionId, sessionId);

            await Clients.Caller.SendAsync(ServerConstants.FileHub.HubConnected, sessionId);

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            string connectionId = Context.ConnectionId;
            long? sessionId = connectionManager.GetByConnectionId(connectionId);

            logger.LogInformation("User DISCONNECTED with connectionId: {ConnectionId}, sessionId: {SessionId}", connectionId, sessionId);

            await sessionManager.LockAsync();
            try
            {
                foreach (var session in sessionManager.Sessions.Where(s => s.SenderConnectionId == connectionId || s.ReceiverConnectionId == connectionId))
                {
                    if (session.SenderConnectionId == connectionId)
                    {
                        await Clients.Client(session.ReceiverConnectionId).SendAsync(ServerConstants.FileHub.SenderDisconnected);
                    }
                    else
                    {
                        await Clients.Client(session.SenderConnectionId).SendAsync(ServerConstants.FileHub.ReceiverDisconnected);
                    }
                }
            }
            finally
            {
                sessionManager.Release();

                connectionManager.RemoveByConnectionId(connectionId);
                await sessionManager.RemoveByConnectionIdAsync(connectionId);
            }

            await base.OnDisconnectedAsync(exception);
        }

        private void LogRequest(GlobalRequestModel request, string whomConnectionId, long whomSessionId)
        {
            logger.LogInformation(
                "REQUEST: {Request} file(s)\n" +
                "From\tconnectionId: {SenderConnectionId}, sessionId: {SensderSessionId}\n" +
                "To:\tconnectionId: {ReceiverConnectionId}, sessionId: {ReceiverSessionId}",
                request.Files.Count,
                Context.ConnectionId, connectionManager.GetByConnectionId(Context.ConnectionId),
                whomConnectionId, whomSessionId);
        }

        private void LogResponse(bool accepted, string whomConnectionId, long whomSessionId)
        {
            logger.LogInformation(
                "RESPONSE: {Response}\n" +
                "From\tconnectionId: {SenderConnectionId}, sessionId: {SensderSessionId}\n" +
                "To:\tconnectionId: {ReceiverConnectionId}, sessionId: {ReceiverSessionId}",
                accepted,
                Context.ConnectionId, connectionManager.GetByConnectionId(Context.ConnectionId),
                whomConnectionId, whomSessionId);
        }

        private void LogSendingStopped(string receiverConnectionId, long receiverSessionId)
        {
            logger.LogInformation(
                "SENDING STOPPED\n" +
                "From\tconnectionId: {SenderConnectionId}, sessionId: {SensderSessionId}\n" +
                "To:\tconnectionId: {ReceiverConnectionId}, sessionId: {ReceiverSessionId}",
                Context.ConnectionId, connectionManager.GetByConnectionId(Context.ConnectionId),
                receiverConnectionId, receiverSessionId);
        }

        private void LogReceivingStopped(string senderConnectionId, long senderSessionId)
        {
            logger.LogInformation(
                "RECEIVING STOPPED\n" +
                "From\tconnectionId: {SenderConnectionId}, sessionId: {SensderSessionId}\n" +
                "To:\tconnectionId: {ReceiverConnectionId}, sessionId: {ReceiverSessionId}",
                senderConnectionId, senderSessionId,
                Context.ConnectionId, connectionManager.GetByConnectionId(Context.ConnectionId));
        }
    }
}
