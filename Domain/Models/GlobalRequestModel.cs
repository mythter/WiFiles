using System.Text.Json.Serialization;

namespace Domain.Models
{
	public class GlobalRequestModel : RequestModel
	{
		public long SenderSessionId { get; set; }

		public GlobalDeviceModel Sender { get; set; }

		public GlobalRequestModel(long senderSessionId, GlobalDeviceModel sender)
		{
			SenderSessionId = senderSessionId;
			Sender = sender;
		}

		[JsonConstructor]
		public GlobalRequestModel(long senderSessionId, GlobalDeviceModel sender, List<FileMetadata> files)
		{
			SenderSessionId = senderSessionId;
			Sender = sender;
			Files = files ?? [];
		}
	}
}
