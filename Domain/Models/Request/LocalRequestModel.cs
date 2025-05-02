using System.Text.Json.Serialization;
using Domain.Models.Device;
using Domain.Models.File;

namespace Domain.Models.Request
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
