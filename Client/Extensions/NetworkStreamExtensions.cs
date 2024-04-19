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
            Task<int> readTask = stream.ReadAsync(buffer, 0 ,buffer.Length, cancellationToken);

            await Task.WhenAny(readTask, Task.Delay(timeout, cancellationToken));

            if (!readTask.IsCompletedSuccessfully)
            {
                throw new OperationCanceledException("Sender or you disconnected.");
            }

            return await readTask;
        }

        public static async Task WriteWithTimeoutAsync(
            this NetworkStream stream,
            byte[] buffer,
            int timeout,
            CancellationToken cancellationToken)
        {
            Task writeTask = stream.WriteAsync(buffer, 0, buffer.Length, cancellationToken);

            await Task.WhenAny(writeTask, Task.Delay(timeout, cancellationToken));

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
