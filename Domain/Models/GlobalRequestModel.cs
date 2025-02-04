using System.Text.Json.Serialization;

namespace Domain.Models
{
    public class GlobalRequestModel : RequestModel
    {
        public long SenderSessionId { get; set; }

        public GlobalRequestModel(long senderSessionId)
        {
            SenderSessionId = senderSessionId;
        }

        [JsonConstructor]
        public GlobalRequestModel(long senderSessionId, List<FileMetadata> files)
        {
            SenderSessionId = senderSessionId;
            Files = files ?? [];
        }
    }
}
