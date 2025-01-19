using System.Diagnostics.CodeAnalysis;
using Client.Constants;
using Client.Enums;
using Client.Interfaces;
using Client.Models;

namespace Client.Services
{
    public class AndroidDeviceService : IDeviceService
    {
        [SuppressMessage("Major Code Smell", "S3358:Ternary operators should not be nested", Justification = "It looks good")]
        public DeviceInfoModel GetCurrentDeviceInfo()
        {
            Guid id = DeviceConstants.Id;
            string name = DeviceInfo.Current.Name;
            string model = DeviceInfo.Current.Model;
            string manufacturer = DeviceInfo.Current.Manufacturer;
            DeviceModelType type =
                DeviceInfo.Current.Idiom == DeviceIdiom.Phone ? DeviceModelType.Mobile :
                DeviceInfo.Current.Idiom == DeviceIdiom.Desktop ? DeviceModelType.Desktop :
                DeviceInfo.Current.Idiom == DeviceIdiom.Tablet ? DeviceModelType.Tablet :
                DeviceModelType.Unknown;

            return new DeviceInfoModel(id, name, type, model, manufacturer);
        }
    }
}
