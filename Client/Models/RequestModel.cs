using System.Net;
using System.Text.Json.Serialization;
using Client.Enums;

namespace Client.Models
{
    public class RequestModel
    {
        [JsonIgnore]
        public IPAddress IPAddress { get; set; }

        public string DeviceName { get; set; }

        public string? DeviceModel { get; set; }

        public DeviceModelType DeviceType { get; set; }

        public List<FileMetadata> Files { get; set; } = new();
    }
}
