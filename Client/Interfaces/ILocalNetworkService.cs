using System.Net;
using Client.Models;

namespace Client.Interfaces
{
    public interface ILocalNetworkService
    {
        event EventHandler<LocalDeviceModel> DeviceFound;

        Task StartMulticastScanAsync(IPAddress networkInterfaceAddress);

        Task StartMulticastListeningAsync();

        void StopMulticastListening();
    }
}
