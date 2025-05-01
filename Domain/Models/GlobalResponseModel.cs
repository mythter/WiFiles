using System.Text.Json.Serialization;

namespace Domain.Models
{
	public class GlobalResponseModel : ResponseModel<GlobalDeviceModel>
	{
		public long ReceiverSessionId { get; set; }

		public long SenderSessionId { get; set; }

		[JsonConstructor]
		public GlobalResponseModel(bool isAccepted, GlobalDeviceModel receiver, long receiverSessionId, long senderSessionId)
			: base(isAccepted, receiver)
		{
			ReceiverSessionId = receiverSessionId;
			SenderSessionId = senderSessionId;
		}
	}
}
