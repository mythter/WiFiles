#if ANDROID
using System.Net;
using Client.Interfaces;
using Java.Net;
using Java.Util;

namespace Client.Services
{
    public class AndroidNetworkInfoService : INetworkInfoService
    {
        public List<IPAddress> GetNetworkInterfaceIPAddresses()
        {
            List<IPAddress> result = new List<IPAddress>();
            var networkInterfaces = Collections.List(NetworkInterface.NetworkInterfaces!);

            foreach (NetworkInterface inter in networkInterfaces)
            {
                if (!inter.IsUp || inter.IsLoopback || inter.InterfaceAddresses is null)
                {
                    continue;
                }

                foreach (var item in inter.InterfaceAddresses
                    .Where(a => a.Address is not null && a.Address.IsSiteLocalAddress))
                {
                    var ip = item.Address!.GetAddress()!;
                    result.Add(new IPAddress(ip));
                }
            }

            return result;
        }
    }
}
#endif