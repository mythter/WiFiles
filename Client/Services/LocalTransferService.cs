using Client.Models;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Client.Extensions;
using Client.Constants;
using Client.Interfaces;
using System.Text.Json;
using Client.Helpers;
using Client.Enums;

namespace Client.Services
{
    //public delegate void StartReceivingFileEventHandler(FileModel file);

    public class LocalTransferService
    {
        private readonly IDeviceService _deviceService;
        private readonly IStorageService _storageService;

        private TcpListener TcpListener { get; set; }
        private TcpClient TcpClient { get; set; }

        public int PortListen { get; set; } = NetworkConstants.Port;
        public int PortConnect { get; set; } = NetworkConstants.Port;

        public bool IsListening { get; private set; }
        public bool IsReceiving { get; private set; }

        //public int BufferSize { get; set; } = 1024;

        public int ReceiveTimeout { get; set; } = 15_000;
        public int SendTimeout { get; set; } = 15_000;

        public IPAddress? ReceiverIp { get; private set; }

        public event EventHandler<FileModel>? ReceivingFileStarted;
        public event EventHandler<string>? ReceivingFileFailed;

        public event EventHandler? ReceivingStopped;
        public event EventHandler? ReceivingFinishedSuccessfully;

        public event EventHandler? SendingFinishedSuccessfully;

        public event EventHandler? ListeningStarted;
        public event EventHandler? ListeningStopped;

        public event EventHandler<string>? ExceptionHandled;

        public Func<RequestModel, Task<bool>>? OnSendFilesRequest { get; set; }

        private CancellationTokenSource? ListenerTokenSource { get; set; }
        private CancellationTokenSource? ClientTokenSource { get; set; }
        private CancellationTokenSource? ReceivingTokenSource { get; set; }

        public LocalTransferService(IDeviceService deviceService, IStorageService storageService)
        {
            _deviceService = deviceService;
            _storageService = storageService;

            TcpListener = new TcpListener(IPAddress.Any, PortListen);
            TcpClient = new TcpClient();
        }

