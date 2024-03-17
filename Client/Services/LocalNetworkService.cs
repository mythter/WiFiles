#if ANDROID
using Android.Net;
using Java.Net;
#endif
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
#if ANDROID
            return AndroidGetAllHostIpAddressesWithGateway();
#elif WINDOWS
            return WindowsGetAllHostIpAddressesWithGateway();
#endif
            return new List<IPAddress>();
        }

        public IPAddress? GetGatewayByHostIp(IPAddress ip)
        {
#if ANDROID
            //return AndroidGetGatewayByHostIp();
#elif WINDOWS
            return WindowsGetGatewayByHostIp(ip);
#endif
            return null;
        }

        public IPAddress? GetGatewayByIp(IPAddress ip)
        {
#if ANDROID
            return AndroidGetGatewayByIp(ip);
#elif WINDOWS
            return WindowsGetGatewayByIp(ip);
#endif
            return null;
        }

        public IPAddress? GetSubnetMaskByHostIp(IPAddress ip)
        {
#if ANDROID
            //return AndroidGetSubnetMaskByHostIp();
#elif WINDOWS
            return WindowsGetSubnetMaskByHostIp(ip);
#endif
            return null;
        }

        public IPAddress? GetSubnetMaskByIp(IPAddress ip)
        {
#if ANDROID
            return AndroidGetSubnetMaskByIp(ip);
#elif WINDOWS
            return WindowsGetSubnetMaskByIp(ip);
#endif
            return null;
        }

        #region Common implementation
        public IPAddress GetNetworkAddress(IPAddress ip, IPAddress mask)
        {
            byte[] addressBytes = ip.GetAddressBytes();
            byte[] subnetMaskBytes = mask.GetAddressBytes();
            byte[] networkBytes = new byte[4];

            for (int i = 0; i < addressBytes.Length; i++)
            {
                networkBytes[i] = (byte)(addressBytes[i] & subnetMaskBytes[i]);
            }

            return new IPAddress(networkBytes);
        }
        #endregion

