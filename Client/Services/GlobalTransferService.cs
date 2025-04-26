using System.Runtime.CompilerServices;
using Client.Exceptions;
using Client.Helpers;
using Client.Interfaces;
using Domain.Enums;
using Domain.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared.Constants;

namespace Client.Services
{
	public class GlobalTransferService : IAsyncDisposable
	{
		private readonly IStorageService _storageService;

		private readonly IDeviceService _deviceService;

		private readonly ILogger<GlobalTransferService> _logger;

		private readonly IConfiguration _configuration;

		private HubConnection _connection;

		private string? _serverUrl;

		private CancellationTokenSource? SendRequestTokenSource { get; set; }
		private CancellationTokenSource? SendTokenSource { get; set; }
		private CancellationTokenSource? ReceiveTokenSource { get; set; }

		private GlobalRequestModel? SendRequest { get; set; }

		private List<FileModel> FilesToSend { get; set; } = [];

		public bool IsSending { get; private set; }

		public bool IsReceiving { get; private set; }

		public long SessionId { get; private set; }

		public long ReceiverId { get; private set; }

		public HubConnectionState ConnectionState => _connection.State;

		public event EventHandler? Disconnected;

		public event EventHandler<long>? Connected;

		public event EventHandler? SendingStarted;

		public event EventHandler<long>? SessionIdDoesNotExist;

		public event EventHandler<FileTransferModel>? ReceivingFileStarted;
		public event EventHandler<FileTransferModel>? ReceivingFileEnded;
		public event EventHandler<string>? ReceivingFileFailed;

		public event EventHandler<FileTransferModel>? SendingFileStarted;
		public event EventHandler<FileTransferModel>? SendingFileEnded;
		public event EventHandler<string>? SendingFileFailed;

		public event EventHandler? ReceivingStopped;
		public event EventHandler? ReceivingFinishedSuccessfully;

		public event EventHandler? SendingStopped;
		public event EventHandler? SendingFinishedSuccessfully;

		public event EventHandler? ReceivingCancelled;
		public event EventHandler? SendingCancelled;

		public event EventHandler? ReceiverDisconnected;
		public event EventHandler? SenderDisconnected;

		public Func<GlobalRequestModel, Task<bool>>? OnSendFilesRequest { get; set; }

		public GlobalTransferService(
			IStorageService storageService,
			IDeviceService deviceService,
			IConfiguration configuration,
			ILogger<GlobalTransferService> logger)
		{
			_deviceService = deviceService;
			_storageService = storageService;
			_logger = logger;
			_configuration = configuration;

			_connection = CreateHubConnection(GetConnectionUrl());
		}

		public async Task ConnectAsync(string? serverUrl = null)
		{
			if (_connection.State != HubConnectionState.Disconnected)
			{
				return;
			}

			_serverUrl = serverUrl;
			_connection = CreateHubConnection(GetConnectionUrl());

			try
			{
				await _connection.StartAsync();
			}
			catch (Exception)
			{
				Disconnected?.Invoke(this, EventArgs.Empty);
				throw;
			}
		}

		public async Task DisconnectAsync()
		{
			await _connection.StopAsync();
			await _connection.DisposeAsync();

			_serverUrl = null;
			SessionId = 0;
		}

		public async Task StartSendingAsync(long receiverSessionId, List<FileModel> files)
		{
			if (IsSending || _connection.State != HubConnectionState.Connected)
			{
				return;
			}

			ValidateSessionId(receiverSessionId);

			IsSending = true;
			ReceiverId = receiverSessionId;
			FilesToSend = files ?? [];

			await SendRequestAsync(receiverSessionId, FilesToSend);

			SendingStarted?.Invoke(this, EventArgs.Empty);
		}

		public void StopSending()
		{
			if (IsSending)
			{
				ResetSending();
				SendingStopped?.Invoke(this, EventArgs.Empty);
			}
		}

		public void StopReceiving()
		{
			if (IsReceiving)
			{
				ResetReceiving();
				ReceivingStopped?.Invoke(this, EventArgs.Empty);
			}
		}

		public void ResetReceiving()
		{
			IsReceiving = false;

			ReceiveTokenSource?.Cancel();
		}

		private async Task SendRequestAsync(long receiverSessionId, List<FileModel> files)
		{
			try
			{
				SendRequestTokenSource = new CancellationTokenSource();

				var filesMetadata = files
					.Select(f => new FileMetadata(Path.GetFileName(f.Path), f.Size))
					.ToList();

				var deviceModel = new GlobalDeviceModel(_deviceService.GetCurrentDeviceInfo());

				SendRequest = new GlobalRequestModel(SessionId, receiverSessionId, deviceModel, filesMetadata);
				await _connection.InvokeAsync(ServerConstants.FileHub.SendRequest, SendRequest, SendRequestTokenSource.Token);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error while sending the request");
				StopSending();
			}
			finally
			{
				SendRequestTokenSource?.Dispose();
				SendRequestTokenSource = null;
			}
		}

