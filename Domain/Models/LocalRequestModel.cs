using System.Text.Json.Serialization;

namespace Domain.Models
{
    public class LocalRequestModel : RequestModel
    {
        public LocalDeviceModel Sender { get; set; }

        public LocalRequestModel(LocalDeviceModel sender)
        {
            Sender = sender;
        }

        [JsonConstructor]
        public LocalRequestModel(LocalDeviceModel sender, List<FileMetadata> files)
        {
            Sender = sender;
            Files = files ?? [];
        }
    }
}
