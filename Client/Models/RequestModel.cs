using Client.Enums;

namespace Client.Models
{
    public class RequestModel
    {
        public string DeviceName { get; set; }

        public string? DeviceModel { get; set; }

        public DeviceModelType DeviceType { get; set; }

        public List<FileMetadata> Files { get; set; } = new();
    }
}
