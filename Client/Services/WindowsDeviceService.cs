#if WINDOWS
using System.Management;
using Client.Constants;
using Client.Enums;
using Client.Interfaces;
using Client.Models;

namespace Client.Services
{
    public class WindowsDeviceService : IDeviceService
    {
        public DeviceInfoModel GetCurrentDeviceInfo()
        {
            Guid id = DeviceConstants.Id;
            string name = Environment.MachineName;
            string model = DeviceInfo.Current.Model;
            string manufacturer = DeviceInfo.Current.Manufacturer;
            DeviceModelType type = GetDeviceType();

            return new DeviceInfoModel(id, name, type, model, manufacturer);
        }

        private static DeviceModelType GetDeviceType()
        {
            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem"))
            {
                foreach (var obj in searcher.Get())
                {
                    var systemType = obj["PCSystemType"];

                    if (systemType != null)
                    {
                        return Convert.ToInt32(systemType) switch
                        {
                            1 => DeviceModelType.Desktop,
                            2 => DeviceModelType.Laptop,
                            _ => DeviceModelType.Unknown,
                        };
                    }
                }
            }

            return DeviceModelType.Unknown;
        }
    }
}
#endif