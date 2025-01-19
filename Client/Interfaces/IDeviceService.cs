using Client.Models;

namespace Client.Interfaces
{
    public interface IDeviceService
    {
        DeviceInfoModel GetCurrentDeviceInfo();
    }
}
