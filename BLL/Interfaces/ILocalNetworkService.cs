using System.Net;

namespace BLL.Interfaces
{
    public interface ILocalNetworkService
    {
        List<IPAddress> GetAllHostIpAddresses();
        List<IPAddress> GetAllHostIpAddressesWithGateway();
        IPAddress? GetGatewayByIp(IPAddress ip);
        IPAddress? GetGatewayByHostIp(IPAddress ip);
        IPAddress? GetSubnetMaskByIp(IPAddress ip);
        IPAddress? GetSubnetMaskByHostIp(IPAddress ip);
        IPAddress GetNetworkAddress(IPAddress ip, IPAddress mask);
    }
}