		public HubConnection CreateHubConnection(string url)
		{
			ArgumentNullException.ThrowIfNullOrWhiteSpace(url);

			var connection = new HubConnectionBuilder()
				.WithUrl(url)
				.Build();

			ListenConnection(connection);
			ListenRequests(connection);
			ListenFiles(connection);

			return connection;
		}

		private void ResetSending()
		{
			IsSending = false;
			ReceiverId = 0;
			SendRequest = null;
			FilesToSend.Clear();

			SendRequestTokenSource?.Cancel();
			SendTokenSource?.Cancel();
		}

		private Task OnConnectionClosed(Exception? ex)
		{
			SessionId = 0;
			_serverUrl = null;

			StopSending();

			if (ex is not null)
			{
				_logger.LogError(ex, "Connection to the server closed abnormally");
			}
			else
			{
				_logger.LogError("Connection to the server closed abnormally");
			}

			Disconnected?.Invoke(this, EventArgs.Empty);

			return Task.CompletedTask;
		}

		private async Task OnReceiveRequest(GlobalRequestModel request)
		{
			bool accepted = false;
			if (OnSendFilesRequest is not null)
			{
				accepted = await OnSendFilesRequest(request);
			}

			IsReceiving = accepted;

			var response = new GlobalResponseModel(SessionId, request.SenderSessionId, _deviceService.GetCurrentDeviceInfo().ToString(), accepted);
			await _connection.InvokeAsync(ServerConstants.FileHub.SendResponse, response);

			if (!accepted) return;

			ReceiveTokenSource = new CancellationTokenSource();

			try
			{
				await ReceiveFiles(request, ReceiveTokenSource.Token);

				ReceivingFinishedSuccessfully?.Invoke(this, EventArgs.Empty);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error while receiving the files");
			}
			finally
			{
				ReceiveTokenSource.Dispose();
				ReceiveTokenSource = null;

				StopReceiving();
			}
		}

		private async Task OnReceiveResponse(GlobalResponseModel response)
		{
			if (!IsSending)
			{
				await _connection.SendAsync(ServerConstants.FileHub.CancelSending);
				return;
			}

			if (response.IsAccepted && FilesToSend.Count == SendRequest?.Files.Count)
			{
				// don't block the flow so that we can receive messages from the server while streaming
				_ = Task.Run(async () =>
				{
					SendTokenSource = new CancellationTokenSource();

					try
					{
						await SendFilesAsync(FilesToSend, SendRequest.Files, response.ReceiverName, SendTokenSource.Token);
					}
					finally
					{
						SendTokenSource?.Dispose();
						SendTokenSource = null;

						StopSending();
					}
				});
			}
			else
			{
				StopSending();
			}
		}

		private async Task SendFilesAsync(List<FileModel> files, List<FileMetadata> filesMetadata, string receiverName, CancellationToken cancellationToken = default)
		{
			foreach (var fileData in files.Zip(filesMetadata, static (f, meta) => (File: f, meta.FileId)))
			{
				FileTransferModel file = new(fileData.File, TransferType.Global)
				{
					Status = TransferStatus.InProgress,
					Receiver = receiverName
				};

				try
				{
					SendingFileStarted?.Invoke(this, file);

					await SendFileAsync(file, fileData.FileId, cancellationToken);

					file.Status = TransferStatus.Finished;
					SendingFileEnded?.Invoke(this, file);

				}
				catch (Exception)
				{
					file.Status = TransferStatus.Failed;
					SendingFileFailed?.Invoke(this, fileData.File.Path);
					throw;
				}

			}

			SendingFinishedSuccessfully?.Invoke(this, EventArgs.Empty);
		}

		private async Task SendFileAsync(FileTransferModel file, Guid fileId, CancellationToken cancellationToken = default)
		{
			var tcs = new TaskCompletionSource();
			IAsyncEnumerable<byte[]> fileStream = GenerateFileStream(file, tcs.SetResult, cancellationToken);

			await _connection.SendAsync(ServerConstants.FileHub.SendFile, ReceiverId, fileStream, fileId);
			await tcs.Task;
		}

		private async Task ReceiveFiles(GlobalRequestModel request, CancellationToken cancellationToken = default)
		{
			for (int i = 0; i < request.Files.Count; i++)
			{
				string filePath = FileHelper.GetUniqueFilePath(request.Files[i].Name, _storageService.SaveFolder);
				FileTransferModel fileTransferModel = new(filePath, request.Files[i].Size, TransferType.Global)
				{
					Status = TransferStatus.InProgress,
					Sender = request.Sender.ToString()
				};

				try
				{
					ReceivingFileStarted?.Invoke(this, fileTransferModel);

					await ReceiveFile(fileTransferModel, request.Files[i].FileId, i == (request.Files.Count - 1), cancellationToken);

					ReceivingFileEnded?.Invoke(this, fileTransferModel);
				}
				catch (Exception)
				{
					fileTransferModel.Status = TransferStatus.Failed;
					HandleFailedFile(filePath);
					throw;
				}
			}
		}

