using System.Collections.Concurrent;
using System.Threading.Channels;
using Domain.Models;

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
    }
}
