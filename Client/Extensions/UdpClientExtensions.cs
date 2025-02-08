using System.Net.Sockets;
using System.Net;

namespace Client.Extensions
{
    public static class UdpClientExtensions
    {
        public static void JoinMulticastGroup(this UdpClient udpListener, IPAddress multicastAddress, IEnumerable<IPAddress> ipAddresses)
        {
            foreach (var ip in ipAddresses)
            {
                udpListener.JoinMulticastGroup(multicastAddress, ip);
            }
        }
    }
}
