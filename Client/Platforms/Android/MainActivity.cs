using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Database;
using Android.OS;
using Android.Provider;
using AndroidX.Activity.Result;
using AndroidX.Activity.Result.Contract;

namespace Client
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        public static Func<Task<string?>> PickFolderAsync { get; set; } = null!;
        public static Func<Task<List<string>>> PickFilesAsync { get; set; } = null!;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            PickFolderAsync = PreparePickFolder(this);
            PickFilesAsync = PreparePickFiles(this);
        }

        static Func<Task<string?>> PreparePickFolder(MainActivity activity)
        {
            var intent = new Intent(Intent.ActionOpenDocumentTree);
            intent.PutExtra("android.content.extra.SHOW_ADVANCED", true);
            intent.PutExtra("android.content.extra.FANCY", true);
            intent.PutExtra("android.content.extra.SHOW_FILESIZE", true);

            intent.AddFlags(ActivityFlags.GrantPersistableUriPermission);
            intent.AddFlags(ActivityFlags.GrantPrefixUriPermission);

            TaskCompletionSource<string?> taskCompletionSource = new TaskCompletionSource<string?>();

            ActivityResultLauncher activityResultLauncher = activity.RegisterForActivityResult(
            new ActivityResultContracts.StartActivityForResult(),
            new ActivityResultCallback(result =>
            {
                if (result.ResultCode == (int)(Result.Ok))
                {
                    Intent intent = result.Data;
                    var uri = intent.Data;

                    var docUriTree = DocumentsContract.BuildDocumentUriUsingTree(uri, DocumentsContract.GetTreeDocumentId(uri));

                    var context = Android.App.Application.Context;
                    string path = GetRealPath(context, docUriTree);
                    taskCompletionSource?.SetResult(path);
                }
                else
                {
                    taskCompletionSource?.SetResult(null);
                }
            }));

            return () =>
            {
                activityResultLauncher.Launch(intent);
                return taskCompletionSource.Task;
            };
        }

        static Func<Task<List<string>>> PreparePickFiles(MainActivity activity)
        {
            var intent = new Intent(Intent.ActionOpenDocument);

            intent.SetType("*/*"); // Указываем, что мы хотим выбрать любой тип файла
            intent.PutExtra(Intent.ExtraAllowMultiple, true); // Разрешаем выбор нескольких файлов
            intent.AddCategory(Intent.CategoryOpenable);

            intent.AddFlags(ActivityFlags.GrantPersistableUriPermission);
            intent.AddFlags(ActivityFlags.GrantPrefixUriPermission);

            TaskCompletionSource<List<string>> taskCompletionSource = new TaskCompletionSource<List<string>>();

            ActivityResultLauncher activityResultLauncher = activity.RegisterForActivityResult(
            new ActivityResultContracts.StartActivityForResult(),
            new ActivityResultCallback(result =>
            {
                List<string> paths = new List<string>();
                if (result.ResultCode == (int)(Result.Ok))
                {
                    Intent intent = result.Data;
                    var uris = intent.ClipData;

                    var context = Android.App.Application.Context;
                    for (int i = 0; i < uris.ItemCount; i++)
                    {
                        var uri = uris.GetItemAt(i).Uri;
                        var path = GetRealPath(context, uri);
                        paths.Add(path);
                    }

                    taskCompletionSource?.SetResult(paths);
                }
                else
                {
                    taskCompletionSource?.SetResult(paths);
                }
            }));

            return () =>
            {
                activityResultLauncher.Launch(intent);
                return taskCompletionSource.Task;
            };
        }

        private static string GetRealPath(Context context, Android.Net.Uri fileUri)
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

        private static string GetRealPathFromURI_API11to18(Context context, Android.Net.Uri contentUri)
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

        private static string GetRealPathFromURI_BelowAPI11(Context context, Android.Net.Uri contentUri)
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

        public static string GetRealPathFromURI_API19(Context context, Android.Net.Uri uri)
        {
            bool isKitKat = Build.VERSION.SdkInt >= BuildVersionCodes.Kitkat;

            // DocumentProvider
            if (isKitKat && DocumentsContract.IsDocumentUri(context, uri))
            {
                // ExternalStorageProvider
                if (IsExternalStorageDocument(uri))
                {
                    string docId = DocumentsContract.GetDocumentId(uri);
                    string[] split = docId.Split(':');
                    string type = split[0];

                    if ("primary".Equals(type, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return $"{Android.OS.Environment.ExternalStorageDirectory}/{split[1]}";
                    }

                    // TODO: Обработка других типов томов
                }
                // DownloadsProvider
                else if (IsDownloadsDocument(uri))
                {
                    string id = DocumentsContract.GetDocumentId(uri);

                    if (string.IsNullOrEmpty(id))
                        return null;

                    if ("downloads".Equals(id, System.StringComparison.OrdinalIgnoreCase))
                    {
                        return $"{Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads)}";
                    }
                    else if (id.StartsWith("raw:"))
                    {
                        return id.Substring("raw:".Length);
                    }

                    var contentUri = ContentUris.WithAppendedId(
                        Android.Net.Uri.Parse("content://downloads/public_downloads"),
                        long.Parse(id));
                    return GetDataColumn(context, contentUri, null, null);
                }
                // MediaProvider
                else if (IsMediaDocument(uri))
                {
                    string docId = DocumentsContract.GetDocumentId(uri);
                    string[] split = docId.Split(':');
                    string type = split[0];

                    Android.Net.Uri contentUri = null;
                    if ("image".Equals(type, System.StringComparison.OrdinalIgnoreCase))
                    {
                        contentUri = MediaStore.Images.Media.ExternalContentUri;
                    }
                    else if ("video".Equals(type, System.StringComparison.OrdinalIgnoreCase))
                    {
                        contentUri = MediaStore.Video.Media.ExternalContentUri;
                    }
                    else if ("audio".Equals(type, System.StringComparison.OrdinalIgnoreCase))
                    {
                        contentUri = MediaStore.Audio.Media.ExternalContentUri;
                    }

                    string selection = "_id=?";
                    string[] selectionArgs = { split[1] };

                    return GetDataColumn(context, contentUri, selection, selectionArgs);
                }
            }
            // MediaStore (и общее)
            else if ("content".Equals(uri.Scheme, System.StringComparison.OrdinalIgnoreCase))
            {
                // Возвращаем удаленный адрес
                if (IsGooglePhotosUri(uri))
                    return uri.LastPathSegment;

                return GetDataColumn(context, uri, null, null);
            }
            // File
            else if ("file".Equals(uri.Scheme, System.StringComparison.OrdinalIgnoreCase))
            {
                return uri.Path;
            }

            return null;
        }

        private static string GetDataColumn(Context context, Android.Net.Uri uri, string selection, string[] selectionArgs)
        {
            string column = "_data";
            string[] projection = { column };

            using (ICursor cursor = context.ContentResolver.Query(uri, projection, selection, selectionArgs, null))
            {
                if (cursor != null && cursor.MoveToFirst())
                {
                    int index = cursor.GetColumnIndexOrThrow(column);
                    return cursor.GetString(index);
                }
            }

            return null;
        }

        private static bool IsExternalStorageDocument(Android.Net.Uri uri)
        {
            return "com.android.externalstorage.documents".Equals(uri.Authority);
        }

        private static bool IsDownloadsDocument(Android.Net.Uri uri)
        {
            return "com.android.providers.downloads.documents".Equals(uri.Authority);
        }

        private static bool IsMediaDocument(Android.Net.Uri uri)
        {
            return "com.android.providers.media.documents".Equals(uri.Authority);
        }

        private static bool IsGooglePhotosUri(Android.Net.Uri uri)
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
