using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Client.Constants;
using Client.Extensions;
using Client.Helpers;
using Client.Interfaces;
using Domain.Enums;
using Domain.Models;

namespace Client.Services
{
	public class LocalTransferService : IDisposable
	{
		private readonly IDeviceService _deviceService;
		private readonly IStorageService _storageService;

		private bool _disposed;

		private TcpListener TcpListener { get; set; }

		public bool IsListening { get; private set; }
		public bool IsReceiving { get; private set; }
		public bool IsSending { get; private set; }

		public int ReceiveTimeout { get; set; } = 15_000;
		public int SendTimeout { get; set; } = 15_000;

		public IPAddress? ReceiverIp { get; private set; }

		public event EventHandler<FileTransferModel>? ReceivingFileStarted;
		public event EventHandler<FileTransferModel>? ReceivingFileEnded;
		public event EventHandler<string>? ReceivingFileFailed;

		public event EventHandler? ReceivingStopped;
		public event EventHandler? ReceivingFinishedSuccessfully;

		public event EventHandler<FileTransferModel>? SendingFileStarted;
		public event EventHandler<FileTransferModel>? SendingFileEnded;
		public event EventHandler<string>? SendingFileFailed;

		public event EventHandler? SendingStopped;
		public event EventHandler? SendingFinishedSuccessfully;

		public event EventHandler? ListeningStarted;
		public event EventHandler? ListeningStopped;

		public event EventHandler<string>? ExceptionHandled;

		public Func<LocalRequestModel, Task<bool>>? OnSendFilesRequest { get; set; }

		private CancellationTokenSource? ListenerTokenSource { get; set; }
		private CancellationTokenSource? ClientTokenSource { get; set; }
		private CancellationTokenSource? ReceivingTokenSource { get; set; }

		public LocalTransferService(IDeviceService deviceService, IStorageService storageService)
		{
			_deviceService = deviceService;
			_storageService = storageService;

			TcpListener = new TcpListener(IPAddress.Any, NetworkConstants.Port);
		}

		public async Task StartSendingAsync(IPAddress ip, List<FileModel> files, bool useEncryption = false)
		{
			if (files is null || files.Count == 0 || ClientTokenSource is not null)
				return;

			var tcpClient = new TcpClient();

			ClientTokenSource = new CancellationTokenSource();
			var token = ClientTokenSource.Token;

			ECDiffieHellman? ecdh = null;
			Encryption? encryption = null;

			try
			{
				ReceiverIp = ip;
				IsSending = true;

				await tcpClient.ConnectAsync(ip, NetworkConstants.Port, token);
				NetworkStream stream = tcpClient.GetStream();

				if (useEncryption)
				{
					ecdh = ECDiffieHellman.Create();
					encryption = new(ecdh.PublicKey.ExportSubjectPublicKeyInfo());
				}

				await SendRequestAsync(stream, files, encryption, token);

				// waiting for the response from the receiver
				var response = await ReceiveResponseAsync(stream, ip, token);

				if (response.IsAccepted)
				{
					Aes? aes = null;

					if (ecdh is not null && (response?.Encryption?.PublicKey is null || response?.Encryption?.IV is null))
					{
						throw new CryptographicException("Not all encryption parameters are provided by receiver.");
					}

					if (ecdh is not null)
					{
						var receiverEcdh = ECDiffieHellman.Create();
						receiverEcdh.ImportSubjectPublicKeyInfo(response.Encryption!.PublicKey, out _);

						byte[] aesKey = ecdh.DeriveKeyMaterial(receiverEcdh.PublicKey);

						aes = Aes.Create();
						aes.Padding = PaddingMode.None;
						aes.Key = aesKey;
						aes.IV = response.Encryption.IV!;
					}

					try
					{
						await SendFilesAsync(stream, files, response.Receiver, aes, token);
					}
					finally
					{
						aes?.Dispose();
					}

					SendingFinishedSuccessfully?.Invoke(this, EventArgs.Empty);
				}
			}
			catch (Exception ex) when (ex is SocketException sex1 && (sex1.SocketErrorCode is SocketError.Shutdown or SocketError.ConnectionReset or SocketError.ConnectionAborted) ||
									   ex is IOException && ex.InnerException is SocketException sex2 && (sex2.SocketErrorCode is SocketError.Shutdown or SocketError.ConnectionReset or SocketError.ConnectionAborted))
			{
				ExceptionHandled?.Invoke(this, "It seems the receiver cancelled the operation.");
			}
			catch (TimeoutException)
			{
				ExceptionHandled?.Invoke(this, "Sending cancelled due to timeout.");
			}
			catch (OperationCanceledException ex) when (!ClientTokenSource.IsCancellationRequested)
			{
				// if operation cancelled not by us show error message
				ExceptionHandled?.Invoke(this, ex.Message);
			}
			catch (OperationCanceledException)
			{
				// otherwise swallow it
			}
			catch (Exception ex)
			{
				ExceptionHandled?.Invoke(this, ex.Message);
			}
			finally
			{
				tcpClient.Close();

				ClientTokenSource?.Dispose();
				ClientTokenSource = null;

				ReceiverIp = null;
				IsSending = false;
				SendingStopped?.Invoke(this, EventArgs.Empty);

				ecdh?.Dispose();
			}
		}

