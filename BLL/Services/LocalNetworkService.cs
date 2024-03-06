using BLL.Interfaces;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace BLL.Services
{
    public class LocalNetworkService : ILocalNetworkService
    {
        public List<IPAddress> GetAllHostIpAddresses()
        {
            List<IPAddress> result = new List<IPAddress>();
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (IPAddress ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    result.Add(ip);
                }
            }

            return result;
        }

        public List<IPAddress> GetAllHostIpAddressesWithGateway()
        {
            List<IPAddress> result = new List<IPAddress>();
            NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();

            foreach (NetworkInterface netInterface in interfaces)
            {
                if (netInterface.OperationalStatus != OperationalStatus.Up ||
                    netInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                IPInterfaceProperties ipProperties = netInterface.GetIPProperties();

                if (ipProperties.GatewayAddresses.Count > 0)
                {
                    result.AddRange(ipProperties.UnicastAddresses
                        .Where(ip => ip.Address.AddressFamily == AddressFamily.InterNetwork)
                        .Select(ip => ip.Address)
                        .ToList());
                }
            }

            return result;
        }

        public IPAddress? GetGatewayByHostIp(IPAddress ip)
        {
            foreach (var n in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (n.OperationalStatus == OperationalStatus.Up &&
                    n.GetIPProperties().UnicastAddresses.Any(u => u.Address.Equals(ip)))
                {
                    return n.GetIPProperties().GatewayAddresses.FirstOrDefault()?.Address;
                }
            }

            return null;
        }

        public IPAddress? GetGatewayByIp(IPAddress ip)
        {
            IPAddress? gateway = null;

            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                IPInterfaceProperties ipProps = nic.GetIPProperties();
                foreach (var gatewayAddress in ipProps.GatewayAddresses)
                {
                    if (gatewayAddress.Address.AddressFamily == ip.AddressFamily &&
                        IsInSameSubnet(ip, nic))
                    {
                        gateway = gatewayAddress.Address;
                        break;
                    }
                }
                if (gateway != null)
                {
                    break;
                }
            }

            return gateway;
        }

        public IPAddress? GetSubnetMaskByHostIp(IPAddress ip)
        {
            IPAddress? mask = null;
            foreach (NetworkInterface i in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (i.OperationalStatus == OperationalStatus.Up)
                {
                    IPInterfaceProperties ipInterface = i.GetIPProperties();
                    mask = ipInterface.UnicastAddresses.FirstOrDefault(u => u.Equals(ip))?.IPv4Mask;

                    if (mask is not null)
                    {
                        break;
                    }
                }
            }
            return mask;
        }

        public IPAddress? GetSubnetMaskByIp(IPAddress ip)
        {
            IPAddress? mask = null;
            foreach (NetworkInterface i in NetworkInterface.GetAllNetworkInterfaces())
            {
                IPInterfaceProperties ipProps = i.GetIPProperties();
                foreach (UnicastIPAddressInformation unicastAddress in ipProps.UnicastAddresses)
                {
                    if (unicastAddress.Address.AddressFamily == AddressFamily.InterNetwork &&
                        IsInSameSubnet(ip, unicastAddress.IPv4Mask, unicastAddress.Address))
                    {
                        mask = unicastAddress.IPv4Mask;
                        break;
                    }
                }
                if (mask is not null)
                {
                    break;
                }
            }
            return mask;
        }

        private bool IsInSameSubnet(IPAddress address, NetworkInterface nic)
        {
            foreach (UnicastIPAddressInformation unicastAddress in nic.GetIPProperties().UnicastAddresses)
            {
                if (unicastAddress.Address.AddressFamily == address.AddressFamily &&
                    IsInSameSubnet(address, unicastAddress.IPv4Mask, unicastAddress.Address))
                {
                    return true;
                }
            }
            return false;
        }

        private bool IsInSameSubnet(IPAddress address, IPAddress subnetMask, IPAddress localAddress)
        {
            byte[] addressBytes = address.GetAddressBytes();
            byte[] subnetMaskBytes = subnetMask.GetAddressBytes();
            byte[] localAddressBytes = localAddress.GetAddressBytes();

            for (int i = 0; i < addressBytes.Length; i++)
            {
                if ((addressBytes[i] & subnetMaskBytes[i]) != (localAddressBytes[i] & subnetMaskBytes[i]))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
