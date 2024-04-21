using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Database;
using Android.OS;
using Android.OS.Storage;
using Android.Provider;
using AndroidX.Activity.Result;
using AndroidX.Activity.Result.Contract;
using static Android.Provider.MediaStore;
using Environment = Android.OS.Environment;
using Uri = Android.Net.Uri;

namespace Client
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        private static readonly int STORAGE_PERMISSION_CODE = 23;
        public static Func<Task<string?>> PickFolderAsync { get; set; } = null!;
        public static Func<Task<List<string>>> PickFilesAsync { get; set; } = null!;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            //if (Build.VERSION.SdkInt >= BuildVersionCodes.R && !Environment.IsExternalStorageManager)
            //{
            //    Intent intent = new Intent();
            //    intent.SetAction(Settings.ActionManageAppAllFilesAccessPermission);
            //    Uri uri = Uri.FromParts("package", PackageName, null);
            //    intent.SetData(uri);
            //    StartActivity(intent);
            //}

            base.OnCreate(savedInstanceState);

            PickFolderAsync = PreparePickFolder(this);
            PickFilesAsync = PreparePickFiles(this);
        }

        Func<Task<string?>> PreparePickFolder(MainActivity activity)
        {
            var intent = new Intent(Intent.ActionOpenDocumentTree);
            intent.PutExtra("android.content.extra.SHOW_ADVANCED", true);
            intent.PutExtra("android.content.extra.FANCY", true);
            intent.PutExtra("android.content.extra.SHOW_FILESIZE", true);

            intent.AddFlags(ActivityFlags.GrantPersistableUriPermission);
            intent.AddFlags(ActivityFlags.GrantPrefixUriPermission);

            TaskCompletionSource<string?>? taskCompletionSource = null;

            ActivityResultLauncher activityResultLauncher = activity.RegisterForActivityResult(
            new ActivityResultContracts.StartActivityForResult(),
            new ActivityResultCallback(result =>
            {
                string? path = null;
                if (result.ResultCode == (int)(Result.Ok))
                {
                    Intent? intent = result.Data;
                    var uri = intent?.Data;

                    var docUriTree = DocumentsContract.BuildDocumentUriUsingTree(uri, DocumentsContract.GetTreeDocumentId(uri));
                    
                    var context = Android.App.Application.Context;

                    if (docUriTree is not null)
                    {
                        path = GetRealPath(context, docUriTree);
                    }
                }
                taskCompletionSource?.SetResult(path);
            }));

            return () =>
            {
                if (!CheckStoragePermissions())
                {
                    RequestForStoragePermissions();
                }
                taskCompletionSource = new TaskCompletionSource<string?>();
                activityResultLauncher.Launch(intent);
                return taskCompletionSource.Task;
            };
        }

        Func<Task<List<string>>> PreparePickFiles(MainActivity activity)
        {
            var intent = new Intent(Intent.ActionOpenDocument);

            intent.SetType("*/*"); // Указываем, что мы хотим выбрать любой тип файла
            intent.PutExtra(Intent.ExtraAllowMultiple, true); // Разрешаем выбор нескольких файлов
            intent.AddCategory(Intent.CategoryOpenable);

            //intent.AddFlags(ActivityFlags.GrantPersistableUriPermission);
            //intent.AddFlags(ActivityFlags.GrantPrefixUriPermission);

            TaskCompletionSource<List<string>>? taskCompletionSource = null;

            ActivityResultLauncher activityResultLauncher = activity.RegisterForActivityResult(
            new ActivityResultContracts.StartActivityForResult(),
            new ActivityResultCallback(result =>
            {
                List<string> paths = new List<string>();
                if (result.ResultCode == (int)(Result.Ok))
                {
                    Intent? intent = result.Data;
                    var uris = intent?.ClipData;
                    var context = Android.App.Application.Context;

                    if (uris is null)
                    {
                        var uri = intent?.Data;
                        if (uri is not null)
                        {
                            var path = GetRealPath(context, uri);
                            if (path is not null)
                                paths.Add(path);
                        }
                    }
                    else
                    {
                        for (int i = 0; i < uris!.ItemCount; i++)
                        {
                            var uri = uris!.GetItemAt(i)?.Uri;
                            if (uri is not null)
                            {
                                var path = GetRealPath(context, uri);
                                if (path is not null)
                                    paths.Add(path);
                            }
                        }
                    }
                }

                taskCompletionSource?.SetResult(paths);
            }));

            return () =>
            {
                if (!CheckStoragePermissions())
                {
                    RequestForStoragePermissions();
                }
                taskCompletionSource = new TaskCompletionSource<List<string>>();
                activityResultLauncher.Launch(intent);
                return taskCompletionSource.Task;
            };
        }

        public bool CheckStoragePermissions()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
            {
                // Android 11 (R) and higher
                return Environment.IsExternalStorageManager;
            }
            else
            {
                // Below Android 11
                var write = ContextCompat.CheckSelfPermission(this, Android.Manifest.Permission.WriteExternalStorage);
                var read = ContextCompat.CheckSelfPermission(this, Android.Manifest.Permission.ReadExternalStorage);

                return read == Permission.Granted && write == Permission.Granted;
            }
        }

        private void RequestForStoragePermissions()
        {
            // Android 11 (R) and higher
            if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
            {
                try
                {
                    Intent intent = new Intent();
                    intent.SetAction(Settings.ActionManageAppAllFilesAccessPermission);
                    Uri uri = Uri.FromParts("package", PackageName, null);
                    intent.SetData(uri);
                    StartActivity(intent);
                }
                catch (Exception)
                {
                    Intent intent = new Intent();
                    intent.SetAction(Settings.ActionManageAllFilesAccessPermission);
                    StartActivity(intent);
                }
                }
            else
            {
                // Below Android 11
                ActivityCompat.RequestPermissions(
                    this,
                    new string[]
                    {
                        Android.Manifest.Permission.WriteExternalStorage,
                        Android.Manifest.Permission.ReadExternalStorage
                    },
                    STORAGE_PERMISSION_CODE
                );
            }
        }


        private static string GetRealPath(Context context, Uri fileUri)
        {
            string realPath;

            if (Build.VERSION.SdkInt < BuildVersionCodes.Honeycomb)
            {
                realPath = GetRealPathFromURI_BelowAPI11(context, fileUri);
            }
            else if (Build.VERSION.SdkInt < BuildVersionCodes.Kitkat)
            {
                realPath = GetRealPathFromURI_API11to18(context, fileUri);
            }
            else
            {
                realPath = GetRealPathFromURI_API19(context, fileUri);
            }

            return realPath;
        }

        private static string GetRealPathFromURI_API11to18(Context context, Uri contentUri)
        {
            string[] proj = { MediaStore.Images.ImageColumns.Data };
            string result = null;

            using (ICursor cursor = new CursorLoader(context, contentUri, proj, null, null, null).LoadInBackground() as ICursor)
            {
                if (cursor != null)
                {
                    int column_index = cursor.GetColumnIndexOrThrow(MediaStore.Images.ImageColumns.Data);
                    cursor.MoveToFirst();
                    result = cursor.GetString(column_index);
                }
            }

            return result;
        }

        private static string GetRealPathFromURI_BelowAPI11(Context context, Uri contentUri)
        {
            string[] proj = { MediaStore.Images.ImageColumns.Data };
            string result = "";

            using (ICursor cursor = context.ContentResolver.Query(contentUri, proj, null, null, null))
            {
                if (cursor != null)
                {
                    int column_index = cursor.GetColumnIndexOrThrow(MediaStore.Images.ImageColumns.Data);
                    cursor.MoveToFirst();
                    result = cursor.GetString(column_index);
                    cursor.Close();
                }
            }

            return result;
        }

        public static string? GetRealPathFromURI_API19(Context context, Uri uri)
        {
            bool isKitKat = Build.VERSION.SdkInt >= BuildVersionCodes.Kitkat;

            // DocumentProvider
            if (isKitKat && DocumentsContract.IsDocumentUri(context, uri))
            {
                // ExternalStorageProvider
                if (IsExternalStorageDocument(uri))
                {
                    string docId = DocumentsContract.GetDocumentId(uri);
                    string[] split = docId.Split(':', StringSplitOptions.RemoveEmptyEntries);
                    string type = split[0];

                    if ("primary".Equals(type, StringComparison.OrdinalIgnoreCase))
                    {
                        return $"{Environment.ExternalStorageDirectory}{(split.Length > 1 ? $"/{split[1]}" : "")}";
                    }
                    else if ("home".Equals(type, StringComparison.OrdinalIgnoreCase))
                    {
                        // Documents folder's type is home
                        return Environment.ExternalStorageDirectory + "/Documents" + (split.Length > 1 ? $"/{split[1]}" : "");
                    }
                    else
                    {
                        // Обрабатываем другие виды томов, например, вторичные тома (SD-карты) или облачные хранилища
                        StorageManager storageManager = (StorageManager)context.GetSystemService(StorageService);
                        IList<StorageVolume> storageVolumes = storageManager.StorageVolumes;
                        var paths = (string[]?)storageManager.Class
                            .GetMethod("getVolumePaths")
                            .Invoke(storageManager);
                        var volume = storageVolumes.FirstOrDefault(v => v.IsRemovable && v.State == Environment.MediaMounted);
                        return $"{paths?.SingleOrDefault(p => p.EndsWith(volume.Uuid))}{(split.Length > 1 ? $"/{split[1]}" : "")}";
                    }

                    // TODO: Обработка других типов томов
                }
                // DownloadsProvider
                else if (IsDownloadsDocument(uri))
                {
                    string? fileName = GetFilePath(context, uri);

                    string? id = DocumentsContract.GetDocumentId(uri);
                    string? path = null;

                    if (string.IsNullOrEmpty(id))
                        return null;

                    if ("downloads".Equals(id, StringComparison.OrdinalIgnoreCase))
                    {
                        return $"{Environment.GetExternalStoragePublicDirectory(Environment.DirectoryDownloads)}";
                    }
                    else if (id.StartsWith("raw:"))
                    {
                        return id.Substring("raw:".Length);
                    }
                    else if (id.StartsWith("msf:"))
                    {
                        // like msf:7755
                        if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
                        {
                            string[] split = id.Split(':');
                            string selection = "_id=?";
                            string[] selectionArgs = { split[1] };
                            return GetDataColumn(context, Downloads.ExternalContentUri, selection, selectionArgs);
                        }
                        else
                        {
                        id = id.Substring("msf:".Length);
                    }
                    }
                    try
                    {
                        var contentUri = ContentUris.WithAppendedId(
                            Uri.Parse("content://downloads/public_downloads")!,
                            long.Parse(id));

                        path = GetDataColumn(context, contentUri, null, null);
                    }
                    catch (Exception ex)
                    {
                        if (fileName is not null)
                        {
                            string? downloadsPath = Environment.GetExternalStoragePublicDirectory(Environment.DirectoryDownloads)?.AbsolutePath;
                            path = downloadsPath is null ? null : Path.Combine(downloadsPath, fileName);
                        }
                    }

                    return path;
                }
                // MediaProvider
                else if (IsMediaDocument(uri))
                {
                    string docId = DocumentsContract.GetDocumentId(uri);
                    string[] split = docId.Split(':');
                    string type = split[0];

                    Uri contentUri = null;
                    if ("image".Equals(type, StringComparison.OrdinalIgnoreCase))
                    {
                        contentUri = Images.Media.ExternalContentUri;
                    }
                    else if ("video".Equals(type, StringComparison.OrdinalIgnoreCase))
                    {
                        contentUri = Video.Media.ExternalContentUri;
                    }
                    else if ("audio".Equals(type, StringComparison.OrdinalIgnoreCase))
                    {
                        contentUri = Audio.Media.ExternalContentUri;
                    }
                    else
                    {
                        contentUri = Files.GetContentUri("external");
                    }

                    string selection = "_id=?";
                    string[] selectionArgs = { split[1] };

                    return GetDataColumn(context, contentUri, selection, selectionArgs);
                }
            }
            // MediaStore (и общее)
            else if ("content".Equals(uri.Scheme, StringComparison.OrdinalIgnoreCase))
            {
                // Возвращаем удаленный адрес
                if (IsGooglePhotosUri(uri))
                    return uri.LastPathSegment;

                return GetDataColumn(context, uri, null, null);
            }
            // File
            else if ("file".Equals(uri.Scheme, StringComparison.OrdinalIgnoreCase))
            {
                return uri.Path;
            }

            return null;
        }

        private static string? GetDataColumn(Context context, Uri uri, string? selection, string[]? selectionArgs)
        {
            string column = "_data";
            string[] projection = { column };

            using (ICursor? cursor = context?.ContentResolver?.Query(uri, projection, selection, selectionArgs, null))
            {
                if (cursor != null && cursor.MoveToFirst())
                {
                    int index = cursor.GetColumnIndexOrThrow(column);
                    return cursor.GetString(index);
                }
            }

            return null;
        }

        public static string? GetFilePath(Context context, Uri uri)
        {
            ICursor? cursor = null;
            string[] projection = { IMediaColumns.DisplayName };

            try
            {
                cursor = context?.ContentResolver?.Query(uri, projection, null, null, null);
                if (cursor != null && cursor.MoveToFirst())
                {
                    int index = cursor.GetColumnIndexOrThrow(IMediaColumns.DisplayName);
                    return cursor.GetString(index);
                }
            }
            finally
            {
                cursor?.Close();
            }
            return null;
        }

        private static bool IsExternalStorageDocument(Uri uri)
        {
            return "com.android.externalstorage.documents".Equals(uri.Authority);
        }

        private static bool IsDownloadsDocument(Uri uri)
        {
            return "com.android.providers.downloads.documents".Equals(uri.Authority);
        }

        private static bool IsMediaDocument(Uri uri)
        {
            return "com.android.providers.media.documents".Equals(uri.Authority);
        }

        private static bool IsGooglePhotosUri(Uri uri)
        {
            return "com.google.android.apps.photos.content".Equals(uri.Authority);
        }
    }

    public class ActivityResultCallback : Java.Lang.Object, IActivityResultCallback
    {
        readonly Action<ActivityResult> _callback;
        public ActivityResultCallback(Action<ActivityResult> callback) => _callback = callback;
        public ActivityResultCallback(TaskCompletionSource<ActivityResult> tcs) => _callback = tcs.SetResult;
        public void OnActivityResult(Java.Lang.Object p0) => _callback((ActivityResult)p0);
    }
}
