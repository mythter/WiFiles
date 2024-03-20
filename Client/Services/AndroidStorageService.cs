#if ANDROID
using Client.Interfaces;

namespace Client.Services
{
    public class AndroidStorageService : IStorageService
    {
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
