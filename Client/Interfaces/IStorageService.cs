namespace Client.Interfaces
{
    public interface IStorageService
    {
        public Task<List<string>> PickFilesAsync();
        public Task<string?> PickFolderAsync();
    }
}
