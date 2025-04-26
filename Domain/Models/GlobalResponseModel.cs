using System.Text.Json.Serialization;

namespace Domain.Models
{
	public class GlobalResponseModel : ResponseModel
	{

		public long ReceiverSessionId { get; set; }

		public long SenderSessionId { get; set; }

		public string ReceiverName { get; set; }

		[JsonConstructor]
		public GlobalResponseModel(long receiverSessionId, long senderSessionId, string receiverName, bool isAccepted) : base(isAccepted)
		{
			ReceiverSessionId = receiverSessionId;
			SenderSessionId = senderSessionId;
			ReceiverName = receiverName;
		}
	}
}
