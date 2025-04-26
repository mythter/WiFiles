using System.Text.Json.Serialization;

namespace Domain.Models
{
	public class GlobalResponseModel : ResponseModel
	{

		public string ReceiverName { get; set; }

		[JsonConstructor]
		public GlobalResponseModel(string receiverName, bool isAccepted) : base(isAccepted)
		{
			ReceiverName = receiverName;
		}
	}
}