		public void StopSending()
		{
			ClientTokenSource?.Cancel();
		}

		private async Task SendRequestAsync(NetworkStream stream, List<FileModel> filesToSend, Encryption? encryption = null, CancellationToken cancellationToken = default)
		{
			List<FileMetadata> filesMetadata = filesToSend
				.Select(f => new FileMetadata(Path.GetFileName(f.Path), f.Size))
				.ToList();

			var localDevice = new LocalDeviceModel(_deviceService.GetCurrentDeviceInfo());
			var request = new LocalRequestModel(localDevice, filesMetadata)
			{
				Encryption = encryption,
			};

			string requestJson = JsonSerializer.Serialize(request);
			byte[] requestBytes = Encoding.UTF8.GetBytes(requestJson);
			byte[] requestSizeBytes = BitConverter.GetBytes(requestBytes.Length);

			await stream.WriteWithTimeoutAsync(requestSizeBytes, SendTimeout, cancellationToken);
			await stream.WriteWithTimeoutAsync(requestBytes, SendTimeout, cancellationToken);
		}

		private static async Task<LocalRequestModel> ReceiveRequestAsync(TcpClient tcpClient, CancellationToken cancellationToken = default)
		{
			var stream = tcpClient.GetStream();
			var requestSize = await stream.ReadInt32Async(cancellationToken);
			var requestJson = await stream.ReadStringAsync(requestSize, cancellationToken);

			var request = JsonSerializer.Deserialize<LocalRequestModel>(requestJson)
				?? throw new JsonException("Could not deserialize request.");

			request.Sender.IP = (tcpClient.Client.RemoteEndPoint as IPEndPoint)?.Address;

			return request;
		}

		private async Task SendResponseAsync(NetworkStream stream, bool isAccepted, Encryption? encryption = null, CancellationToken cancellationToken = default)
		{
			var localDevice = new LocalDeviceModel(_deviceService.GetCurrentDeviceInfo());
			var response = new LocalResponseModel(isAccepted, localDevice)
			{
				Encryption = encryption,
			};

			string responseJson = JsonSerializer.Serialize(response);
			byte[] responseBytes = Encoding.UTF8.GetBytes(responseJson);
			byte[] responseSizeBytes = BitConverter.GetBytes(responseBytes.Length);

			await stream.WriteWithTimeoutAsync(responseSizeBytes, SendTimeout, cancellationToken);
			await stream.WriteWithTimeoutAsync(responseBytes, SendTimeout, cancellationToken);
		}

		private static async Task<LocalResponseModel> ReceiveResponseAsync(NetworkStream stream, IPAddress ip, CancellationToken cancellationToken = default)
		{
			var responseSize = await stream.ReadInt32Async(cancellationToken);
			var responseJson = await stream.ReadStringAsync(responseSize, cancellationToken);

			var response = JsonSerializer.Deserialize<LocalResponseModel>(responseJson)
				?? throw new JsonException("Could not deserialize response.");

			response.Receiver.IP = ip;

			return response;
		}

		private async Task SendFilesAsync(NetworkStream stream, List<FileModel> files, LocalDeviceModel receiver, Aes? aes = null, CancellationToken cancellationToken = default)
		{
			foreach (var fileModel in files)
			{
				using var encryptor = aes?.CreateEncryptor();

				FileTransferModel file = new(fileModel, TransferType.Local)
				{
					Status = TransferStatus.InProgress,
					Receiver = receiver
				};

				try
				{
					SendingFileStarted?.Invoke(this, file);

					await SendFileAsync(stream, file, encryptor, cancellationToken);

					file.Status = TransferStatus.Finished;
					SendingFileEnded?.Invoke(this, file);
				}
				catch (Exception)
				{
					file.Status = TransferStatus.Failed;
					SendingFileFailed?.Invoke(this, fileModel.Path);
					throw;
				}
			}
		}

