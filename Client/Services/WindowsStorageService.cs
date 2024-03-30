using Client.Interfaces;
using CommunityToolkit.Maui.Storage;

namespace Client.Services
{
    public class WindowsStorageService : IStorageService
    {
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
