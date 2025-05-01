using System.Net;
using System.Net.Sockets;
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
		public bool IsSending{ get; private set; }

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

		public async Task StartSendingAsync(IPAddress ip, List<FileModel> files)
		{
			if (files is null || files.Count == 0 || ClientTokenSource is not null)
				return;

			var tcpClient = new TcpClient();

			ClientTokenSource = new CancellationTokenSource();
			var token = ClientTokenSource.Token;

			try
			{
				ReceiverIp = ip;
				IsSending = true;

				await tcpClient.ConnectAsync(ip, NetworkConstants.Port, token);
				NetworkStream stream = tcpClient.GetStream();

				await SendRequestAsync(files, stream, token);

				// waiting for the response from the receiver
				bool isAccepted = await stream.ReadBooleanAsync();

				if (isAccepted)
				{
					await SendFilesAsync(stream, files, ip, token);

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
			}
		}

		public void StopSending()
		{
			ClientTokenSource?.Cancel();
		}

		private async Task SendRequestAsync(List<FileModel> filesToSend, NetworkStream stream, CancellationToken cancellationToken)
		{
			List<FileMetadata> filesMetadata = filesToSend
				.Select(f => new FileMetadata(Path.GetFileName(f.Path), f.Size))
				.ToList();

			var localDevice = new LocalDeviceModel(_deviceService.GetCurrentDeviceInfo());
			var request = new LocalRequestModel(localDevice, filesMetadata);

			string requestJson = JsonSerializer.Serialize(request);
			byte[] requestBytes = Encoding.UTF8.GetBytes(requestJson);
			byte[] requestSizeBytes = BitConverter.GetBytes(requestBytes.Length);

			await stream.WriteWithTimeoutAsync(requestSizeBytes, SendTimeout, cancellationToken);
			await stream.WriteWithTimeoutAsync(requestBytes, SendTimeout, cancellationToken);
		}

		private static async Task<LocalRequestModel> ReceiveRequestAsync(TcpClient tcpClient, CancellationToken cancellationToken = default)
		{
			var stream = tcpClient.GetStream();
			int requestSize = await stream.ReadInt32Async(cancellationToken);
			string requestJson = await stream.ReadStringAsync(requestSize, cancellationToken);

			LocalRequestModel request = JsonSerializer.Deserialize<LocalRequestModel>(requestJson)
				?? throw new JsonException("Could not deserialize request JSON.");

			request.Sender.IP = (tcpClient.Client.RemoteEndPoint as IPEndPoint)?.Address;

			return request;
		}

		private async Task SendFilesAsync(NetworkStream stream, List<FileModel> files, IPAddress receiver, CancellationToken cancellationToken)
		{
			foreach (var fileModel in files)
			{
				FileTransferModel file = new(fileModel, TransferType.Local)
				{
					Status = TransferStatus.InProgress,
					Receiver = receiver.ToString()
				};

				try
				{
					SendingFileStarted?.Invoke(this, file);

					await SendFileAsync(stream, file, cancellationToken);

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

		private async Task SendFileAsync(NetworkStream stream, FileTransferModel file, CancellationToken cancellationToken)
		{
			var bufferSize = FileHelper.GetBufferSizeByFileSize(file.Size);
			await using var fs = new FileStream(file.Path, FileMode.Open, FileAccess.Read);
			var size = fs.Length < bufferSize ? fs.Length : bufferSize;
			var buffer = new byte[size];
			int bytesRead;
			while ((bytesRead = await fs.ReadAsync(buffer)) > 0)
			{
				if (bytesRead < buffer.Length)
				{
					Array.Resize(ref buffer, bytesRead);
				}
				await stream.WriteWithTimeoutAsync(buffer, SendTimeout, cancellationToken);
				file.CurrentProgress += bytesRead;
			}
		}

		private async Task ReceiveFilesAsync(NetworkStream stream, LocalRequestModel request, CancellationToken cancellationToken)
		{
			string? sender = request.Sender?.ToString();

			foreach (var fileMetadata in request.Files)
			{
				string filePath = FileHelper.GetUniqueFilePath(fileMetadata.Name, _storageService.SaveFolder);
				FileTransferModel file = new(filePath, fileMetadata.Size, TransferType.Local)
				{
					Status = TransferStatus.InProgress,
					Sender = request.Sender
				};

				try
				{
					ReceivingFileStarted?.Invoke(this, file);

					await ReceiveFileAsync(stream, file, cancellationToken);

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

		private async Task ReceiveFileAsync(NetworkStream stream, FileTransferModel file, CancellationToken cancellationToken)
		{
			using FileStream fs = new(file.Path, FileMode.Create, FileAccess.Write);
			long receivedSize = 0;
			byte[] buffer;
			int bufferSize = FileHelper.GetBufferSizeByFileSize(file.Size);
			while (receivedSize < file.Size)
			{
				buffer = new byte[bufferSize < file.Size - receivedSize ? bufferSize : file.Size - receivedSize];
				int size = await stream.ReadWithTimeoutAsync(buffer, ReceiveTimeout, cancellationToken);
				if (size == 0)
				{
					throw new OperationCanceledException("Sender cancelled the operation or was disconnected.");
				}
				await fs.WriteAsync(buffer.AsMemory(0, size));
				receivedSize += size;
				file.CurrentProgress = receivedSize;
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

			// getting current user response
			bool isAccepted = await GetUserResponseAsync(request);

			// sending response to the remote host
			await stream.WriteBooleanAsync(isAccepted);

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

				await ReceiveFilesAsync(stream, request, ReceivingTokenSource.Token);

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
