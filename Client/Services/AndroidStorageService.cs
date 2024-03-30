#if ANDROID
using Client.Interfaces;

namespace Client.Services
{
    public class AndroidStorageService : IStorageService
    {
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
