using Client.Models;

namespace Client.Interfaces
{
    public interface IDeviceService
    {
        DeviceModel GetCurrentDeviceInfo();
    }
}
