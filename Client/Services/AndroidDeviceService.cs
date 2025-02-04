using System.Diagnostics.CodeAnalysis;
using Client.Interfaces;
using Domain.Enums;
using Domain.Models;

namespace Client.Services
{
    public class AndroidDeviceService : IDeviceService
    {
        [SuppressMessage("Major Code Smell", "S3358:Ternary operators should not be nested", Justification = "It looks good")]
        public DeviceModel GetCurrentDeviceInfo()
        {
            string name = DeviceInfo.Current.Name;
            string model = DeviceInfo.Current.Model;
            string manufacturer = DeviceInfo.Current.Manufacturer;
            DeviceModelType type =
                DeviceInfo.Current.Idiom == DeviceIdiom.Phone ? DeviceModelType.Mobile :
                DeviceInfo.Current.Idiom == DeviceIdiom.Desktop ? DeviceModelType.Desktop :
                DeviceInfo.Current.Idiom == DeviceIdiom.Tablet ? DeviceModelType.Tablet :
                DeviceModelType.Unknown;

            return new DeviceModel(name, type, model, manufacturer);
        }
    }
}
