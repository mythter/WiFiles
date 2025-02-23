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

        private readonly ILogger<GlobalTransferService> _logger;

        private readonly HubConnection _connection;

        private CancellationTokenSource? ConnectTokenSource { get; set; }
        private CancellationTokenSource? SendRequestTokenSource { get; set; }
        private CancellationTokenSource? SendTokenSource { get; set; }
        private CancellationTokenSource? ReceiveTokenSource { get; set; }

        public bool IsReceiving { get; private set; }

        private GlobalRequestModel? ReceiveRequest { get; set; }

        private GlobalRequestModel? SendRequest { get; set; }

        private List<FileModel> FilesToSend { get; set; } = [];

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

        public event EventHandler? ReceivingStopped;
        public event EventHandler? ReceivingFinishedSuccessfully;

        public event EventHandler? SendingFinishedSuccessfully;

        public event EventHandler? ReceivingCancelled;
        public event EventHandler? SendingCancelled;

        public event EventHandler? ReceiverDisconnected;
        public event EventHandler? SenderDisconnected;

        public Func<GlobalRequestModel, Task<bool>>? OnSendFilesRequest { get; set; }

        public GlobalTransferService(
            IStorageService storageService,
            IConfiguration configuration,
            ILogger<GlobalTransferService> logger)
        {
            _storageService = storageService;
            _logger = logger;

            _connection = new HubConnectionBuilder()
                .WithUrl(GetConnectionUrl(configuration))
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
                    if (ConnectTokenSource is not null)
                    {
                        await ConnectTokenSource.CancelAsync();
                    }
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
            ReceiveRequest = null;

            ReceiveTokenSource?.Cancel();
            ReceiveTokenSource?.Dispose();
            ReceiveTokenSource = null;
        }

        public async Task SendRequestAsync(long receiverSessionId, List<FileModel> files, CancellationToken cancellationToken = default)
        {
            try
            {
                SendRequestTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                List<FileMetadata> filesMetadata = files
                    .Select(f => new FileMetadata(Path.GetFileName(f.Path), f.Size))
                    .ToList();

                SendRequest = new GlobalRequestModel(SessionId, filesMetadata);
                await _connection.InvokeAsync(ServerConstants.FileHub.SendRequest, receiverSessionId, SendRequest, cancellationToken);
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

        private void ResetSending()
        {
            IsSending = false;
            ReceiverId = 0;
            SendRequest = null;
            FilesToSend.Clear();

            SendRequestTokenSource?.Cancel();

            SendTokenSource?.Cancel();
            SendTokenSource?.Dispose();
            SendTokenSource = null;
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
            if (OnSendFilesRequest is not null)
            {
                accepted = await OnSendFilesRequest(request);
            }
            IsReceiving = accepted;
            ReceiveRequest = accepted ? request : null;
            await _connection.InvokeAsync(ServerConstants.FileHub.SendResponse, request.SenderSessionId, accepted);
        }

        private async Task OnReceiveResponse(bool accepted)
        {
            if (!IsSending)
            {
                return;
            }

            if (accepted && FilesToSend.Count > 0)
            {
                await SendFilesAsync(FilesToSend);
            }
            else
            {
                StopSending();
            }
        }

        private async Task SendFilesAsync(List<FileModel> files, CancellationToken cancellationToken = default)
        {
            try
            {
                SendTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                for (int i = 0; i < files.Count; i++)
                {
                    var fileMetadata = new FileMetadata(Path.GetFileName(files[i].Path), files[i].Size);
                    var fileId = Guid.NewGuid();
                    var isLast = i == (files.Count - 1);
                    await _connection.SendAsync(ServerConstants.FileHub.StartSendingFile, ReceiverId, fileMetadata, fileId, isLast);

                    // if last file send transfer finished event
                    Action? callback = isLast ? OnSendingFilesFinished : null;

                    IAsyncEnumerable<byte[]> fileStream = GenerateFileStream(files[i], callback, SendTokenSource.Token);
                    await _connection.SendAsync(ServerConstants.FileHub.SendFile, ReceiverId, fileStream, fileId, isLast);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while sending the request");
                StopSending();
            }
        }

        private void OnSendingFilesFinished()
        {
            ResetSending();
            SendingFinishedSuccessfully?.Invoke(this, EventArgs.Empty);
        }

        private async Task OnStartReceivingFile(FileMetadata fileMetadata, Guid fileId, bool isLast)
        {
            string filePath = FileHelper.GetUniqueFilePath(fileMetadata.Name, _storageService.SaveFolder);
            FileModel file = new(filePath, fileMetadata.Size)
            {
                Status = TransferStatus.InProgress
            };

            bool exceptionThrown = false;
            try
            {
                using FileStream fs = new(filePath, FileMode.Create, FileAccess.Write);
                ReceivingFileStarted?.Invoke(this, file);

                ReceiveTokenSource ??= new CancellationTokenSource();
                var fileStream = _connection.StreamAsync<byte[]>(ServerConstants.FileHub.ReceiveFile, fileId, ReceiveTokenSource.Token);

                await foreach (var chunk in fileStream)
                {
                    await fs.WriteAsync(chunk.AsMemory(0, chunk.Length));
                    file.CurrentProgress += chunk.Length;
                }

                file.Status = file.CurrentProgress == fileMetadata.Size
                    ? TransferStatus.Finished
                    : throw new TransferException($"The {fileMetadata.Name} file data was not received completely");
            }
            catch (Exception ex)
            {
                exceptionThrown = true;
                _logger.LogError(ex, "Error while receiving the file");
            }
            finally
            {
                if (exceptionThrown)
                {
                    file.Status = TransferStatus.Failed;
                    HandleFailedFile(filePath);
                    StopReceiving();
                }
                else if (isLast)
                {
                    ResetReceiving();
                    ReceivingFinishedSuccessfully?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private static async IAsyncEnumerable<byte[]> GenerateFileStream(FileModel file, Action? fileSentCallback, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            byte[] buffer;
            int bufferSize = FileHelper.GetBufferSizeByFileSize(file.Size);
            using FileStream fs = new(file.Path, FileMode.Open, FileAccess.Read);
            long size = fs.Length < bufferSize ? fs.Length : bufferSize;
            buffer = new byte[size];
            int bytesRead;
            while ((bytesRead = await fs.ReadAsync(buffer, CancellationToken.None)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                //if (cancellationToken.IsCancellationRequested)
                //{
                //    yield break;
                //}

                if (bytesRead < buffer.Length)
                {
                    Array.Resize(ref buffer, bytesRead);
                }
                yield return buffer;
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

        private static string GetConnectionUrl(IConfiguration configuration)
        {
            Uri baseUrl = new(configuration.GetValue<string>(Constants.Config.Server.BASE_URL_PATH) ?? "");
            string fileHubUrl = configuration.GetValue<string>(Constants.Config.Server.FILE_HUB_PATH) ?? "";
            return new Uri(baseUrl, fileHubUrl).AbsoluteUri;
        }

        private static void ValidateSessionId(long receiverSessionId)
        {
            if (Math.Log10(receiverSessionId) is < 10 or > 11)
            {
                throw new ArgumentException("Session ID must have 11 significant digits.", nameof(receiverSessionId));
            }
        }

        private void ListenConnection()
        {
            _connection.On<long>(ServerConstants.FileHub.HubConnected, OnConnected);

            _connection.On(ServerConstants.FileHub.ReceiverDisconnected, OnReceiverDisconnected);
            _connection.On(ServerConstants.FileHub.SenderDisconnected, OnSenderDisconnected);

            _connection.Closed += OnConnectionClosed;
        }

        private void ListenRequests()
        {
            _connection.On<GlobalRequestModel>(ServerConstants.FileHub.ReceiveRequest, OnReceiveRequest);

            _connection.On<bool>(ServerConstants.FileHub.ReceiveResponse, OnReceiveResponse);
        }

        private void ListenFiles()
        {
            _connection.On<FileMetadata, Guid, bool>(ServerConstants.FileHub.StartReceivingFile, OnStartReceivingFile);

            _connection.On(ServerConstants.FileHub.ReceivingCancelled, OnReceivingCancelled);
            _connection.On(ServerConstants.FileHub.SendingCancelled, OnSendingCancelled);
        }

        private void OnReceiverDisconnected()
        {
            StopSending();
            ReceiverDisconnected?.Invoke(this, EventArgs.Empty);
        }
        private void OnSenderDisconnected()
        {
            SenderDisconnected?.Invoke(this, EventArgs.Empty);
        }

        private void OnReceivingCancelled()
        {
            StopSending();
            ReceivingCancelled?.Invoke(this, EventArgs.Empty);
        }
        private void OnSendingCancelled()
        {
            SendingCancelled?.Invoke(this, EventArgs.Empty);
        }

        private void OnConnected(long sessionId)
        {
            SessionId = sessionId;
            Connected?.Invoke(this, SessionId);
        }

        public async ValueTask DisposeAsync()
        {
            ConnectTokenSource?.Dispose();
            SendRequestTokenSource?.Dispose();
            SendTokenSource?.Dispose();
            ReceiveTokenSource?.Dispose();

            try
            {
                await _connection.StopAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while stopping the connection");
            }
            finally
            {
                await _connection.DisposeAsync();
            }
        }
    }
}
