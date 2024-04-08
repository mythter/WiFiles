using Client.Models;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Client.Services
{
    //public delegate void StartReceivingFileEventHandler(FileModel file);

    public class LocalTransferService
    {
        public TcpListener TcpListener { get; set; }
        public TcpClient TcpClient { get; set; }

        public int PortListen { get; set; } = 8887;
        public int PortConnect { get; set; } = 8887;

        private CancellationTokenSource? ListenerTokenSource { get; set; }
        private CancellationTokenSource? ClientTokenSource { get; set; }

        public int BufferSize { get; set; } = 1024;

        public string? SaveFolder { get; set; }

        public bool IsListening { get; private set; }

        public event EventHandler<FileModel>? StartReceivingFile;
        public event EventHandler? ListeningStarted;
        public event EventHandler? ListeningStopped;

        public string ExceptionMessage { get; private set; }

        public LocalTransferService()
        {
            TcpListener = new TcpListener(IPAddress.Any, PortListen);
            TcpClient = new TcpClient();
        }

        public async Task StartSendingAsync(IPAddress ip, List<FileModel> files)
        {
            byte[] buffer;

            try
            {
                if (TcpClient.Connected)
                {
                    StopSending();
                }

                TcpClient.SendTimeout = 60_000;
                TcpClient.ReceiveTimeout = 60_000;
                ClientTokenSource = new CancellationTokenSource();
                await TcpClient.ConnectAsync(ip, PortConnect, ClientTokenSource.Token);
                ClientTokenSource = null;
                NetworkStream stream = TcpClient.GetStream();

                // Отправляем количество файлов
                int fileCount = files.Count;
                byte[] fileCountBytes = BitConverter.GetBytes(fileCount);
                await stream.WriteAsync(fileCountBytes.AsMemory(0, 4));

                foreach (FileModel file in files)
                {
                    // Отправляем имя файла
                    string fileName = Path.GetFileName(file.Path);
                    byte[] data = Encoding.UTF8.GetBytes(fileName + '\n');
                    await stream.WriteAsync(data);

                    // Отправляем размер файла
                    long fileSize = new FileInfo(file.Path).Length;
                    byte[] fileSizeBytes = BitConverter.GetBytes(fileSize);
                    await stream.WriteAsync(fileSizeBytes.AsMemory(0, 8));


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
                        await stream.WriteAsync(buffer);
                    }
                }
            }
            catch (SocketException ex)
            {
                ClientTokenSource = null;
                throw;
            }
            catch (OperationCanceledException ex)
            {
                
            }
            catch (Exception ex)
            {
                ClientTokenSource = null;
                throw;
            }
            finally
            {
                StopSending();
            }
        }

        public void StopSending()
        {
            ClientTokenSource?.Cancel();
            ClientTokenSource = null;
            TcpClient.Close();
            TcpClient = new TcpClient();
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
                    var tcpClient = await TcpListener.AcceptTcpClientAsync(ListenerTokenSource.Token);
                    Task.Run(async () => await ProcessClientAsync(tcpClient));
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException || ex is SocketException)
            {
                ListenerTokenSource = null;
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
            }

            ListenerTokenSource = null;
            TcpListener.Stop();
            ListeningStopped?.Invoke(this, EventArgs.Empty);
        }

        private async Task ProcessClientAsync(TcpClient tcpClient)
        {
            NetworkStream stream = tcpClient.GetStream();
            byte[] buffer;

            // Получаем количество файлов
            byte[] fileCountBytes = new byte[4];
            stream.Read(fileCountBytes, 0, 4);
            int fileCount = BitConverter.ToInt32(fileCountBytes, 0);

            for (int i = 0; i < fileCount; i++)
            {
                var fileNameList = new List<byte>();
                int bytesRead;
                // Получаем имя файла
                while ((bytesRead = stream.ReadByte()) != '\n')
                {
                    fileNameList.Add((byte)bytesRead);
                }
                string fileName = Encoding.UTF8.GetString(fileNameList.ToArray());

                // Получаем размер файла
                byte[] fileSizeBytes = new byte[8];
                stream.Read(fileSizeBytes, 0, 8);
                int fileSize = BitConverter.ToInt32(fileSizeBytes, 0);

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

#if ANDROID
                var activity = Platform.CurrentActivity ?? throw new NullReferenceException("Current activity is null");
                if (Android.OS.Build.VERSION.SdkInt < Android.OS.BuildVersionCodes.R)
                {
                    if (AndroidX.Core.Content.ContextCompat.CheckSelfPermission(activity, Android.Manifest.Permission.ReadExternalStorage) != Android.Content.PM.Permission.Granted)
                    {
                        AndroidX.Core.App.ActivityCompat.RequestPermissions(activity, new[] { Android.Manifest.Permission.ReadExternalStorage }, 1);
                    }
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

                try
                {
                    using (FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                    {
                        IProgress<long> progress = new Progress<long>();
                        FileModel file = new FileModel(filePath, fileSize)
                        {
                            Sender = (tcpClient.Client.RemoteEndPoint as IPEndPoint)?.Address,
                            Progress = (Progress<long>)progress,
                        };
                        StartReceivingFile?.Invoke(null, file);
                        long sentSize = 0;
                        object locker = new object();
                        while (sentSize < fileSize)
                        {
                            buffer = new byte[BufferSize < fileSize - sentSize ? BufferSize : fileSize - sentSize];
                            int size = await stream.ReadAsync(buffer);
                            if (size == 0)
                            {
                                // client has disconnected
                                break;
                            }
                            await fs.WriteAsync(buffer.AsMemory(0, size));
                            lock (locker)
                            {
                                sentSize += size;
                                progress.Report(sentSize);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }

                    throw;
                }
            }
        }
    }
}
