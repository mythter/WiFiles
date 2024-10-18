using System.Net;

namespace Client.Interfaces
{
    public interface INetworkInfoService
    {
        List<IPAddress> GetNetworkInterfaceIPAddresses();
    }
}
