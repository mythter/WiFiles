using System.Text.Json.Serialization;

namespace Domain.Models
{
	public class GlobalRequestModel : RequestModel
	{
		public long SenderSessionId { get; set; }

		public long ReceiverSessionId { get; set; }

		public GlobalDeviceModel Sender { get; set; }

		public GlobalRequestModel(long senderSessionId, long receiverSessionId, GlobalDeviceModel sender)
		{
			SenderSessionId = senderSessionId;
			ReceiverSessionId = receiverSessionId;
			Sender = sender;
		}

		[JsonConstructor]
		public GlobalRequestModel(long senderSessionId, long receiverSessionId, GlobalDeviceModel sender, List<FileMetadata> files)
		{
			SenderSessionId = senderSessionId;
			ReceiverSessionId = receiverSessionId;
			Sender = sender;
			Files = files ?? [];
		}
	}
}
