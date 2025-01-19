#if ANDROID
using Client.Interfaces;
using Client.Models;

namespace Client.Services
{
    public class AndroidStorageService : IStorageService
    {
        public string SaveFolder { get; private set; }

        public SynchronizedCollection<FileModel> SendFiles { get; } = new();

        public SynchronizedCollection<FileModel> ReceiveFiles { get; } = new();

        public AndroidStorageService()
        {
            SaveFolder = GetDefaultFolder();
        }

        public bool CheckIfDirectoryWritable(string dirPath, bool throwIfFails = false)
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

        public bool CheckIfFileReadable(string filePath, bool throwIfFails = false)
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

        public string GetDefaultFolder()
        {
            string extStorDir = Android.OS.Environment.ExternalStorageDirectory?.AbsolutePath
                ?? throw new NotSupportedException("Unable to get ExternalStorageDirectory absolute path");
            Java.IO.File path = new Java.IO.File(extStorDir + "/Download");
            if (path.Exists())
            {
                return path.AbsolutePath;
            }
            else
            {
                return extStorDir;
            }
        }

        public bool TrySetSaveFolder(string path)
        {
            Java.IO.File folder = new Java.IO.File(path);
            if (folder.Exists())
            {
                SaveFolder = path;
                return true;
            }

            return false;
        }

        public async Task<List<string>> PickFilesAsync()
        {
            return await MainActivity.PickFilesAsync();
        }

        public async Task<string?> PickFolderAsync()
        {
            return await MainActivity.PickFolderAsync();
        }
    }
}
#endif
