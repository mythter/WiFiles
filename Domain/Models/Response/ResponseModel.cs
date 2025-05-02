using Domain.Models.Device;
using Domain.Models.Encryptions;

namespace Domain.Models.Response
{
	public class ResponseModel<T> where T : DeviceModel
	{
		public T Receiver { get; set; }

		public bool IsAccepted { get; set; }

		public Encryption? Encryption { get; set; }

		public ResponseModel(bool isAccepted, T receiver)
		{
			IsAccepted = isAccepted;
			Receiver = receiver;
		}
	}
}
