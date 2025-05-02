using System.Text.Json.Serialization;
using Domain.Enums;

namespace Domain.Models.Device
{
	public class GlobalDeviceModel : DeviceModel
	{
		public GlobalDeviceModel(string name) : base(name)
		{ }

		public GlobalDeviceModel(DeviceModel deviceModel)
			: this(
				deviceModel.Name,
				deviceModel.Type,
				deviceModel.Model,
				deviceModel.Manufacturer)
		{ }

		[JsonConstructor]
		public GlobalDeviceModel(
			string name,
			DeviceModelType type,
			string? model,
			string? manufacturer)
		: base(name, type, model, manufacturer)
		{ }
	}
}
