using Client.Constants;
using Client.Interfaces;
using Client.Models;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Client.Services
{
    public class LocalNetworkService : ILocalNetworkService, IDisposable
    {
        public event EventHandler<LocalDeviceModel>? DeviceFound;

        private readonly IDeviceService _deviceService;

        private readonly INetworkInfoService _networkInfoService;

        private UdpClient _udpListener;

        private bool _disposedValue;

        private bool _listening;

        public LocalNetworkService(IDeviceService deviceService, INetworkInfoService networkInfoService)
        {
            _deviceService = deviceService;
            _networkInfoService = networkInfoService;

            _udpListener = new UdpClient();
        }

        public async Task StartMulticastListeningAsync()
        {
            if (_listening)
            {
                return;
            }

            _listening = true;

            foreach (var ip in _networkInfoService.GetNetworkInterfaceIPAddresses())
            {
                _udpListener.JoinMulticastGroup(NetworkConstants.MulticastIP, ip);
            }

            _udpListener.Client.Bind(new IPEndPoint(IPAddress.Any, NetworkConstants.Port));

            // on Windows only when this is setted it doesn't receive request from itself
            _udpListener.MulticastLoopback = false;

            while (_listening)
            {
                try
                {
                    var udpResult = await _udpListener.ReceiveAsync();
                    var foundDevice = GetLocalDeviceFromByteArray(udpResult.Buffer);

                    // check if request is not coming from the current device
                    if (foundDevice is not null && foundDevice.Id != DeviceConstants.Id)
                    {
                        foundDevice.IP = udpResult.RemoteEndPoint.Address;
                        DeviceFound?.Invoke(this, foundDevice);

                        await SendMulticastResponseAsync(udpResult.RemoteEndPoint);
                    }
                }
                catch (ObjectDisposedException)
                {
                    // listening stopped
                    break;
                }
            }
        }

        public void StopMulticastListening()
        {
            if (!_listening)
            {
                return;
            }

            _listening = false;

            _udpListener.Close();
            _udpListener = new UdpClient();
        }

        public async Task StartMulticastScanAsync(IPAddress networkInterfaceAddress)
        {
            using var udpClient = new UdpClient();

            udpClient.Client.SetSocketOption(
                SocketOptionLevel.IP,
                SocketOptionName.MulticastInterface,
                networkInterfaceAddress.GetAddressBytes());

            // on Android only when this is setted it doesn't receive request from itself
            udpClient.MulticastLoopback = false;

            var messageBytes = GetCurrentLocalDeviceBytes();
            var multicastEndPoint = new IPEndPoint(NetworkConstants.MulticastIP, NetworkConstants.Port);

            await udpClient.SendAsync(messageBytes, messageBytes.Length, multicastEndPoint);

            using var cts = new CancellationTokenSource();
            cts.CancelAfter(NetworkConstants.MulticastScanResponseTimeout);
            await ListenForMulticastResponsesAsync(udpClient, cts.Token);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _udpListener.Dispose();
                }

                _disposedValue = true;
            }
        }

        private async Task SendMulticastResponseAsync(IPEndPoint requestEndpoint)
        {
            var responseBytes = GetCurrentLocalDeviceBytes();
            await _udpListener.SendAsync(responseBytes, responseBytes.Length, requestEndpoint);
        }

        private async Task ListenForMulticastResponsesAsync(UdpClient udpClient, CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var result = await udpClient.ReceiveAsync(cancellationToken);
                    var foundDevice = GetLocalDeviceFromByteArray(result.Buffer);

                    if (foundDevice is not null)
                    {
                        foundDevice.IP = result.RemoteEndPoint.Address;
                        DeviceFound?.Invoke(this, foundDevice);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // cancelled by timeout
            }
        }

        private byte[] GetCurrentLocalDeviceBytes()
        {
            var device = new LocalDeviceModel(_deviceService.GetCurrentDeviceInfo());
            var json = JsonSerializer.Serialize(device);
            return Encoding.UTF8.GetBytes(json);
        }

        private static LocalDeviceModel? GetLocalDeviceFromByteArray(byte[] bytes)
        {
            var json = Encoding.UTF8.GetString(bytes);
            return JsonSerializer.Deserialize<LocalDeviceModel>(json);
        }
    }
}
