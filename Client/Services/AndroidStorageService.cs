#if ANDROID
using Android.Content;
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
            //try
            //{
            //    var intent = new Intent(Intent.ActionOpenDocumentTree);
            //    intent.AddFlags(ActivityFlags.GrantPersistableUriPermission);
            //    intent.AddFlags(ActivityFlags.GrantPrefixUriPermission);

            //    var activity = Platform.CurrentActivity;
            //    if (activity != null)
            //    {
            //        var result = activity.StartActivityForResult(intent, 0);

            //        if (result?.Data != null)
            //        {
            //            var uri = result.Data.Data;
            //            var folderPath = GetFolderPathFromUri(uri);
            //            return folderPath;
            //        }
            //    }
            //    return null;
            //}
            //catch (Exception ex)
            //{
            //    // Обработка ошибок
            //    Console.WriteLine($"Ошибка выбора папки: {ex.Message}");
            //    return null;
            //}

            var tcs = new TaskCompletionSource<string>();

            try
            {
                //ActivityCompat.RequestPermissions(Platform.CurrentActivity, new[] { Manifest.Permission.ReadExternalStorage }, 100);

                var intent = new Intent(Intent.ActionOpenDocumentTree);
                intent.PutExtra("android.content.extra.SHOW_ADVANCED", true);
                intent.PutExtra("android.content.extra.FANCY", true);
                intent.PutExtra("android.content.extra.SHOW_FILESIZE", true);

                intent.AddFlags(ActivityFlags.GrantPersistableUriPermission);
                intent.AddFlags(ActivityFlags.GrantPrefixUriPermission);

                var activity = Platform.CurrentActivity;
                if (activity != null)
                {
                    //activity.ActivityResult += (sender, e) =>
                    //{
                    //    if (e?.Data?.Data != null)
                    //    {
                    //        var uri = e.Data.Data;
                    //        var folderPath = GetFolderPathFromUri(uri);
                    //        tcs.TrySetResult(folderPath);
                    //    }
                    //    else
                    //    {
                    //        tcs.TrySetResult(null);
                    //    }
                    //};

                    activity.StartActivityForResult(intent, 0);
                }
            }
            catch (Exception ex)
            {
                // Обработка ошибок
                Console.WriteLine($"Ошибка выбора папки: {ex.Message}");
                tcs.TrySetException(ex);
            }

            return await tcs.Task;
        }
    }
}
#endif