        public async Task StartSendingAsync(IPAddress ip, List<FileModel> files)
        {
            if (files is null || files.Count == 0)
                return;

            try
            {
                if (TcpClient.Connected)
                {
                    StopSending();
                    TcpClient.Close();
                    TcpClient = new TcpClient();
                }

                ReceiverIp = ip;
                ClientTokenSource = new CancellationTokenSource();
                await TcpClient.ConnectAsync(ip, PortConnect, ClientTokenSource.Token);
                NetworkStream stream = TcpClient.GetStream();

                await SendRequestAsync(files, stream, ClientTokenSource.Token);

                // waiting for the response from the receiver
                bool isAccepted = await stream.ReadBooleanAsync();

                if (isAccepted)
                {
                    await SendFilesAsync(stream, files, ClientTokenSource.Token);

                    SendingFinishedSuccessfully?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception ex) when (ex is SocketException sex1 && (sex1.SocketErrorCode == SocketError.Shutdown || sex1.SocketErrorCode == SocketError.ConnectionReset) ||
                                       ex is IOException && ex.InnerException is SocketException sex2 && (sex2.SocketErrorCode == SocketError.Shutdown || sex2.SocketErrorCode == SocketError.ConnectionReset))
            {
                ExceptionHandled?.Invoke(this, "It seems the receiver cancelled the operation.");
            }
            catch (OperationCanceledException ex)
            {
                // if operation cancelled not by us show error message
                if (ClientTokenSource is not null)
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
                ClientTokenSource?.Dispose();
                ClientTokenSource = null;
                ReceiverIp = null;
                TcpClient.Close();
                TcpClient = new TcpClient();
            }
        }

        public void StopSending()
        {
            ClientTokenSource?.Cancel();
            ClientTokenSource?.Dispose();
            ClientTokenSource = null;
            ReceiverIp = null;
        }

        private async Task SendRequestAsync(List<FileModel> filesToSend, NetworkStream stream, CancellationToken cancellationToken)
        {
            DeviceInfoModel deviceInfo = _deviceService.GetCurrentDeviceInfo();
            List<FileMetadata> filesMetadata = filesToSend
                .Select(f =>
                    new FileMetadata
                    {
                        Name = Path.GetFileName(f.Path),
                        Size = f.Size
                    })
                .ToList();

            RequestModel request = new RequestModel
            {
                DeviceModel = deviceInfo.Model,
                DeviceName = deviceInfo.Name,
                DeviceType = deviceInfo.Type,
                Files = filesMetadata
            };

            string requestJson = JsonSerializer.Serialize(request);
            byte[] requestBytes = Encoding.UTF8.GetBytes(requestJson);
            byte[] requestSizeBytes = BitConverter.GetBytes(requestBytes.Length);

            await stream.WriteWithTimeoutAsync(requestSizeBytes, SendTimeout, cancellationToken);
            await stream.WriteWithTimeoutAsync(requestBytes, SendTimeout, cancellationToken);
        }

        private static async Task<RequestModel> ReceiveRequestAsync(NetworkStream stream, CancellationToken cancellationToken = default)
        {
            int requestSize = await stream.ReadInt32Async(cancellationToken);
            string requestJson = await stream.ReadStringAsync(requestSize, cancellationToken);

            RequestModel request = JsonSerializer.Deserialize<RequestModel>(requestJson)
                ?? throw new JsonException("Could not deserialize request JSON.");

            return request;
        }

        private async Task SendFilesAsync(NetworkStream stream, List<FileModel> files, CancellationToken cancellationToken)
        {
            byte[] buffer;
            foreach (FileModel file in files)
            {
                int bufferSize = GetBufferSizeByFileSize(file.Size);
                using FileStream fs = new FileStream(file.Path, FileMode.Open, FileAccess.Read);
                long size = fs.Length < bufferSize ? fs.Length : bufferSize;
                buffer = new byte[size];
                int bytesRead;
                while ((bytesRead = await fs.ReadAsync(buffer)) > 0)
                {
                    if (bytesRead < buffer.Length)
                    {
                        Array.Resize(ref buffer, bytesRead);
                    }
                    await stream.WriteWithTimeoutAsync(buffer, SendTimeout, cancellationToken);
                }
            }
        }

        private async Task ReceiveFilesAsync(
            NetworkStream stream,
            List<FileMetadata> files,
            CancellationToken cancellationToken)
        {
            foreach (var file in files)
            {
                await ReceiveFileAsync(stream, file, cancellationToken);
            }
        }

        private async Task ReceiveFileAsync(NetworkStream stream, FileMetadata fileMetadata, CancellationToken cancellationToken)
        {
            string filePath = FileHelper.GetUniqueFilePath(fileMetadata.Name, _storageService.SaveFolder);
            FileModel file = new FileModel(filePath, fileMetadata.Size)
            {
                Status = TransferStatus.InProgress
            };

            try
            {
                using FileStream fs = new FileStream(file.Path, FileMode.Create, FileAccess.Write);
                ReceivingFileStarted?.Invoke(this, file);

                long receivedSize = 0;
                byte[] buffer;
                int bufferSize = GetBufferSizeByFileSize(file.Size);
                while (receivedSize < file.Size)
                {
                    buffer = new byte[bufferSize < file.Size - receivedSize ? bufferSize : file.Size - receivedSize];
                    int size = await stream.ReadWithTimeoutAsync(buffer, ReceiveTimeout, cancellationToken);
                    if (size == 0)
                    {
                        throw new OperationCanceledException("Sender cancelled the operation or disconnected.");
                    }
                    await fs.WriteAsync(buffer.AsMemory(0, size));
                    receivedSize += size;
                    file.CurrentProgress = receivedSize;
                }

                file.Status = TransferStatus.Finished;
            }
            catch (Exception)
            {
                file.Status = TransferStatus.Failed;
                DeleteFileIfExists(file.Path);
                throw;
            }
        }

        public async Task StartListeningAsync()
        {
            try
            {
                IsListening = true;
                TcpListener.Start();
                ListeningStarted?.Invoke(this, EventArgs.Empty);

                ListenerTokenSource = new CancellationTokenSource();
                while (!ListenerTokenSource.IsCancellationRequested)
                {
                    TcpClient tcpClient = await TcpListener.AcceptTcpClientAsync(ListenerTokenSource.Token);
                    _ = ProcessClientAsync(tcpClient);
                }
            }
            catch (OperationCanceledException ex)
            {
                // listening stopped by user
            }
            catch (Exception ex)
            {
                ExceptionHandled?.Invoke(this, ex.Message);
            }
            finally
            {
                // IsListening true means that listening is not stopped by user
                if (IsListening)
                {
                    await StopListeningAsync();
                }

                ListenerTokenSource?.Dispose();
                ListenerTokenSource = null;
            }
        }

        public async Task StopListeningAsync()
        {
            IsListening = false;
            if (ListenerTokenSource is not null)
            {
                await ListenerTokenSource.CancelAsync();
            }

            TcpListener.Stop();
            ListeningStopped?.Invoke(this, EventArgs.Empty);
        }

        public void StopReceiving()
        {
            IsReceiving = false;
            ReceivingTokenSource?.Cancel();
            ReceivingTokenSource = null;
        }

        private async Task ProcessClientAsync(TcpClient tcpClient)
        {
            NetworkStream stream = tcpClient.GetStream();

            RequestModel request = await ReceiveRequestAsync(stream);
            request.IPAddress = ((IPEndPoint)tcpClient.Client.RemoteEndPoint!).Address;

            bool isAccepted = await GetUserResponseAsync(tcpClient, request);

            await stream.WriteBooleanAsync(isAccepted);

            if (!isAccepted)
            {
                tcpClient.Close();
                return;
            }

            IsReceiving = true;
            ReceivingTokenSource = new CancellationTokenSource();
            try
            {
                if (string.IsNullOrEmpty(_storageService.SaveFolder))
                {
                    throw new InvalidOperationException("Destination folder not setted.");
                }

                await ReceiveFilesAsync(stream, request.Files, ReceivingTokenSource.Token);

                ReceivingFinishedSuccessfully?.Invoke(this, EventArgs.Empty);
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
                if (IsReceiving)
                {
                    IsReceiving = false;
                    ReceivingTokenSource?.Dispose();
                    ReceivingTokenSource = null;
                }

                tcpClient.Close();
                ReceivingStopped?.Invoke(this, EventArgs.Empty);
            }
        }

        private async Task<bool> GetUserResponseAsync(TcpClient tcpClient, RequestModel request)
        {
            IPAddress? sender = (tcpClient.Client.RemoteEndPoint as IPEndPoint)?.Address;

            bool isAccepted = false;
            if (OnSendFilesRequest is not null)
            {
                isAccepted = await OnSendFilesRequest.Invoke(request);
            }

            return isAccepted;
        }

        private void DeleteFileIfExists(string? filePath)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                ReceivingFileFailed?.Invoke(this, filePath);
            }
        }

        private static int GetBufferSizeByFileSize(long fileSize)
        {
            return fileSize switch
            {                             // file size is:
                < 10_485_760 => 1024,    // less than 10 MB
                < 104_857_600 => 4096,    // less than 100 MB
                _ => 16384    // more or equal 100 MB
            };
        }
    }
}
