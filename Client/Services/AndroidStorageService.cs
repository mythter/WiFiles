#if ANDROID
using Client.Interfaces;

namespace Client.Services
{
    public class AndroidStorageService : IStorageService
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
            string? extStorDir = Android.OS.Environment.ExternalStorageDirectory?.AbsolutePath;
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
