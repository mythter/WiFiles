using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Server.Models
{
    public class Session
    {
        public string SenderConnectionId { get; }
        public string ReceiverConnectionId { get; }

        public ConcurrentDictionary<Guid, Channel<byte[]>> FileChannels { get; } = new();

        public Session(string senderid, string receiverId)
        {
            SenderConnectionId = senderid;
            ReceiverConnectionId = receiverId;
        }

        public override bool Equals(object? obj)
        {
            if (obj is Session other)
            {
                return SenderConnectionId == other.SenderConnectionId && ReceiverConnectionId == other.ReceiverConnectionId;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(SenderConnectionId, ReceiverConnectionId);
        }
    }
}
