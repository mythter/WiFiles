using System.Text.Json.Serialization;
using Client.Enums;

namespace Client.Models
{
    public class DeviceModel
    {
        public string Name { get; set; }

        public string? Model { get; set; }

        public string? Manufacturer { get; set; }

        public DeviceModelType Type { get; set; }

        public DeviceModel(string name)
        {
            Name = name;
        }

        [JsonConstructor]
        public DeviceModel(
            string name,
            DeviceModelType type,
            string? model = null,
            string? manufacturer = null) : this(name)
        {
            Model = model;
            Manufacturer = manufacturer;
            Type = type;
        }
    }
}
