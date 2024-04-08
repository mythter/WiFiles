using System.Net;

namespace Client.Models
{
    public class FileModel
    {
        public string Path { get; set; }
        public long Size { get; set; }
        public IPAddress? Sender { get; set; }
        public Progress<long>? Progress { get; set; }

        public FileModel(string path, long size, IPAddress? sender = null, Progress<long>? progress = null)
        {
            Path = path;
            Size = size;
            Sender = sender;
            Progress = progress;
        }
    }
}
