using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Client.Exceptions;
using Client.Helpers;
using Client.Interfaces;
using Domain.Enums;
using Domain.Models.Device;
using Domain.Models.Encryptions;
using Domain.Models.File;
using Domain.Models.Request;
using Domain.Models.Response;
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

		private HubConnection _connection = null!;

		private string? _serverUrl;

		private CancellationTokenSource? SendRequestTokenSource { get; set; }
		private CancellationTokenSource? SendTokenSource { get; set; }
		private CancellationTokenSource? ReceiveTokenSource { get; set; }

		private GlobalRequestModel? SendRequest { get; set; }

		private List<FileModel> FilesToSend { get; set; } = [];

		private ECDiffieHellman? Ecdh { get; set; }

		public bool IsSending { get; private set; }

		public bool IsReceiving { get; private set; }

		public long SessionId { get; private set; }

		public long ReceiverId { get; private set; }

		public HubConnectionState ConnectionState => _connection?.State ?? HubConnectionState.Disconnected;

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
		}

		public async Task ConnectAsync(string? serverUrl = null)
		{
			if (ConnectionState != HubConnectionState.Disconnected)
			{
				return;
			}

			_serverUrl = serverUrl ?? _configuration.GetValue<string>(Constants.Config.Server.BASE_URL_PATH);

			try
			{
				_connection = CreateHubConnection(GetConnectionUrl(_serverUrl));

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
			if (_connection is not null)
			{
				await _connection.StopAsync();
				await _connection.DisposeAsync();
			}

			_serverUrl = null;
			SessionId = 0;
		}

		public async Task StartSendingAsync(long receiverSessionId, List<FileModel> files, bool useEncryption)
		{
			if (IsSending || ConnectionState != HubConnectionState.Connected)
			{
				return;
			}

			ValidateSessionId(receiverSessionId);

			IsSending = true;
			ReceiverId = receiverSessionId;
			FilesToSend = files ?? [];

			Ecdh = null;
			Encryption? encryption = null;

			if (useEncryption)
			{
				Ecdh = ECDiffieHellman.Create();
				encryption = new(Ecdh.PublicKey.ExportSubjectPublicKeyInfo());
			}

			await SendRequestAsync(receiverSessionId, FilesToSend, encryption);

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

		private void ResetReceiving()
		{
			IsReceiving = false;

			ReceiveTokenSource?.Cancel();
		}

		private async Task SendRequestAsync(long receiverSessionId, List<FileModel> files, Encryption? encryption = null)
		{
			try
			{
				SendRequestTokenSource = new CancellationTokenSource();

				var filesMetadata = files
					.Select(f => new FileMetadata(Path.GetFileName(f.Path), f.Size))
					.ToList();

				var deviceModel = new GlobalDeviceModel(_deviceService.GetCurrentDeviceInfo());

				SendRequest = new GlobalRequestModel(SessionId, receiverSessionId, deviceModel, filesMetadata)
				{
					Encryption = encryption
				};
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

			Ecdh?.Dispose();
			Ecdh = null;

			SendRequestTokenSource?.Cancel();
			SendTokenSource?.Cancel();
		}

		private Task OnConnectionClosed(Exception? ex)
		{
			SessionId = 0;
			_serverUrl = null;

			StopSending();
			StopReceiving();

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
			var deviceModel = new GlobalDeviceModel(_deviceService.GetCurrentDeviceInfo());

			Encryption? encryption = null;
			Aes? aes = null;

			if (accepted && request.Encryption?.PublicKey is { } senderKey)
			{
				var receiverEcdh = ECDiffieHellman.Create();

				var senderEcdh = ECDiffieHellman.Create();
				senderEcdh.ImportSubjectPublicKeyInfo(senderKey, out _);

				byte[] aesKey = receiverEcdh.DeriveKeyMaterial(senderEcdh.PublicKey);
				aes = Aes.Create();
				aes.Padding = PaddingMode.None;
				aes.Key = aesKey;
				aes.GenerateIV();

				encryption = new(receiverEcdh.ExportSubjectPublicKeyInfo(), aes.IV);
			}

			var response = new GlobalResponseModel(accepted, deviceModel, SessionId, request.SenderSessionId)
			{
				Encryption = encryption
			};
			await _connection.InvokeAsync(ServerConstants.FileHub.SendResponse, response);

			if (!accepted) return;

			ReceiveTokenSource = new CancellationTokenSource();

			try
			{
				await ReceiveFiles(request, aes, ReceiveTokenSource.Token);

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

				aes?.Dispose();

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
				Aes? aes = null;

				if (Ecdh is not null && (response?.Encryption?.PublicKey is null || response?.Encryption?.IV is null))
				{
					throw new CryptographicException("Not all encryption parameters are provided by receiver.");
				}

				if (Ecdh is not null)
				{
					var receiverEcdh = ECDiffieHellman.Create();
					receiverEcdh.ImportSubjectPublicKeyInfo(response.Encryption!.PublicKey, out _);

					byte[] aesKey = Ecdh.DeriveKeyMaterial(receiverEcdh.PublicKey);

					aes = Aes.Create();
					aes.Padding = PaddingMode.None;
					aes.Key = aesKey;
					aes.IV = response.Encryption.IV!;
				}

				// don't block the flow so that we can receive messages from the server while streaming
				_ = Task.Run(async () =>
				{
					SendTokenSource = new CancellationTokenSource();

					try
					{
						await SendFilesAsync(FilesToSend, SendRequest.Files, response.Receiver, aes, SendTokenSource.Token);
					}
					finally
					{
						SendTokenSource?.Dispose();
						SendTokenSource = null;

						aes?.Dispose();

						StopSending();
					}
				});
			}
			else
			{
				StopSending();
			}
		}

		private async Task SendFilesAsync(List<FileModel> files, List<FileMetadata> filesMetadata, DeviceModel receiver, Aes? aes = null, CancellationToken cancellationToken = default)
		{
			foreach (var fileData in files.Zip(filesMetadata, static (f, meta) => (File: f, meta.FileId)))
			{
				using var encryptor = aes?.CreateEncryptor();

				FileTransferModel file = new(fileData.File, TransferType.Global)
				{
					Status = TransferStatus.InProgress,
					Receiver = receiver
				};

				try
				{
					SendingFileStarted?.Invoke(this, file);

					await SendFileAsync(file, fileData.FileId, encryptor, cancellationToken);

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

		private async Task SendFileAsync(FileTransferModel file, Guid fileId, ICryptoTransform? encryptor = null, CancellationToken cancellationToken = default)
		{
			var tcs = new TaskCompletionSource();
			IAsyncEnumerable<byte[]> fileStream = GenerateFileStream(file, tcs.SetResult, tcs.SetException, tcs.SetCanceled, encryptor, cancellationToken);

			await _connection.SendAsync(ServerConstants.FileHub.SendFile, ReceiverId, fileStream, fileId);
			await tcs.Task;
		}

		private async Task ReceiveFiles(GlobalRequestModel request, Aes? aes = null, CancellationToken cancellationToken = default)
		{
			for (int i = 0; i < request.Files.Count; i++)
			{
				using var decryptor = aes?.CreateDecryptor();

				string filePath = FileHelper.GetUniqueFilePath(request.Files[i].Name, _storageService.SaveFolder);
				FileTransferModel fileTransferModel = new(filePath, request.Files[i].Size, TransferType.Global)
				{
					Status = TransferStatus.InProgress,
					Sender = request.Sender
				};

				try
				{
					ReceivingFileStarted?.Invoke(this, fileTransferModel);

					await ReceiveFile(fileTransferModel, request.Files[i].FileId, i == (request.Files.Count - 1), decryptor, cancellationToken);

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

		private async Task ReceiveFile(FileTransferModel file, Guid fileId, bool isLast, ICryptoTransform? decryptor = null, CancellationToken cancellationToken = default)
		{
			using FileStream fs = new(file.Path, FileMode.Create, FileAccess.Write);

			var fileStream = _connection.StreamAsync<byte[]>(ServerConstants.FileHub.ReceiveFile, fileId, isLast, cancellationToken);

			if (decryptor is not null)
			{
				var blockSize = decryptor.OutputBlockSize;
				int bufferSize = FileHelper.GetBufferSizeByFileSize(file.Size);
				var size = bufferSize > file.Size ? (int)file.Size : bufferSize;
				var padding = (blockSize - (size % blockSize)) % blockSize;
				byte[] buffer = new byte[size + padding];

				int bytesWritten;
				await foreach (var chunk in fileStream)
				{
					bytesWritten = decryptor.TransformBlock(chunk, 0, chunk.Length, buffer, 0);

					await fs.WriteAsync(buffer.AsMemory(0, bytesWritten - padding), cancellationToken);
					file.CurrentProgress += (bytesWritten - padding);

					size = bufferSize > file.Size - file.CurrentProgress ? (int)(file.Size - file.CurrentProgress) : bufferSize;
					padding = (blockSize - (size % blockSize)) % blockSize;
					buffer = buffer.Length > size + padding ? new byte[size + padding] : buffer;
				}
			}
			else
			{
				await foreach (var chunk in fileStream)
				{
					await fs.WriteAsync(chunk.AsMemory(0, chunk.Length), cancellationToken);
					file.CurrentProgress += chunk.Length;
				}
			}

			file.Status = file.CurrentProgress == file.Size
				? TransferStatus.Finished
				: throw new TransferException($"The {Path.GetFileName(file.Path)} file data was not received completely");
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S3358:Ternary operators should not be nested", Justification = "My code my rules.")]
		private static async IAsyncEnumerable<byte[]> GenerateFileStream(
			FileTransferModel file,
			Action? successCallback,
			Action<Exception>? failCallback,
			Action? cancelCallback,
			ICryptoTransform? encryptor = null,
			[EnumeratorCancellation] CancellationToken cancellationToken = default)
		{
			var useEncryption = encryptor is not null;

			FileStream? fs = null;
			try
			{
				fs = new(file.Path, FileMode.Open, FileAccess.Read);
			}
			catch (Exception ex)
			{
				if (fs is not null)
				{
					await fs.DisposeAsync();
				}

				failCallback?.Invoke(ex);
				throw;
			}

			var blockSize = encryptor?.OutputBlockSize ?? 0;
			var bufferSize = FileHelper.GetBufferSizeByFileSize(file.Size);
			var size = fs.Length < bufferSize ? (int)fs.Length : bufferSize;
			var padding = useEncryption ? (blockSize - (size % blockSize)) % blockSize : 0;
			var buffer = new byte[size + padding];
			var chunk = new byte[size + padding];

			int bytesRead = 0;
			do
			{
				try
				{
					bytesRead = await fs.ReadAsync(buffer.AsMemory(0, size), cancellationToken);

					if (bytesRead == 0) break;

					padding = useEncryption ? (blockSize - (bytesRead % blockSize)) % blockSize : 0;

					chunk = chunk.Length > bytesRead + padding ? new byte[bytesRead + padding] : chunk;

					encryptor?.TransformBlock(buffer, 0, bytesRead + padding, chunk, 0);

					file.CurrentProgress += bytesRead;
				}
				catch (OperationCanceledException)
				{
					await fs.DisposeAsync();

					cancelCallback?.Invoke();
					throw;
				}
				catch (Exception ex)
				{
					await fs.DisposeAsync();

					failCallback?.Invoke(ex);
					throw;
				}

				yield return useEncryption
					? chunk
					: buffer.Length > bytesRead ? buffer.AsSpan(0, bytesRead).ToArray() : buffer;

			} while (bytesRead > 0);

			await fs.DisposeAsync();

			successCallback?.Invoke();
		}

		private void HandleFailedFile(string filePath)
		{
			FileHelper.DeleteFileIfExists(filePath);
			ReceivingFileFailed?.Invoke(this, filePath);
		}

		private string GetConnectionUrl(string? basePath)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(basePath);

			Uri baseUrl = new(basePath);
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

			if (_connection is not null)
			{
				await _connection.StopAsync();
				await _connection.DisposeAsync();
			}
		}
	}
}
