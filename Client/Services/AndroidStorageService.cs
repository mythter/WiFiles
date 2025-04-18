#if ANDROID

namespace Client.Services
{
	public class AndroidStorageService : StorageServiceBase
	{
		public override string SaveFolder { get; protected set; }

		public AndroidStorageService()
		{
			SaveFolder = GetDefaultFolder();
		}

		public override bool CheckIfDirectoryWritable(string dirPath, bool throwIfFails = false)
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

		public override string GetDefaultFolder()
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

		public override bool TrySetSaveFolder(string path)
		{
			Java.IO.File folder = new Java.IO.File(path);
			if (folder.Exists())
			{
				SaveFolder = path;
				return true;
			}

			return false;
		}

		public override async Task<List<string>> PickFilesAsync()
		{
			return await MainActivity.PickFilesAsync();
		}

		public override async Task<string?> PickFolderAsync()
		{
			return await MainActivity.PickFolderAsync();
		}
	}
}
#endif
