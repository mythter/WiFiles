using System.Net;
using System.Text.Json.Serialization;
using Domain.Constants;
using Domain.Enums;

namespace Domain.Models
{
    public class LocalDeviceModel : DeviceModel
    {
        public IPAddress? IP { get; set; }

        public Guid Id { get; } = DeviceConstants.Id;

        public LocalDeviceModel(string name, IPAddress? ip = null) : base (name) 
        {
            IP = ip;
        }

        public LocalDeviceModel(DeviceModel deviceModel, IPAddress? ip = null)
            : this(
                deviceModel.Name,
                deviceModel.Type,
                deviceModel.Model,
                deviceModel.Manufacturer,
                ip)
        { }

        public LocalDeviceModel(
            string name,
            DeviceModelType type,
            string? model = null,
            string? manufacturer = null,
            IPAddress? ip = null)
        : base(name, type, model, manufacturer)
        {
            IP = ip;
        }

        [JsonConstructor]
        public LocalDeviceModel(
            string name,
            DeviceModelType type,
            string? model,
            string? manufacturer,
            Guid id,
            IPAddress? ip = null)
        : base(name, type, model, manufacturer)
        {
            IP = ip;
            Id = id;
        }

        public override string ToString()
        {
            return Type switch
            {
                DeviceModelType.Desktop or
                DeviceModelType.Laptop => Model ?? Name,
                _ => Name
            };
        }
    }
}
