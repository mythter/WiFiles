using System.Net;
using Client.Enums;

namespace Client.Models
{
    public class DeviceModel
    {
        public IPAddress Ip { get; set; }

        public DeviceInfoModel Info { get; set; }

        public DeviceModel(IPAddress ip, DeviceInfoModel deviceInfo)
        {
            Ip = ip;
            Info = deviceInfo;
        }

        public override string ToString()
        {
            return Info.Type switch
            {
                DeviceModelType.Desktop or
                DeviceModelType.Laptop => Info.Model ?? Info.Name,
                _ => Info.Name
            };
        }
    }
}
