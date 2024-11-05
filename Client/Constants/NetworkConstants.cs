using System.Net;

namespace Client.Constants
{
    public static class NetworkConstants
    {
        public const int Port = 23969;

        public static readonly IPAddress MulticastIP = new IPAddress(new byte[] { 224, 0, 0, 171 });

        public const int MulticastScanResponseTimeout = 10_000;
    }
}
