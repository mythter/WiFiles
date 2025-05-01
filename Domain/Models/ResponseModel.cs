namespace Domain.Models
{
	public class ResponseModel<T> where T : DeviceModel
	{
		public T Receiver { get; set; }

		public bool IsAccepted { get; set; }

		public ResponseModel(bool isAccepted)

		public ResponseModel(bool isAccepted, T receiver)
		{
			IsAccepted = isAccepted;
			Receiver = receiver;
		}
	}
}
