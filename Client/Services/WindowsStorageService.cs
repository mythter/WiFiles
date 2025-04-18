using CommunityToolkit.Maui.Storage;

namespace Client.Services
{
	public class WindowsStorageService : StorageServiceBase
	{
		public override string SaveFolder { get; protected set; }

		public WindowsStorageService()
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

		public override bool TrySetSaveFolder(string path)
		{
			if (Directory.Exists(path))
			{
				SaveFolder = path;
				return true;
			}

			return false;
		}

		public override async Task<List<string>> PickFilesAsync()
		{
			var result = await FilePicker.PickMultipleAsync();
			return result
				.Select(f => f.FullPath)
				.ToList();
		}

		public override async Task<string?> PickFolderAsync()
		{
			var result = await FolderPicker.PickAsync(default);
			return result?.Folder?.Path;
		}
	}
}
