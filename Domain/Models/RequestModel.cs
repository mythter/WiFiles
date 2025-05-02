namespace Domain.Models
{
	public class RequestModel<T> where T : DeviceModel
	{
		public T Sender { get; set; }

		public List<FileMetadata> Files { get; set; } = [];

		public Encryption? Encryption { get; set; }

		public RequestModel(T sender)
		{
			Sender = sender;
		}
	}
}
