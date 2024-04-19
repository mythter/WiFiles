using Client.Interfaces;
using CommunityToolkit.Maui.Storage;

namespace Client.Services
{
    public class WindowsStorageService : IStorageService
    {
        public bool CheckIsDirectoryWritable(string dirPath, bool throwIfFails = false)
        {
            try
            {
                string fileName = Path.Combine(dirPath, Path.GetRandomFileName());
                using FileStream fs = File.Create(fileName, 1, FileOptions.DeleteOnClose);
                return true;
            }
            catch
            {
                if (throwIfFails)
                    throw;
                else
                    return false;
            }
        }

        public bool CheckIsFileReadable(string filePath, bool throwIfFails = false)
        {
            try
            {
                using FileStream fs = File.Open(filePath, FileMode.Open, FileAccess.Read);
                return true;
            }
            catch
            {
                if (throwIfFails)
                    throw;
                else
                    return false;
            }
        }

        public string? GetDefaultFolder()
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            if (Directory.Exists(path))
            {
                return path;
            }
            else
            {
                return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
        }

        public async Task<List<string>> PickFilesAsync()
        {
            var result = await FilePicker.PickMultipleAsync();
            return result
                .Select(f => f.FullPath)
                .ToList();
        }

        public async Task<string?> PickFolderAsync()
        {
            var result = await FolderPicker.PickAsync(default);
            return result?.Folder?.Path;
        }
    }
}