		private async Task SendFileAsync(NetworkStream networkStream, FileTransferModel file, ICryptoTransform? encryptor = null, CancellationToken cancellationToken = default)
		{
			var useEncryption = encryptor is not null;

			Stream stream = useEncryption
				? new CryptoStream(networkStream, encryptor!, CryptoStreamMode.Write, leaveOpen: true)
				: networkStream;

			await using var fs = new FileStream(file.Path, FileMode.Open, FileAccess.Read);

			var blockSize = encryptor?.OutputBlockSize ?? 0;
			var bufferSize = FileHelper.GetBufferSizeByFileSize(file.Size);
			var size = fs.Length < bufferSize ? (int)fs.Length : bufferSize;
			var padding = useEncryption ? (blockSize - (size % blockSize)) % blockSize : 0;
			var buffer = new byte[size + padding];

			int bytesRead;
			while ((bytesRead = await fs.ReadAsync(buffer.AsMemory(0, size), cancellationToken)) > 0)
			{
				padding = useEncryption ? (blockSize - (bytesRead % blockSize)) % blockSize : 0;

				await stream.WriteWithTimeoutAsync(buffer.AsMemory(0, bytesRead + padding), SendTimeout, cancellationToken);
				await stream.FlushAsync(cancellationToken);
				file.CurrentProgress += bytesRead;
			}

			if (stream is CryptoStream cryptoStream)
			{
				await cryptoStream.DisposeAsync();
			}
		}

		private async Task ReceiveFilesAsync(NetworkStream stream, LocalRequestModel request, Aes? aes = null, CancellationToken cancellationToken = default)
		{
			foreach (var fileMetadata in request.Files)
			{
				using var decryptor = aes?.CreateDecryptor();

				string filePath = FileHelper.GetUniqueFilePath(fileMetadata.Name, _storageService.SaveFolder);
				FileTransferModel file = new(filePath, fileMetadata.Size, TransferType.Local)
				{
					Status = TransferStatus.InProgress,
					Sender = request.Sender
				};

				try
				{
					ReceivingFileStarted?.Invoke(this, file);

					await ReceiveFileAsync(stream, file, decryptor, cancellationToken);

					file.Status = TransferStatus.Finished;
					ReceivingFileEnded?.Invoke(this, file);
				}
				catch (Exception)
				{
					file.Status = TransferStatus.Failed;
					HandleFailedFile(file.Path);
					throw;
				}
			}
		}

		private async Task ReceiveFileAsync(NetworkStream networkStream, FileTransferModel file, ICryptoTransform? decryptor = null, CancellationToken cancellationToken = default)
		{
			var useEncryption = decryptor is not null;

			Stream stream = useEncryption
				? new CryptoStream(networkStream, decryptor!, CryptoStreamMode.Read, leaveOpen: true)
				: networkStream;

			await using var fs = new FileStream(file.Path, FileMode.Create, FileAccess.Write);

			var blockSize = decryptor?.InputBlockSize ?? 0;
			int bufferSize = FileHelper.GetBufferSizeByFileSize(file.Size);
			var size = bufferSize > file.Size ? (int)file.Size : bufferSize;
			var padding = useEncryption ? (blockSize - (size % blockSize)) % blockSize : 0;
			byte[] buffer = new byte[size + padding];

			long receivedSize = 0;

			while (receivedSize < file.Size)
			{
				var bytesRead = await stream.ReadWithTimeoutAsync(buffer, ReceiveTimeout, cancellationToken);

				if (bytesRead == 0)
				{
					throw new OperationCanceledException("Sender cancelled the operation or was disconnected.");
				}

				await fs.WriteAsync(buffer.AsMemory(0, bytesRead - padding), cancellationToken);

				receivedSize += (bytesRead - padding);
				file.CurrentProgress = receivedSize;

				size = bufferSize > file.Size - receivedSize ? (int)(file.Size - receivedSize) : bufferSize;
				padding = useEncryption ? (blockSize - (size % blockSize)) % blockSize : 0;
				buffer = buffer.Length > size + padding ? new byte[size + padding] : buffer;
			}

			if (stream is CryptoStream cryptoStream)
			{
				await cryptoStream.DisposeAsync();
			}
		}

