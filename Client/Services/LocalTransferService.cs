﻿using Client.Models;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Client.Extensions;

namespace Client.Services
{
    //public delegate void StartReceivingFileEventHandler(FileModel file);

    public class LocalTransferService
    {
        private TcpListener TcpListener { get; set; }
        private TcpClient TcpClient { get; set; }

        public int PortListen { get; set; } = 8887;
        public int PortConnect { get; set; } = 8887;

        public bool IsListening { get; private set; }
        public bool IsReceiving { get; private set; }

        public int BufferSize { get; set; } = 1024;

        public int ReceiveTimeout { get; set; } = 15_000;
        public int SendTimeout { get; set; } = 15_000;

        public string? SaveFolder { get; set; }

        public IPAddress? ReceiverIp { get; private set; }

        public event EventHandler<FileModel>? ReceivingFileStarted;
        public event EventHandler<string>? ReceivingFileFailed;
        public event EventHandler<string>? ExceptionHandled;
        public event EventHandler? ListeningStarted;
        public event EventHandler? ListeningStopped;

        public Func<IPAddress?, int, Task<bool>>? OnSendFilesRequest { get; set; }

        private CancellationTokenSource? ListenerTokenSource { get; set; }
        private CancellationTokenSource? ClientTokenSource { get; set; }
        private CancellationTokenSource? ReceivingTokenSource { get; set; }

        public string ExceptionMessage { get; private set; }

        public LocalTransferService()
        {
            TcpListener = new TcpListener(IPAddress.Any, PortListen);
            TcpClient = new TcpClient();
        }

        public async Task StartSendingAsync(IPAddress ip, List<FileModel> files)
        {
            if (files is null || files.Count == 0)
                return;

            ReceiverIp = ip;

            try
            {
                if (TcpClient.Connected)
                {
                    StopSending();
                }

                //TcpClient.SendTimeout = 60_000;
                //TcpClient.ReceiveTimeout = 60_000;
                ClientTokenSource = new CancellationTokenSource();
                await TcpClient.ConnectAsync(ip, PortConnect, ClientTokenSource.Token);
                NetworkStream stream = TcpClient.GetStream();

                await SendFilesCount(files, stream, ClientTokenSource.Token);

                // setting timeout for synchronous reading
                // sync reading is needed to read response text byte-by-byte
                TcpClient.ReceiveTimeout = 30_000;

                // waiting for the response from receiver
                string? response = await ReceiveLineAsync(stream, ClientTokenSource.Token);

                if (response == "Accepted")
                {
#if ANDROID
                    RequestReadAccess();
#endif
                    await SendFiles(files, stream, ClientTokenSource.Token);
                }
            }
            catch (SocketException ex)
            {
                ExceptionHandled?.Invoke(this, ex.Message);
            }
            catch (OperationCanceledException ex)
            {
            }
            catch (Exception ex)
            {
                ExceptionHandled?.Invoke(this, ex.Message);
            }
            finally
            {
                ClientTokenSource = null;
                ReceiverIp = null;
                TcpClient.Close();
                TcpClient = new TcpClient();
                //StopSending();
            }
        }

        public void StopSending()
        {
            ClientTokenSource?.Cancel();
            ClientTokenSource = null;
            ReceiverIp = null;
            //TcpClient.Close();
            //TcpClient = new TcpClient();
        }

        private async Task SendFilesCount(List<FileModel> files, NetworkStream stream, CancellationToken cancellationToken)
        {
            int fileCount = files.Count;
            byte[] fileCountBytes = BitConverter.GetBytes(fileCount);
            await stream.WriteWithTimeoutAsync(fileCountBytes, SendTimeout, cancellationToken);
        }

        private async Task SendFiles(List<FileModel> files, NetworkStream stream, CancellationToken cancellationToken)
        {
            byte[] buffer;
            //try
            //{
            foreach (FileModel file in files)
            {
                await SendFileName(file, stream, cancellationToken);

                await SendFileSize(file, stream, cancellationToken);

                using FileStream fs = new FileStream(file.Path, FileMode.Open, FileAccess.Read);
                long size = fs.Length < BufferSize ? fs.Length : BufferSize;
                buffer = new byte[size];
                int bytesRead;
                while ((bytesRead = await fs.ReadAsync(buffer)) > 0)
                {
                    if (bytesRead < buffer.Length)
                    {
                        Array.Resize(ref buffer, bytesRead);
                    }
                    //await stream.WriteAsync(buffer, cancellationToken);
                    await stream.WriteWithTimeoutAsync(buffer, SendTimeout, cancellationToken);
                }
            }
            //}
            //catch (Exception ex) when (ex is IOException || ex is ObjectDisposedException)
            //{
            //    throw new OperationCanceledException(ex.Message, ex);
            //}
        }

