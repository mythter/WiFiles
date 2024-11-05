using System.Text.Json.Serialization;
using Client.Enums;

namespace Client.Models
{
    public class DeviceInfoModel
    {
        public Guid Id { get; }

        public string Name { get; set; }

        public string? Model { get; set; }

        public string? Manufacturer { get; set; }

        public DeviceModelType Type { get; set; }

        public DeviceInfoModel(Guid id, string name)
        {
            Id = id;
            Name = name;
        }

        [JsonConstructor]
        public DeviceInfoModel(
            Guid id,
            string name,
            DeviceModelType type,
            string? model = null,
            string? manufacturer = null) : this(id, name)
        {
            Model = model;
            Manufacturer = manufacturer;
            Type = type;
        }
    }
}
