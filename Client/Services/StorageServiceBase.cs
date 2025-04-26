using Client.Interfaces;
using Domain.Models;

namespace Client.Services
{
	public abstract class StorageServiceBase : IStorageService
	{
		public abstract string SaveFolder { get; protected set; }

		public SynchronizedCollection<FileModel> SelectedFiles { get; set; } = [];

		public SynchronizedCollection<FileTransferModel> SentFiles { get; set; } = [];

		public SynchronizedCollection<FileTransferModel> ReceivedFiles { get; set; } = [];

		public virtual bool CheckIfFileReadable(string filePath, bool throwIfFails = false)
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

		public abstract bool CheckIfDirectoryWritable(string dirPath, bool throwIfFails = false);

		public abstract string GetDefaultFolder();

		public abstract Task<List<string>> PickFilesAsync();

		public abstract Task<string?> PickFolderAsync();

		public abstract bool TrySetSaveFolder(string path);
	}
}