        private async Task SendFileName(FileModel file, NetworkStream stream, CancellationToken cancellationToken)
        {
            string fileName = Path.GetFileName(file.Path);
            byte[] data = Encoding.UTF8.GetBytes(fileName + '\n');
            await stream.WriteWithTimeoutAsync(data, SendTimeout, cancellationToken);
        }

        private async Task SendFileSize(FileModel file, NetworkStream stream, CancellationToken cancellationToken)
        {
            long fileSize = new FileInfo(file.Path).Length;
            byte[] fileSizeBytes = BitConverter.GetBytes(fileSize);
            await stream.WriteWithTimeoutAsync(fileSizeBytes, SendTimeout, cancellationToken);
        }

        public async Task StartListeningAsync()
        {
            try
            {
                IsListening = true;
                TcpListener.Start();
                ListeningStarted?.Invoke(this, EventArgs.Empty);

                while (true)
                {
                    ListenerTokenSource = new CancellationTokenSource();
                    TcpClient tcpClient = await TcpListener.AcceptTcpClientAsync(ListenerTokenSource.Token);
                    ListenerTokenSource = null;
                    Task.Run(async () => await ProcessClientAsync(tcpClient));
                }
            }
            catch (SocketException ex)
            {
                ExceptionHandled?.Invoke(this, ex.Message);

            }
            catch (OperationCanceledException ex)
            {
            }
            catch (Exception ex)
            {
                ExceptionHandled?.Invoke(this, ex.Message);
            }
            finally
            {
                await StopListeningAsync();
            }
        }

        public async Task StopListeningAsync()
        {
            IsListening = false;
            if (ListenerTokenSource is not null)
            {
                await ListenerTokenSource.CancelAsync();
                ListenerTokenSource = null;
            }

            TcpListener.Stop();
            ListeningStopped?.Invoke(this, EventArgs.Empty);
        }

        public void StopReceiving()
        {
            ReceivingTokenSource?.Cancel();
            ReceivingTokenSource = null;
            IsReceiving = false;
        }

        private async Task ProcessClientAsync(TcpClient tcpClient)
        {
            NetworkStream stream = tcpClient.GetStream();

            int fileCount = await ReceiveFileCountAsync(stream);

            string response = await GetUserResponseAsync(tcpClient, fileCount);

            byte[] data = Encoding.UTF8.GetBytes(response + '\n');
            await stream.WriteAsync(data);

            if (response == "Declined")
            {
                tcpClient.Close();
                return;
            }

#if ANDROID
            RequestWriteAccess();
#endif
            IsReceiving = true;
            ReceivingTokenSource = new CancellationTokenSource();
            // setting timeout for synchronous reading
            // sync reading is needed to read file name byte-by-byte
            tcpClient.ReceiveTimeout = ReceiveTimeout;
            try
            {
            for (int i = 0; i < fileCount; i++)
            {
                string? fileName = await ReceiveLineAsync(stream, ReceivingTokenSource.Token);

                long fileSize = await ReceiveFileSizeAsync(stream, ReceivingTokenSource.Token);

                string filePath = GetUniqueFilePath(fileName);

                try
                {
                    await ReceiveFilesAsync(filePath, fileSize, tcpClient, ReceivingTokenSource.Token);
                }
                catch (OperationCanceledException ex)
                {
                    DeleteFileIfExists(filePath);

                    if (ReceivingTokenSource is not null)
                    {
                        try
                        {
                            tcpClient.Client.Shutdown(SocketShutdown.Both);
                        }
                        catch (Exception e)
                        {

                            throw;
                        }
                        ExceptionHandled?.Invoke(this, ex.Message);

                    }
                }
                catch (Exception ex)
                {
                    DeleteFileIfExists(filePath);
                    ExceptionHandled?.Invoke(this, ex.Message);
                }
                finally
                {
                    ReceivingTokenSource = null;
                    IsReceiving = false;
                    tcpClient.Close();
                }
            }
        }

        private async Task ReceiveFilesAsync(string filePath, long fileSize, TcpClient tcpClient, CancellationToken cancellationToken)
        {
            NetworkStream stream = tcpClient.GetStream();

            using (FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                IProgress<long> progress = new Progress<long>();
                FileModel file = new FileModel(filePath, fileSize)
                {
                    Sender = (tcpClient.Client.RemoteEndPoint as IPEndPoint)?.Address,
                    Progress = (Progress<long>)progress,
                };
                ReceivingFileStarted?.Invoke(this, file);

                long sentSize = 0;
                byte[] buffer;
                int timeout = 5000;
                while (sentSize < fileSize)
                {
                    buffer = new byte[BufferSize < fileSize - sentSize ? BufferSize : fileSize - sentSize];
                    int size = await stream.ReadWithTimeoutAsync(buffer, ReceiveTimeout, cancellationToken);
                    //int size = await stream.ReadAsync(buffer, cancellationToken);
                    if (size == 0)
                    {
                        throw new OperationCanceledException("Sender canceled the operation or was disconnected");
                    }
                    await fs.WriteAsync(buffer.AsMemory(0, size));
                    sentSize += size;
                    progress.Report(sentSize);
                }
            }
        }

        private Task<string> ReceiveLineAsync(NetworkStream stream, CancellationToken cancellationToken = default)
        {
            //using StreamReader streamReader = new StreamReader(stream, leaveOpen: true);
            ////string? line = await streamReader.ReadLineAsync(cancellationToken);
            //string? line = await streamReader.ReadLineWithTimeoutAsync(timeout, cancellationToken);
            //return line;
            return Task.Run(() =>
            {
                var fileNameList = new List<byte>();
                int bytesRead;
                while ((bytesRead = stream.ReadByte()) != '\n' && bytesRead != -1)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    fileNameList.Add((byte)bytesRead);
                }
                return Encoding.UTF8.GetString(fileNameList.ToArray());
            });
        }