		private async Task ReceiveFile(FileTransferModel file, Guid fileId, bool isLast, CancellationToken cancellationToken = default)
		{
			using FileStream fs = new(file.Path, FileMode.Create, FileAccess.Write);

			var fileStream = _connection.StreamAsync<byte[]>(ServerConstants.FileHub.ReceiveFile, fileId, isLast, cancellationToken);

			await foreach (var chunk in fileStream)
			{
				await fs.WriteAsync(chunk.AsMemory(0, chunk.Length));
				file.CurrentProgress += chunk.Length;
			}

			file.Status = file.CurrentProgress == file.Size
				? TransferStatus.Finished
				: throw new TransferException($"The {Path.GetFileName(file.Path)} file data was not received completely");
		}

		private static async IAsyncEnumerable<byte[]> GenerateFileStream(FileTransferModel file, Action? fileSentCallback, [EnumeratorCancellation] CancellationToken cancellationToken = default)
		{
			int bufferSize = FileHelper.GetBufferSizeByFileSize(file.Size);
			using FileStream fs = new(file.Path, FileMode.Open, FileAccess.Read);
			long size = fs.Length < bufferSize ? fs.Length : bufferSize;
			byte[] buffer = new byte[size];
			int bytesRead;
			while ((bytesRead = await fs.ReadAsync(buffer, CancellationToken.None)) > 0)
			{
				cancellationToken.ThrowIfCancellationRequested();

				if (bytesRead < buffer.Length)
				{
					Array.Resize(ref buffer, bytesRead);
				}
				yield return buffer;

				file.CurrentProgress += bytesRead;
			}

			if (fileSentCallback is not null && !cancellationToken.IsCancellationRequested)
			{
				fileSentCallback();
			}
		}

		private void HandleFailedFile(string filePath)
		{
			FileHelper.DeleteFileIfExists(filePath);
			ReceivingFileFailed?.Invoke(this, filePath);
		}

		private string GetConnectionUrl()
		{
			Uri baseUrl = new(_serverUrl ?? _configuration.GetValue<string>(Constants.Config.Server.BASE_URL_PATH) ?? "");
			string fileHubUrl = _configuration.GetValue<string>(Constants.Config.Server.FILE_HUB_PATH) ?? "";
			return new Uri(baseUrl, fileHubUrl).AbsoluteUri;
		}

		private static void ValidateSessionId(long receiverSessionId)
		{
			if (Math.Log10(receiverSessionId) is < 10 or > 11)
			{
				throw new ArgumentException("Session ID must have 11 significant digits.", nameof(receiverSessionId));
			}
		}

		private void ListenConnection(HubConnection connection)
		{
			connection.On<long>(ServerConstants.FileHub.HubConnected, OnConnected);

			connection.On(ServerConstants.FileHub.ReceiverDisconnected, OnReceiverDisconnected);
			connection.On(ServerConstants.FileHub.ReceiverDisconnected, StopSending);

			connection.On(ServerConstants.FileHub.SenderDisconnected, OnSenderDisconnected);
			connection.On(ServerConstants.FileHub.SenderDisconnected, StopReceiving);

			connection.Closed += OnConnectionClosed;
		}

		private void ListenRequests(HubConnection connection)
		{
			connection.On<GlobalRequestModel>(ServerConstants.FileHub.ReceiveRequest, OnReceiveRequest);

			connection.On<GlobalResponseModel>(ServerConstants.FileHub.ReceiveResponse, OnReceiveResponse);

			connection.On<long>(ServerConstants.FileHub.SessionIdDoesNotExist, OnSessionIdDoesNotExist);
		}

		private void ListenFiles(HubConnection connection)
		{
			//connection.On<FileMetadata, Guid, bool>(ServerConstants.FileHub.StartReceivingFile, OnStartReceivingFile);

			connection.On(ServerConstants.FileHub.ReceivingCancelled, OnReceivingCancelled);

			connection.On(ServerConstants.FileHub.SendingCancelled, OnSendingCancelled);

			connection.On(ServerConstants.FileHub.SendingAborted, StopReceiving);
			connection.On(ServerConstants.FileHub.ReceivingAborted, StopSending);
		}

		private void OnSessionIdDoesNotExist(long sessionId)
		{
			StopSending();
			SessionIdDoesNotExist?.Invoke(this, sessionId);
		}

		private void OnReceiverDisconnected()
		{
			ReceiverDisconnected?.Invoke(this, EventArgs.Empty);
		}

		private void OnSenderDisconnected()
		{
			SenderDisconnected?.Invoke(this, EventArgs.Empty);
		}

		private void OnReceivingCancelled()
		{
			if (IsSending)
			{
				StopSending();
				ReceivingCancelled?.Invoke(this, EventArgs.Empty);
			}
		}

		private void OnSendingCancelled()
		{
			SendingCancelled?.Invoke(this, EventArgs.Empty);
			StopReceiving();
		}

		private void OnConnected(long sessionId)
		{
			SessionId = sessionId;
			Connected?.Invoke(this, SessionId);
		}

		public async ValueTask DisposeAsync()
		{
			SendRequestTokenSource?.Dispose();
			SendTokenSource?.Dispose();
			ReceiveTokenSource?.Dispose();

			await _connection.StopAsync();
			await _connection.DisposeAsync();
		}
	}
}
