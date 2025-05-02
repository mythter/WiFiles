using Domain.Models.Device;

namespace Client.Interfaces
{
	public interface IDeviceService
	{
		DeviceModel GetCurrentDeviceInfo();
	}
}
