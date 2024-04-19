namespace Client.Interfaces
{
    public interface IStorageService
    {
        public Task<List<string>> PickFilesAsync();
        public Task<string?> PickFolderAsync();
        public string? GetDefaultFolder();
        public bool CheckIsDirectoryWritable(string dirPath, bool throwIfFails = false);
        public bool CheckIsFileReadable(string filePath, bool throwIfFails = false);
    }
}
