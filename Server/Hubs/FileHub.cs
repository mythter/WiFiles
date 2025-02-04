using System.Threading.Channels;
using Domain.Models;
using Microsoft.AspNetCore.SignalR;
using Server.Services;
using Shared.Constants;

namespace Server.Hubs
{
    public class FileHub(ConnectionManager connectionManager, SessionManager sessionManager) : Hub
    {
        public async Task SendRequest(long whomSessionId, GlobalRequestModel request)
        {
            if (connectionManager.GetBySessionId(whomSessionId) is string whomConnectionId)
            {
                await Clients.Client(whomConnectionId).SendAsync(ServerConstants.FileHub.ReceiveRequest, request);
            }
        }

        public async Task SendResponse(long whomSessionId, bool accepted)
        {
            if (connectionManager.GetBySessionId(whomSessionId) is string whomConnectionId)
            {
                if (accepted)
                {
                    await sessionManager.TryAddAsync(whomConnectionId, Context.ConnectionId);
                }

                await Clients.Client(whomConnectionId).SendAsync(ServerConstants.FileHub.ReceiveResponse, accepted);
            }
        }
        public async Task StartSendingFile(long whomSessionId, FileMetadata file, Guid fileId)
        {
            if (connectionManager.GetBySessionId(whomSessionId) is string whomConnectionId)
            {
                await sessionManager.LockAsync();
                var session = sessionManager.Sessions
                    .SingleOrDefault(s => s.SenderConnectionId == Context.ConnectionId && s.ReceiverConnectionId == whomConnectionId);
                sessionManager.Release();

                if (session is not null)
                {
                    //session.FileChannels.TryAdd(fileId, Channel.CreateBounded<byte[]>(20));
                    session.FileChannels.TryAdd(fileId, Channel.CreateUnbounded<byte[]>());
                    await Clients.Client(whomConnectionId).SendAsync(ServerConstants.FileHub.StartReceivingFile, file, fileId);
                }
            }
        }

        public async Task SendFile(long whomSessionId, IAsyncEnumerable<byte[]> stream, Guid fileId)
        {
            if (connectionManager.GetBySessionId(whomSessionId) is string whomConnectionId)
            {
                await sessionManager.LockAsync();
                var session = sessionManager.Sessions
                    .SingleOrDefault(s => s.SenderConnectionId == Context.ConnectionId && s.ReceiverConnectionId == whomConnectionId);
                sessionManager.Release();

                if (session is not null && session.FileChannels.TryGetValue(fileId, out var channel))
                {
                    try
                    {
                        await foreach (var chunk in stream)
                        {
                            await channel.Writer.WriteAsync(chunk);
                        }
                    }
                    finally
                    {
                        channel.Writer.Complete();
                    }
                }
            }
        }

        public async IAsyncEnumerable<byte[]> ReceiveFile(Guid fileId)
        {
            await sessionManager.LockAsync();
            var session = sessionManager.Sessions
                .SingleOrDefault(s => s.ReceiverConnectionId == Context.ConnectionId);
            sessionManager.Release();

            if (session is not null && session.FileChannels.TryGetValue(fileId, out var channel))
            {
                await foreach (var chunk in channel.Reader.ReadAllAsync())
                {
                    yield return chunk;
                }
            }
        }

        public override async Task OnConnectedAsync()
        {
            long sessionId = await connectionManager.AddConnectionAsync(Context.ConnectionId);

            await Clients.Caller.SendAsync(ServerConstants.FileHub.HubConnected, sessionId);

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            string connectionId = Context.ConnectionId;
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
            }

            connectionManager.RemoveByConnectionId(connectionId);
            await sessionManager.RemoveByConnectionIdAsync(connectionId);

            await base.OnDisconnectedAsync(exception);
        }
    }
}
