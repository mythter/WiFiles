using Domain.Models;

namespace Client.Interfaces
{
    public interface IStorageService
    {
        public string SaveFolder { get; }
        public SynchronizedCollection<FileModel> SendFiles { get; }
        public SynchronizedCollection<FileModel> ReceiveFiles { get; }

        public Task<List<string>> PickFilesAsync();
        public Task<string?> PickFolderAsync();
        public string GetDefaultFolder();
        public bool TrySetSaveFolder(string path);
        public bool CheckIfDirectoryWritable(string dirPath, bool throwIfFails = false);
        public bool CheckIfFileReadable(string filePath, bool throwIfFails = false);
    }
}
