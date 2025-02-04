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

        private readonly ILogger<GlobalTransferService> _logger;

        private readonly HubConnection _connection;

        private CancellationTokenSource? ConnectTokenSource { get; set; }

        public bool IsReceiving { get; private set; }

        private GlobalRequestModel? ReceiveRequest { get; set; }

        private GlobalRequestModel? SendRequest { get; set; }

        private List<FileModel> SendFiles { get; set; } = [];

        public bool IsSending { get; private set; }

        public long SessionId { get; private set; }

        public long ReceiverId { get; private set; }

        public HubConnectionState ConnectionState => _connection.State;

        public event EventHandler? Disconnected;

        public event EventHandler<long>? Connected;

        public event EventHandler? SendingStarted;

        public event EventHandler? SendingStopped;

        public event EventHandler<FileModel>? ReceivingFileStarted;
        public event EventHandler<string>? ReceivingFileFailed;

        public Func<GlobalRequestModel, Task<bool>>? OnSendFilesRequest { get; set; }

        public GlobalTransferService(
            IStorageService storageService,
            IConfiguration configuration,
            ILogger<GlobalTransferService> logger)
        {
            _storageService = storageService;
            _logger = logger;

            Uri baseUrl = new(configuration.GetValue<string>(Constants.Config.Server.BASE_URL_PATH) ?? "");
            string fileHubUrl = configuration.GetValue<string>(Constants.Config.Server.FILE_HUB_PATH) ?? "";
            string connectUrl = new Uri(baseUrl, fileHubUrl).AbsoluteUri;

            _connection = new HubConnectionBuilder()
                .WithUrl(connectUrl)
                .Build();

            ListenConnection();
            ListenRequests();
            ListenFiles();
        }

        public async Task ConnectAsync()
        {
            if (_connection.State != HubConnectionState.Disconnected)
            {
                return;
            }

            try
            {
                ConnectTokenSource = new CancellationTokenSource();

                await _connection.StartAsync(ConnectTokenSource.Token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while connecting to server hub");
            }
            finally
            {
                ConnectTokenSource?.Dispose();
                ConnectTokenSource = null;
            }
        }

        public async Task DisconnectAsync()
        {
            switch (_connection.State)
            {
                case HubConnectionState.Connecting:
                case HubConnectionState.Reconnecting:
                    await CancelConnectAsync();
                    break;
                case HubConnectionState.Connected:
                    await _connection.StopAsync();
                    break;
                default:
                    return;
            }
        }

        public async Task StartSendingAsync(long receiverSessionId, List<FileModel> files)
        {
            if (Math.Log10(receiverSessionId) is < 10 or > 11)
            {
                throw new ArgumentException("Session ID must have 11 significant digits.", nameof(receiverSessionId));
            }

            if (_connection.State != HubConnectionState.Connected)
            {
                return;
            }

            try
            {
                IsSending = true;
                ReceiverId = receiverSessionId;

                await SendRequestAsync(receiverSessionId, files ?? []);

                SendingStarted?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while sending");
                StopSending();
            }
        }

        public void StopSending()
        {
            if (IsSending)
            {
                IsSending = false;
                ReceiverId = 0;
                SendRequest = null;
                SendFiles.Clear();
                SendingStopped?.Invoke(this, EventArgs.Empty);
            }
        }

        public async Task SendRequestAsync(long receiverSessionId, List<FileModel> files)
        {
            SendFiles = files;

            List<FileMetadata> filesMetadata = files
                .Select(f => new FileMetadata(Path.GetFileName(f.Path), f.Size))
                .ToList();

            SendRequest = new GlobalRequestModel(SessionId, filesMetadata);
            await _connection.InvokeAsync(ServerConstants.FileHub.SendRequest, receiverSessionId, SendRequest);
        }

        private async Task CancelConnectAsync()
        {
            if (ConnectTokenSource is not null)
            {
                await ConnectTokenSource.CancelAsync();
            }
        }

        private void ListenConnection()
        {
            _connection.On<long>(ServerConstants.FileHub.HubConnected, OnConnected);

            _connection.Closed += OnConnectionClosed;
        }

        private void ListenRequests()
        {
            _connection.On<GlobalRequestModel>(ServerConstants.FileHub.ReceiveRequest, OnReceiveRequest);
            _connection.On<bool>(ServerConstants.FileHub.ReceiveResponse, OnReceiveResponse);
        }

        private void ListenFiles()
        {
            _connection.On<FileMetadata, Guid>(ServerConstants.FileHub.StartReceivingFile, OnStartReceivingFile);
        }

        private void OnConnected(long sessionId)
        {
            SessionId = sessionId;
            Connected?.Invoke(this, SessionId);
        }

        private Task OnConnectionClosed(Exception? ex)
        {
            SessionId = 0;
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
            ReceiveRequest = null;
            if (OnSendFilesRequest is not null)
            {
                accepted = await OnSendFilesRequest(request);
                ReceiveRequest = request;
            }
            IsReceiving = accepted;
            await _connection.InvokeAsync(ServerConstants.FileHub.SendResponse, request.SenderSessionId, accepted);
        }

        private async Task OnReceiveResponse(bool accepted)
        {
            if (!IsSending)
            {
                return;
            }

            if (accepted && SendFiles.Count > 0)
            {
                foreach (var file in SendFiles)
                {
                    var fileMetadata = new FileMetadata(Path.GetFileName(file.Path), file.Size);
                    var fileId = Guid.NewGuid();
                    await _connection.SendAsync(ServerConstants.FileHub.StartSendingFile, ReceiverId, fileMetadata, fileId);

                    IAsyncEnumerable<byte[]> fileStream = GenerateFileStream(file);
                    await _connection.SendAsync(ServerConstants.FileHub.SendFile, ReceiverId, fileStream, fileId);
                }
            }
            else
            {
                StopSending();
            }
        }

        private async Task OnStartReceivingFile(FileMetadata fileMetadata, Guid fileId)
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

                var cancellationTokenSource = new CancellationTokenSource();
                var fileStream = _connection.StreamAsync<byte[]>(ServerConstants.FileHub.ReceiveFile, fileId, cancellationTokenSource.Token);

                await foreach (var chunk in fileStream)
                {
                    await fs.WriteAsync(chunk.AsMemory(0, chunk.Length));
                    file.CurrentProgress += chunk.Length;
                }

                file.Status = TransferStatus.Finished;
            }
            catch (Exception ex)
            {
                FileHelper.DeleteFileIfExists(file.Path);
                file.Status = TransferStatus.Failed;
                ReceivingFileFailed?.Invoke(this, filePath);
                throw;
            }
        }

        private static async IAsyncEnumerable<byte[]> GenerateFileStream(FileModel file)
        {
            byte[] buffer;
            int bufferSize = FileHelper.GetBufferSizeByFileSize(file.Size);
            using FileStream fs = new (file.Path, FileMode.Open, FileAccess.Read);
            long size = fs.Length < bufferSize ? fs.Length : bufferSize;
            buffer = new byte[size];
            int bytesRead;
            while ((bytesRead = await fs.ReadAsync(buffer)) > 0)
            {
                if (bytesRead < buffer.Length)
                {
                    Array.Resize(ref buffer, bytesRead);
                }
                yield return buffer;
            }
        }

        public async ValueTask DisposeAsync()
        {
            ConnectTokenSource?.Dispose();

            _connection.Remove(ServerConstants.FileHub.HubConnected);
            _connection.Remove("StartReceivingFile");
            _connection.Remove("ReceivingFile");
            _connection.Remove("EndReceivingFile");

            await _connection.StopAsync();
        }
    }
}