		public async Task StartListeningAsync()
		{
			try
			{
				TcpListener.Start();
				IsListening = true;
				ListeningStarted?.Invoke(this, EventArgs.Empty);

				ListenerTokenSource = new CancellationTokenSource();
				while (!ListenerTokenSource.IsCancellationRequested)
				{
					TcpClient tcpClient = await TcpListener.AcceptTcpClientAsync(ListenerTokenSource.Token);
					_ = ProcessClientAsync(tcpClient);
				}
			}
			catch (OperationCanceledException)
			{
				// listening stopped by user
			}
			catch (Exception ex)
			{
				ExceptionHandled?.Invoke(this, ex.Message);
			}
			finally
			{
				ListenerTokenSource?.Dispose();
				ListenerTokenSource = null;

				await StopListeningAsync();
			}
		}

		public async Task StopListeningAsync()
		{
			if (!IsListening)
			{
				return;
			}

			if (ListenerTokenSource is not null)
			{
				await ListenerTokenSource.CancelAsync();
			}

			try
			{
				TcpListener.Stop();
			}
			catch (Exception)
			{
				// ignore
			}

			IsListening = false;
			ListeningStopped?.Invoke(this, EventArgs.Empty);
		}

		public void StopReceiving()
		{
			IsReceiving = false;
			ReceivingTokenSource?.Cancel();
		}

		private async Task ProcessClientAsync(TcpClient tcpClient)
		{
			NetworkStream stream = tcpClient.GetStream();

			// retrieving request from the remote host
			LocalRequestModel request = await ReceiveRequestAsync(tcpClient);

			// getting user response
			bool isAccepted = await GetUserResponseAsync(request);

			Encryption? encryption = null;
			Aes? aes = null;

			if (isAccepted && request.Encryption?.PublicKey is { } senderKey)
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

			// sending response to the remote host
			await SendResponseAsync(stream, isAccepted, encryption);

			// close connection if user declined request
			if (!isAccepted)
			{
				tcpClient.Close();
				return;
			}

			try
			{
				if (string.IsNullOrEmpty(_storageService.SaveFolder))
				{
					throw new InvalidOperationException("Destination folder not setted.");
				}

				IsReceiving = true;
				ReceivingTokenSource = new CancellationTokenSource();

				await ReceiveFilesAsync(stream, request, aes, ReceivingTokenSource.Token);

				ReceivingFinishedSuccessfully?.Invoke(this, EventArgs.Empty);
			}
			catch (TimeoutException)
			{
				ExceptionHandled?.Invoke(this, "Receiving cancelled due to timeout.");
			}
			catch (IOException ex) when (ex.InnerException is SocketException sex)
			{
				string message = ex.Message;

				if (sex.SocketErrorCode == SocketError.ConnectionReset)
				{
					message = "It seems the receiver was disconnected.";
				}
				else if (sex.SocketErrorCode == SocketError.ConnectionAborted)
				{
					message = "Connection lost.";
				}

				ExceptionHandled?.Invoke(this, message);
			}
			catch (OperationCanceledException ex)
			{
				// if operation cancelled not by user show error message
				if (IsReceiving)
				{
					ExceptionHandled?.Invoke(this, ex.Message);
				}
			}
			catch (Exception ex)
			{
				ExceptionHandled?.Invoke(this, ex.Message);
			}
			finally
			{
				IsReceiving = false;
				ReceivingTokenSource?.Dispose();
				ReceivingTokenSource = null;

				aes?.Dispose();

				tcpClient.Close();
				ReceivingStopped?.Invoke(this, EventArgs.Empty);
			}
		}

		private async Task<bool> GetUserResponseAsync(LocalRequestModel request)
		{
			bool isAccepted = false;

			if (OnSendFilesRequest is not null)
			{
				isAccepted = await OnSendFilesRequest.Invoke(request);
			}

			return isAccepted;
		}

		private void HandleFailedFile(string filePath)
		{
			FileHelper.DeleteFileIfExists(filePath);
			ReceivingFileFailed?.Invoke(this, filePath);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposed)
			{
				if (disposing)
				{
					TcpListener?.Dispose();

					ClientTokenSource?.Dispose();
					ListenerTokenSource?.Dispose();
					ReceivingTokenSource?.Dispose();
				}

				_disposed = true;
			}
		}

		public void Dispose()
		{
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}
	}
}