#if WINDOWS
        #region Windows implementation

        private List<IPAddress> WindowsGetAllHostIpAddressesWithGateway()
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

        public IPAddress? WindowsGetGatewayByHostIp(IPAddress ip)
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

        public IPAddress? WindowsGetGatewayByIp(IPAddress ip)
        {
            IPAddress? gateway = null;

            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                IPInterfaceProperties ipProps = nic.GetIPProperties();
                foreach (var gatewayAddress in ipProps.GatewayAddresses)
                {
                    if (gatewayAddress.Address.AddressFamily == ip.AddressFamily &&
                        WindowsIsInSameSubnet(ip, nic))
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

        public IPAddress? WindowsGetSubnetMaskByHostIp(IPAddress ip)
        {
            IPAddress? mask = null;
            foreach (NetworkInterface i in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (i.OperationalStatus == OperationalStatus.Up)
                {
                    IPInterfaceProperties ipInterface = i.GetIPProperties();
                    mask = ipInterface.UnicastAddresses.FirstOrDefault(u => u.Address.Equals(ip))?.IPv4Mask;

                    if (mask is not null)
                    {
                        break;
                    }
                }
            }
            return mask;
        }

        public IPAddress? WindowsGetSubnetMaskByIp(IPAddress ip)
        {
            IPAddress? mask = null;
            foreach (NetworkInterface i in NetworkInterface.GetAllNetworkInterfaces())
            {
                IPInterfaceProperties ipProps = i.GetIPProperties();
                foreach (UnicastIPAddressInformation unicastAddress in ipProps.UnicastAddresses)
                {
                    if (unicastAddress.Address.AddressFamily == AddressFamily.InterNetwork &&
                        WindowsIsInSameSubnet(ip, unicastAddress.IPv4Mask, unicastAddress.Address))
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

        private bool WindowsIsInSameSubnet(IPAddress address, NetworkInterface nic)
        {
            foreach (UnicastIPAddressInformation unicastAddress in nic.GetIPProperties().UnicastAddresses)
            {
                if (unicastAddress.Address.AddressFamily == address.AddressFamily &&
                    WindowsIsInSameSubnet(address, unicastAddress.IPv4Mask, unicastAddress.Address))
                {
                    return true;
                }
            }
            return false;
        }

        private bool WindowsIsInSameSubnet(IPAddress address, IPAddress subnetMask, IPAddress localAddress)
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

        #endregion
#elif ANDROID
        #region Android implementation

        private List<IPAddress> AndroidGetAllHostIpAddressesWithGateway()
        {
            var context = Android.App.Application.Context;
            List<IPAddress> result = new List<IPAddress>();
            //var networkInterfaces = Collections.List(Java.Net.NetworkInterface.NetworkInterfaces);

            //foreach (Java.Net.NetworkInterface inter in networkInterfaces)
            //{
            //    var temp = inter;
            //    var ip1 = inter.InterfaceAddresses;
            //    var ip2 = inter.InetAddresses;
            //    var mac = inter.GetHardwareAddress();

            //    string? ip;
            //    string? name;

            //    foreach (var item in ip1)
            //    {
            //        var i = item.Address;
            //        ip = item.Address.HostAddress;
            //    }

            //    int p = 9;
            //}

            Android.Net.ConnectivityManager? connectivityManager =
                context.GetSystemService(Android.Content.Context.ConnectivityService) as Android.Net.ConnectivityManager;

            if (connectivityManager == null)
                return result;

            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.N || true)
            {
                var networks = connectivityManager.GetAllNetworks();

                if (networks == null)
                    return result;

                foreach (var network in networks)
                {
                    var properties = connectivityManager.GetLinkProperties(network);
                    if (properties == null)
                        return result;

                    foreach (Android.Net.RouteInfo route in properties.Routes)
                    {
                        if (route.IsDefaultRoute && !(route.Gateway is Inet6Address))
                        {
                            foreach (var ip in properties.LinkAddresses)
                            {
                                if (ip != null &&
                                    ip.Address != null &&
                                    ip.Address.IsSiteLocalAddress &&
                                    ip.Address.GetAddress() != null &&
                                    ip.Address is Inet4Address)
                                {
                                    result.Add(new IPAddress(ip.Address.GetAddress()!));
                                }
                            }
                            break;
                        }
                    }
                }
            }

            return result;

            // GetActiveNetwork() requires M or later.
            if (Android.OS.Build.VERSION.SdkInt < Android.OS.BuildVersionCodes.M)
                return result;

            Android.Net.Network activeNetwork = connectivityManager.ActiveNetwork;

            var v3 = connectivityManager.GetLinkProperties(activeNetwork);
            var v4 = connectivityManager.GetNetworkInfo(activeNetwork);

            if (activeNetwork == null)
                return result;

            var routes = connectivityManager.GetLinkProperties(activeNetwork)?.Routes;

            if (routes == null)
                return result;

            foreach (Android.Net.RouteInfo route in routes)
            {
                if (route.IsDefaultRoute && !(route.Gateway is Java.Net.Inet6Address))
                {
                    //result.Add();
                    //return route.Gateway;
                }
            }

            return result;
        }

        public IPAddress? AndroidGetGatewayByHostIp(IPAddress ip)
        {
            return null;
        }

        public IPAddress? AndroidGetGatewayByIp(IPAddress ip)
        {
            var context = Android.App.Application.Context;
            IPAddress? gateway = null;

            Android.Net.ConnectivityManager? connectivityManager =
                    context.GetSystemService(Android.Content.Context.ConnectivityService) as Android.Net.ConnectivityManager;

            if (connectivityManager == null)
                return null;

            var networks = connectivityManager.GetAllNetworks();

            if (networks == null)
                return null;

            foreach (var network in networks)
            {
                var properties = connectivityManager.GetLinkProperties(network);
                if (properties == null)
                    return null;

                foreach (RouteInfo route in properties.Routes)
                {
                    if (route.IsDefaultRoute &&
                        !(route.Gateway is Inet6Address) &&
                        route.Gateway != null &&
                        AndroidIsInSameSubnet(ip, properties))
                    {
                        gateway = new IPAddress(route.Gateway.GetAddress()!);
                    }
                }
            }

            return gateway;
        }

        public IPAddress? AndroidGetSubnetMaskByHostIp(IPAddress ip)
        {
            return null;
        }

        public IPAddress? AndroidGetSubnetMaskByIp(IPAddress ip)
        {
            var context = Android.App.Application.Context;
            IPAddress? mask = null;

            ConnectivityManager? connectivityManager =
                    context.GetSystemService(Android.Content.Context.ConnectivityService) as Android.Net.ConnectivityManager;

            if (connectivityManager == null)
                return null;

            var networks = connectivityManager.GetAllNetworks();

            if (networks == null)
                return null;

            foreach (var network in networks)
            {
                var properties = connectivityManager.GetLinkProperties(network);
                if (properties == null)
                    return null;

                foreach (LinkAddress address in properties.LinkAddresses)
                {
                    if (address.Address != null &&
                        AndroidIsInSameSubnet(ip, properties))
                    {
                        mask = new IPAddress(PrefixLengthToByteArray(address.PrefixLength));
                    }
                }
            }

            return mask;
        }

        private bool AndroidIsInSameSubnet(byte[]? addressBytes, int subnetMaskLength, InetAddress interfaceAddress)
        {
            //byte[]? addressBytes = address.GetAddress();
            byte[] subnetMaskBytes;
            byte[]? interfaceBytes = interfaceAddress.GetAddress();

            if (addressBytes == null || interfaceBytes == null)
                return false;

            subnetMaskBytes = PrefixLengthToByteArray(subnetMaskLength);

            for (int i = 0; i < addressBytes.Length; i++)
            {
                if ((addressBytes[i] & subnetMaskBytes[i]) != (interfaceBytes[i] & subnetMaskBytes[i]))
                {
                    return false;
                }
            }
            return true;
        }

        private bool AndroidIsInSameSubnet(InetAddress address, Java.Net.NetworkInterface nic)
        {
            var interfaceAddresses = nic.InetAddresses;
            if (interfaceAddresses == null)
                return false;

            while (interfaceAddresses.HasMoreElements)
            {
                InetAddress? interfaceAddress = interfaceAddresses.NextElement() as InetAddress;
                if (interfaceAddress == null || nic.InterfaceAddresses == null)
                    return false;

                foreach (InterfaceAddress iface in nic.InterfaceAddresses)
                {
                    if (interfaceAddress.GetAddress() != null &&
                        interfaceAddress.GetAddress()!.Length == 4 &&
                        address.GetAddress() != null &&
                        address.GetAddress()!.Length == 4 &&
                        interfaceAddress.GetAddress()!.SequenceEqual(address.GetAddress()!) &&
                        AndroidIsInSameSubnet(address.GetAddress(), iface.NetworkPrefixLength, interfaceAddress))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool AndroidIsInSameSubnet(InetAddress address, LinkProperties properties)
        {
            foreach (LinkAddress ip in properties.LinkAddresses)
            {
                if (ip.Address != null &&
                    AndroidIsInSameSubnet(address.GetAddress(), ip.PrefixLength, ip.Address))
                {
                    return true;
                }
            }

            return false;
        }

        private bool AndroidIsInSameSubnet(IPAddress address, LinkProperties properties)
        {
            foreach (LinkAddress ip in properties.LinkAddresses)
            {
                if (ip.Address != null &&
                    AndroidIsInSameSubnet(address.GetAddressBytes(), ip.PrefixLength, ip.Address))
                {
                    return true;
                }
            }

            return false;
        }

        private byte[] PrefixLengthToByteArray(int subnetMaskLength)
        {
            byte[] subnetMaskBytes = new byte[4];

            for (int i = 0; i < subnetMaskBytes.Length; i++)
            {
                int bits = Math.Max(0, Math.Min(8, subnetMaskLength));
                subnetMaskBytes[i] = (byte)(0xff << (8 - bits));
                subnetMaskLength -= 8;
            }

            return subnetMaskBytes;
        }

        #endregion
#endif

    }
}
