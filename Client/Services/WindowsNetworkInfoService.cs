using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Client.Interfaces;

namespace Client.Services
{
    public class WindowsNetworkInfoService : INetworkInfoService
    {
        [SuppressMessage("Minor Code Smell", "S3267:Loops should be simplified with \"LINQ\" expressions", Justification = "If-else statement cannot be simplified")]
        public List<IPAddress> GetNetworkInterfaceIPAddresses()
        {
            List<IPAddress> result = new();
            NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();

            foreach (NetworkInterface netInterface in interfaces)
            {
                if (netInterface.OperationalStatus != OperationalStatus.Up ||
                    netInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                IPInterfaceProperties ipProperties = netInterface.GetIPProperties();

                var ip4Addresses = ipProperties.UnicastAddresses
                    .Where(ip => ip.Address.AddressFamily == AddressFamily.InterNetwork);
                foreach (var ip in ip4Addresses)
                {
                    if (netInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                    {
                        result.Insert(0, ip.Address);
                    }
                    else
                    {
                        result.Add(ip.Address);
                    }
                }
            }

            return result;
        }
    }
}
