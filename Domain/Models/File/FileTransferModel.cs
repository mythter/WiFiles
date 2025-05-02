using System.Text.Json.Serialization;
using Domain.Enums;
using Domain.Models.Device;

namespace Domain.Models.File
{
	public class FileTransferModel : FileModel
	{
		public DeviceModel? Sender { get; set; }
		public DeviceModel? Receiver { get; set; }
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
		public FileTransferModel(string path, long size, TransferType transferType, DeviceModel? sender = null, DeviceModel? receiver = null)
			: base(path, size)
		{
			TransferType = transferType;
			Sender = sender;
			Receiver = receiver;
		}

		public FileTransferModel(FileModel fileModel, TransferType transferType, DeviceModel? sender = null, DeviceModel? receiver = null)
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
