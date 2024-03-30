#if ANDROID
using Android.Net;
using Client.Interfaces;
using Java.Net;
using System.Net;

namespace Client.Services
{
    public class AndroidLocalNetworkService : ILocalNetworkService
    {
        public List<IPAddress> GetAllHostIpAddressesWithGateway()
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

        public IPAddress? GetGatewayByIp(IPAddress ip)
        {
            var context = Android.App.Application.Context;
            IPAddress? gateway = null;

            ConnectivityManager? connectivityManager =
                    context.GetSystemService(Android.Content.Context.ConnectivityService) as ConnectivityManager;

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
                        IsInSameSubnet(ip, properties))
                    {
                        gateway = new IPAddress(route.Gateway.GetAddress()!);
                    }
                }
            }

            return gateway;
        }

        public IPAddress? GetSubnetMaskByIp(IPAddress ip)
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
                        IsInSameSubnet(ip, properties))
                    {
                        mask = new IPAddress(PrefixLengthToByteArray(address.PrefixLength));
                    }
                }
            }

            return mask;
        }

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

        private bool IsInSameSubnet(byte[]? addressBytes, int subnetMaskLength, InetAddress interfaceAddress)
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

        private bool IsInSameSubnet(InetAddress address, Java.Net.NetworkInterface nic)
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
                        IsInSameSubnet(address.GetAddress(), iface.NetworkPrefixLength, interfaceAddress))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool IsInSameSubnet(InetAddress address, LinkProperties properties)
        {
            foreach (LinkAddress ip in properties.LinkAddresses)
            {
                if (ip.Address != null &&
                    IsInSameSubnet(address.GetAddress(), ip.PrefixLength, ip.Address))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsInSameSubnet(IPAddress address, LinkProperties properties)
        {
            foreach (LinkAddress ip in properties.LinkAddresses)
            {
                if (ip.Address != null &&
                    IsInSameSubnet(address.GetAddressBytes(), ip.PrefixLength, ip.Address))
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
    }
}
#endif