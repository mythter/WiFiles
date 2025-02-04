namespace Domain.Models
{
    public class FileMetadata
    {
        public string Name { get; set; }

        public long Size { get; set; }

        public FileMetadata(string name, long size)
        {
            Name = name;
            Size = size;
        }
    }
}
