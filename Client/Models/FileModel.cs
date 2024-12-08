using System.Net;
using Client.Enums;

namespace Client.Models
{
    public class FileModel
    {
        public string Path { get; set; }
        public long Size { get; set; }
        public IPAddress? Sender { get; set; }
        public TransferStatus Status { get; set; }

        private long currentProgress;
        public long CurrentProgress
        {
            get => currentProgress;
            set
            {
                currentProgress = value;
                ProgressChanged?.Invoke(this, currentProgress);
            }
        }

        public event EventHandler<long>? ProgressChanged;

        public FileModel(string path, long size, IPAddress? sender = null)
        {
            Path = path;
            Size = size;
            Sender = sender;
        }
    }
}
