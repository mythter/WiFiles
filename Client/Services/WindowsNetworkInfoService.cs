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

            // filter all not Loopback interfaces
            var notLoopback = interfaces.Where(i => i.NetworkInterfaceType != NetworkInterfaceType.Loopback);
            // filter all interfaces that are either in Up state or Wi-Fi type
            var upStateOrWiFiType = notLoopback.Where(i =>
                i.OperationalStatus == OperationalStatus.Up
                || i.NetworkInterfaceType == NetworkInterfaceType.Wireless80211);
            // filter out the APIPA interfaces
            var upStateOrWiFiTypeNotApipa = upStateOrWiFiType.Where(i =>
                i.NetworkInterfaceType != NetworkInterfaceType.Wireless80211
                || IsNotApipaInterface(i));

            foreach (NetworkInterface netInterface in upStateOrWiFiTypeNotApipa)
            {
                IPInterfaceProperties ipProperties = netInterface.GetIPProperties();

                var ip4Addresses = ipProperties.UnicastAddresses
                    .Where(ip => ip.Address.AddressFamily == AddressFamily.InterNetwork);
                foreach (var ip in ip4Addresses)
                {
                    if (netInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211
                        && netInterface.OperationalStatus == OperationalStatus.Up)
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

        private static bool IsNotApipaInterface(NetworkInterface inter)
        {
            foreach (var unicastIP in inter.GetIPProperties().UnicastAddresses)
            {
                byte[] addressBytes = unicastIP.Address.GetAddressBytes();
                if (addressBytes[0] == 169 && addressBytes[1] == 254)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