        private async Task<long> ReceiveFileSizeAsync(NetworkStream stream, CancellationToken cancellationToken)
        {
            byte[] fileSizeBytes = new byte[8];
            //await stream.ReadAsync(fileSizeBytes.AsMemory(0, 8), cancellationToken);
            await stream.ReadWithTimeoutAsync(fileSizeBytes, ReceiveTimeout, cancellationToken);
            return BitConverter.ToInt32(fileSizeBytes, 0);
        }

        private async Task<int> ReceiveFileCountAsync(NetworkStream stream)
        {
            byte[] fileCountBytes = new byte[4];
            await stream.ReadAsync(fileCountBytes.AsMemory(0, 4));
            return BitConverter.ToInt32(fileCountBytes, 0);
        }

        private string GetUniqueFilePath(string fileName)
        {
            string extension = Path.GetExtension(fileName);
            string tempName = Path.GetFileNameWithoutExtension(fileName);
            string filePath = Path.Combine(SaveFolder, fileName);
            int n = 1;
            while (File.Exists(filePath))
            {
                fileName = $"{tempName} ({n}){extension}";
                filePath = Path.Combine(SaveFolder, fileName);
                n++;
            }

            return filePath;
        }

        private async Task<string> GetUserResponseAsync(TcpClient tcpClient, int fileCount)
        {
            IPAddress? sender = (tcpClient.Client.RemoteEndPoint as IPEndPoint)?.Address;

            string response;
            if (OnSendFilesRequest is null)
            {
                response = "Declined";
            }
            else
            {
                bool isAccepted = await OnSendFilesRequest.Invoke(sender, fileCount);
                response = isAccepted ? "Accepted" : "Declined";
            }

            return response;
        }

        private void DeleteFileIfExists(string filePath)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                ReceivingFileFailed?.Invoke(this, filePath);
            }
        }

        private void RequestReadAccess()
        {
#if ANDROID
            var activity = Platform.CurrentActivity ?? throw new NullReferenceException("Current activity is null");
            if (Android.OS.Build.VERSION.SdkInt < Android.OS.BuildVersionCodes.R)
            {
                if (AndroidX.Core.Content.ContextCompat.CheckSelfPermission(activity, Android.Manifest.Permission.ReadExternalStorage) != Android.Content.PM.Permission.Granted)
                {
                    AndroidX.Core.App.ActivityCompat.RequestPermissions(activity, new[] { Android.Manifest.Permission.ReadExternalStorage }, 1);
                }
            }
            else
            {
                if (AndroidX.Core.Content.ContextCompat.CheckSelfPermission(activity, Android.Manifest.Permission.ManageExternalStorage) != Android.Content.PM.Permission.Granted)
                {
                    AndroidX.Core.App.ActivityCompat.RequestPermissions(activity, new[] { Android.Manifest.Permission.ManageExternalStorage }, 3);
                }
            }
#endif
        }

        private void RequestWriteAccess()
        {
#if ANDROID
            var activity = Platform.CurrentActivity ?? throw new NullReferenceException("Current activity is null");
            if (Android.OS.Build.VERSION.SdkInt < Android.OS.BuildVersionCodes.R)
            {
                if (AndroidX.Core.Content.ContextCompat.CheckSelfPermission(activity, Android.Manifest.Permission.WriteExternalStorage) != Android.Content.PM.Permission.Granted)
                {
                    AndroidX.Core.App.ActivityCompat.RequestPermissions(activity, new[] { Android.Manifest.Permission.WriteExternalStorage }, 2);
                }
            }
            else
            {
                if (AndroidX.Core.Content.ContextCompat.CheckSelfPermission(activity, Android.Manifest.Permission.ManageExternalStorage) != Android.Content.PM.Permission.Granted)
                {
                    AndroidX.Core.App.ActivityCompat.RequestPermissions(activity, new[] { Android.Manifest.Permission.ManageExternalStorage }, 3);
                }
            }
#endif
        }
    }
}
