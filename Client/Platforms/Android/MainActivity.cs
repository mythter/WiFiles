using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Database;
using Android.OS;
using Android.Provider;
using Android.Runtime;
using Android.Webkit;
using Android.Widget;
using AndroidX.Activity.Result;
using AndroidX.Activity.Result.Contract;
using AndroidX.DocumentFile.Provider;
using Client.Services;
using Java.Lang;
using Java.Nio.FileNio;

namespace Client
{

    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        public static Func<Task<string?>> PickFolderAsync { get; set; }
        public static Func<Task<List<string>>> PickFilesAsync { get; set; }

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

                    //var docUriTree = DocumentsContract.BuildDocumentUriUsingTree(uri, DocumentsContract.GetTreeDocumentId(uri));

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

        protected void OnActivity(int requestCode, Result resultCode, Android.Content.Intent intent)
        {


            int i = 8;
            string path = null;

            if (intent?.Data != null)
            {
                var uri = intent.Data;

                var docUriTree = DocumentsContract.BuildDocumentUriUsingTree(uri, DocumentsContract.GetTreeDocumentId(uri));

                var context = Android.App.Application.Context;
                //context.ContentResolver.TakePersistableUriPermission(docUriTree, ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);
                path = GetRealPath(context, docUriTree);

                //if (uri.Scheme == "file")
                //{
                //    path = uri.Path;
                //}
                //else if (uri.Scheme == "content")
                //{
                //    path = GetContentAbsolutePath(uri);
                //}

                if (IsTreeUri(uri))
                {
                    path = GetDocumentFilePath(uri);
                }
                else
                {
                    path = GetContentAbsolutePath(uri);
                }

                var folderPath = GetFolderPathFromUri(uri);
            }
        }

        private string GetFolderPathFromUri(Android.Net.Uri uri)
        {
            var documentFile = DocumentFile.FromTreeUri(Platform.CurrentActivity, uri);
            return documentFile?.Uri?.Path;
        }

        private static string GetContentAbsolutePath(Android.Net.Uri uri)
        {
            var context = Android.App.Application.Context;
            string[] projection = { MediaStore.MediaColumns.Data };
            ICursor cursor = context.ContentResolver.Query(uri, projection, null, null, null);
            if (cursor != null && cursor.MoveToFirst())
            {
                int columnIndex = cursor.GetColumnIndexOrThrow(MediaStore.MediaColumns.Data);
                string path = cursor.GetString(columnIndex);
                cursor.Close();
                return path;
            }
            return null;
        }

        private static string GetDocumentFilePath(Android.Net.Uri treeUri)
        {
            var context = Android.App.Application.Context;
            //string[] split = treeUri.Path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            //if (split.Length >= 2)
            //{
            //    string rootPath = split[1]; // Находим корневую папку, например, "primary:CastBox"
            //    string documentPath = treeUri.LastPathSegment; // Последний сегмент в URI содержит имя документа

            //    string[] rootSplit = rootPath.Split(':');
            //    if (rootSplit.Length == 2)
            //    {
            //        string directoryType = rootSplit[0]; // Получаем тип директории, например, "primary"
            //        string path = Android.OS.Environment.GetExternalStoragePublicDirectory(directoryType).AbsolutePath;
            //        return Path.Combine(path, documentPath);
            //    }
            //}
            //return null;

            string[] split = treeUri.Path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (split.Length >= 2)
            {
                string rootPath = split[1]; // Находим корневую папку, например, "primary:CastBox"
                string documentPath = treeUri.LastPathSegment; // Последний сегмент в URI содержит имя документа

                string[] rootSplit = rootPath.Split(':', StringSplitOptions.RemoveEmptyEntries);
                if (rootSplit.Length == 2)
                {
                    string directoryType = rootSplit[0]; // Получаем тип директории, например, "primary"
                    string rootDocumentId = rootSplit[1]; // Получаем идентификатор корневого документа, например, "CastBox"

                    // Получаем документ по его идентификатору
                    var docUri = DocumentsContract.BuildDocumentUri("com.android.externalstorage.documents", "document");
                    var documentUri = DocumentsContract.BuildChildDocumentsUriUsingTree(treeUri, rootDocumentId);
                    string[] projection = { DocumentsContract.Document.ColumnDisplayName };
                    ICursor cursor = context.ContentResolver.Query(documentUri, projection, null, null, null);
                    if (cursor != null)
                    {
                        while (cursor.MoveToNext())
                        {
                            string displayName = cursor.GetString(0);
                            if (displayName == documentPath)
                            {
                                string documentId = cursor.GetString(0);
                                var uri = DocumentsContract.BuildDocumentUriUsingTree(treeUri, documentId);
                                return GetPathFromContentUri(context, uri);
                            }
                        }
                        cursor.Close();
                    }
                }
            }
            return null;
        }

        private static string GetPathFromContentUri(Context context, Android.Net.Uri contentUri)
        {
            string path = null;
            string[] projection = { MediaStore.MediaColumns.Data };
            ContentResolver contentResolver = context.ContentResolver;
            ICursor cursor = contentResolver.Query(contentUri, projection, null, null, null);
            if (cursor != null && cursor.MoveToFirst())
            {
                int columnIndex = cursor.GetColumnIndexOrThrow(MediaStore.MediaColumns.Data);
                path = cursor.GetString(columnIndex);
                cursor.Close();
            }
            return path;
        }

        private static bool IsTreeUri(Android.Net.Uri uri)
        {
            return DocumentsContract.IsTreeUri(uri);
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

}
