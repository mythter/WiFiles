using System.Text.Json.Serialization;

namespace Domain.Models
{
	public class GlobalRequestModel : RequestModel<GlobalDeviceModel>
	{
		public long SenderSessionId { get; set; }

		public long ReceiverSessionId { get; set; }

		public GlobalRequestModel(long senderSessionId, long receiverSessionId, GlobalDeviceModel sender) : base (sender)
		{
			SenderSessionId = senderSessionId;
			ReceiverSessionId = receiverSessionId;
		}

		[JsonConstructor]
		public GlobalRequestModel(long senderSessionId, long receiverSessionId, GlobalDeviceModel sender, List<FileMetadata> files)
			 : this(senderSessionId, receiverSessionId, sender)
		{
			Files = files ?? [];
		}
	}
}
