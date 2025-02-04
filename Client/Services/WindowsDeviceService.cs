#if WINDOWS
using System.Management;
using Domain.Enums;
using Client.Interfaces;
using Domain.Models;

namespace Client.Services
{
    public class WindowsDeviceService : IDeviceService
    {
        public DeviceModel GetCurrentDeviceInfo()
        {
            string name = Environment.MachineName;
            string model = DeviceInfo.Current.Model;
            string manufacturer = DeviceInfo.Current.Manufacturer;
            DeviceModelType type = GetDeviceType();

            return new DeviceModel(name, type, model, manufacturer);
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