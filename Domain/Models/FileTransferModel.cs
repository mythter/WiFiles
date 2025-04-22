using System.Text.Json.Serialization;
using Domain.Enums;

namespace Domain.Models
{
	public class FileTransferModel : FileModel
	{
		public string? Sender { get; set; }
		public string? Receiver { get; set; }
		public TransferStatus Status { get; set; }
		public TransferType TransferType { get; set; }

		private long _currentProgress;
		public long CurrentProgress
		{
			get => _currentProgress;
			set
			{
				_currentProgress = value;
				ProgressChanged?.Invoke(this, _currentProgress);
			}
		}

		public event EventHandler<long>? ProgressChanged;

		[JsonConstructor]
		public FileTransferModel(string path, long size, TransferType transferType, string? sender = null, string? receiver = null)
			: base(path, size)
		{
			TransferType = transferType;
			Sender = sender;
			Receiver = receiver;
		}

		public FileTransferModel(FileModel fileModel, TransferType transferType, string? sender = null, string? receiver = null)
			: this(
				fileModel.Path, 
				fileModel.Size, 
				transferType, 
				sender, 
				receiver)
		{
		}
	}
}
