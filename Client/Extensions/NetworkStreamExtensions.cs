using System.Net.Sockets;

namespace Client.Extensions
{
    public static class NetworkStreamExtensions
    {
        public static async Task<int> ReadWithTimeoutAsync(
            this NetworkStream stream, 
            byte[] buffer, 
            int timeout, 
            CancellationToken cancellationToken)
        {
            using var readCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeout));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(readCts.Token, cancellationToken);

            return await stream.ReadAsync(buffer, linkedCts.Token);
            }

            return await readTask;
        }

        public static async Task WriteWithTimeoutAsync(
            this NetworkStream stream,
            byte[] buffer,
            int timeout,
            CancellationToken cancellationToken)
        {
            using var writeCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeout));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(writeCts.Token, cancellationToken);

            await stream.WriteAsync(buffer, linkedCts.Token);

            if (!writeTask.IsCompletedSuccessfully)
            {
                if (writeTask.Exception?.InnerException?.InnerException is SocketException ex)
                {
                    throw ex;
                }

                throw new OperationCanceledException("Receiver or you disconnected.");
            }

            await writeTask;
        }
    }
}
