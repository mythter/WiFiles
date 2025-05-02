using Domain.Models.Device;
using Domain.Models.Encryptions;
using Domain.Models.File;

namespace Domain.Models.Request
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
