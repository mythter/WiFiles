using System.Net;
using Client.Interfaces;

namespace Client.Services
{
    internal class GlobalNetworkService : IGlobalNetworkService
    {
        public readonly string[] _getPublicIpUrls = [
            "https://checkip.amazonaws.com/",
            "https://api.ipify.org",
            "https://icanhazip.com",
            "https://ifconfig.me",
            "https://ipinfo.io/ip"];

        public async Task<IPAddress?> GetPublicIPAddress()
        {
            using var httpClient = new HttpClient();
            try
            {
                foreach (var service in _getPublicIpUrls)
                {
                    var response = await httpClient.GetStringAsync(service);
                    if (IPAddress.TryParse(response, out var publicIP))
                    {
                        return publicIP;
                    }
                }
            }
            catch 
            { 
                // just return null if error happenned
            }

            return null;
        }
    }
}
