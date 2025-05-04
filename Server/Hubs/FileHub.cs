using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Domain.Models.Request;
using Domain.Models.Response;
using Microsoft.AspNetCore.SignalR;
using Server.Services;
using Shared.Constants;

namespace Server.Hubs
{
	public class FileHub(ConnectionManager connectionManager, SessionManager sessionManager, ILogger<FileHub> logger) : Hub
	{
		public async Task SendRequest(GlobalRequestModel request)
		{
			if (connectionManager.GetBySessionId(request.ReceiverSessionId) is string receiverConnectionId)
			{
				LogRequest(request, receiverConnectionId, request.ReceiverSessionId);

				await Clients.Client(receiverConnectionId).SendAsync(ServerConstants.FileHub.ReceiveRequest, request);
			}
			else
			{
				await Clients.Caller.SendAsync(ServerConstants.FileHub.SessionIdDoesNotExist, request.ReceiverSessionId);
			}
		}

		public async Task SendResponse(GlobalResponseModel response)
		{
			if (connectionManager.GetBySessionId(response.SenderSessionId) is string senderConnectionId)
			{
				LogResponse(response.IsAccepted, senderConnectionId, response.SenderSessionId);

				if (response.IsAccepted)
				{
					await sessionManager.TryAddAsync(senderConnectionId, Context.ConnectionId);
					logger.LogInformation(
						"SESSION CREATED: sender: {SenderConnectionId}, receiver {ReceiverConnectionId}",
						senderConnectionId, Context.ConnectionId);
				}

				await Clients.Client(senderConnectionId).SendAsync(ServerConstants.FileHub.ReceiveResponse, response);
			}
		}

		public async Task CancelSending()
		{
			logger.LogInformation("CANCEL SENDING from {ConnectionId}", Context.ConnectionId);

			if (await sessionManager.GetBySenderConnectionId(Context.ConnectionId) is { } session)
			{
				await session.CancellationTokenSource.CancelAsync();
				await Clients.Client(session.ReceiverConnectionId).SendAsync(ServerConstants.FileHub.SendingCancelled);
			}

			await sessionManager.RemoveBySenderAsync(Context.ConnectionId);

			logger.LogInformation(
				"SESSION DELETED: sender: {SenderConnectionId}, receiver {ReceiverConnectionId}",
				Context.ConnectionId, "");
		}

		public async Task SendFile(long whomSessionId, IAsyncEnumerable<byte[]> stream, Guid fileId)
		{
			logger.LogInformation("Send file to {WhomSessionId}", whomSessionId);

			if (connectionManager.GetBySessionId(whomSessionId) is string whomConnectionId)
			{
				var session = await sessionManager.GetBySenderAndReceiverConnectionIdAsync(Context.ConnectionId, whomConnectionId);

				//var channel = Channel.CreateBounded<byte[]>(20);
				var channel = Channel.CreateUnbounded<byte[]>();

				if (session is not null && !session.FileChannels.TryAdd(fileId, channel))
				{
					session.FileChannels.TryGetValue(fileId, out channel);
				}

				if (session is not null && channel is not null)
				{
					Exception? exception = null;
					bool senderCancelled = false;
					try
					{
						await foreach (var chunk in stream.WithCancellation(session.CancellationTokenSource.Token))
						{
							await channel.Writer.WriteAsync(chunk, session.CancellationTokenSource.Token);
						}
					}
					catch (HubException ex) when (ex.Message == "Stream canceled by client.")
					{
						logger.LogError(ex, "Sending cancelled");
						await Clients.Client(session.ReceiverConnectionId).SendAsync(ServerConstants.FileHub.SendingCancelled);
						senderCancelled = true;
						exception = ex;
					}
					catch (OperationCanceledException ex)
					{
						logger.LogError(ex, "Sending operation cancelled");
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

						if (exception is not null && !session.CancellationTokenSource.Token.IsCancellationRequested)
						{
							await session.CancellationTokenSource.CancelAsync();
						}

						// close session when exception occurred or the transfer was cancelled
						if (exception is not null || session.CancellationTokenSource.Token.IsCancellationRequested)
						{
							await sessionManager.RemoveBySenderAsync(Context.ConnectionId);

							if (!senderCancelled)
							{
								logger.LogInformation("Sending Aborted");
								await Clients.Client(session.SenderConnectionId).SendAsync(ServerConstants.FileHub.SendingAborted, 
									exception is null 
										? null 
										: new { exception.Message, Type = exception.GetType().Name });
							}
						}
					}
				}
			}
		}

		public async IAsyncEnumerable<byte[]> ReceiveFile(Guid fileId, bool isLast, [EnumeratorCancellation] CancellationToken cancellationToken)
		{
			var session = await sessionManager.GetByReceiverConnectionIdAsync(Context.ConnectionId);

			logger.LogInformation("Receive file for {ReceiverConnectionId}", session?.ReceiverConnectionId);

			Channel<byte[]>? channel = null;

			if (session is not null && !session.FileChannels.TryGetValue(fileId, out channel))
			{
				//var channel = Channel.CreateBounded<byte[]>(20);
				channel = Channel.CreateUnbounded<byte[]>();

				if (!session.FileChannels.TryAdd(fileId, channel))
				{
					session.FileChannels.TryGetValue(fileId, out channel);
				}
			}

			if (session is not null && channel is not null)
			{
				using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, session.CancellationTokenSource.Token);
				byte[] result;
				bool hasResult = true;
				Exception? exception = null;
				bool receiverCancelled = false;
				while (hasResult)
				{
					try
					{
						await channel.Reader.WaitToReadAsync(linkedTokenSource.Token);
						hasResult = channel.Reader.TryRead(out result!);
					}
					catch (OperationCanceledException ex) when (Context.ConnectionAborted.IsCancellationRequested)
					{
						logger.LogError(ex, "Receiver disconnected");
						exception = ex;
						break;
					}
					catch (OperationCanceledException ex) when (!session.CancellationTokenSource.IsCancellationRequested)
					{
						logger.LogError(ex, "Receiving cancelled");
						await Clients.Client(session.SenderConnectionId).SendAsync(ServerConstants.FileHub.ReceivingCancelled);
						receiverCancelled = true;
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
						if (exception is not null || (isLast && !hasResult))
						{
							// stop writing when receiver stopped reading or exception occurred
							if (exception is not null)
							{
								await session.CancellationTokenSource.CancelAsync();

								if (!receiverCancelled)
								{
									logger.LogInformation("Receiving Aborted");
									await Clients.Client(session.ReceiverConnectionId).SendAsync(ServerConstants.FileHub.ReceivingAborted, new
									{
										exception.Message,
										Type = exception.GetType().Name
									});
								}
							}

							// close session if exception occurred or the last file was transferred
							await sessionManager.RemoveByReceiverAsync(Context.ConnectionId);
						}
					}

					if (hasResult)
					{
						yield return result;
					}
				}
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

			logger.LogError(exception, "User DISCONNECTED with connectionId: {ConnectionId}, sessionId: {SessionId}", connectionId, sessionId);

			await sessionManager.LockAsync();
			try
			{
				foreach (var session in sessionManager.Sessions.Where(s => s.SenderConnectionId == connectionId || s.ReceiverConnectionId == connectionId))
				{
					if (session.SenderConnectionId == connectionId)
					{
						await Clients.Client(session.ReceiverConnectionId).SendAsync(ServerConstants.FileHub.SenderDisconnected, exception);
					}
					else
					{
						await Clients.Client(session.SenderConnectionId).SendAsync(ServerConstants.FileHub.ReceiverDisconnected, exception);
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
