using System.Text.Json.Serialization;

namespace Domain.Models
{
	public class LocalRequestModel : RequestModel<LocalDeviceModel>
	{
		public LocalRequestModel(LocalDeviceModel sender) : base(sender) 
		{
		}

		[JsonConstructor]
		public LocalRequestModel(LocalDeviceModel sender, List<FileMetadata> files) : this(sender)
		{
			Files = files ?? [];
		}
	}
}
