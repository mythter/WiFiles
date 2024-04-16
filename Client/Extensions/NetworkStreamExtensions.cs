using System.Net.Sockets;

namespace Client.Extensions
{
    public static class NetworkStreamExtensions
    {
        public static async ValueTask<int> ReadWithTimeoutAsync(
            this NetworkStream stream, 
            byte[] buffer, 
            int timeout, 
            CancellationToken cancellationToken)
        {
            Task<int> result = stream.ReadAsync(buffer, 0 ,buffer.Length, cancellationToken);

            await Task.WhenAny(result, Task.Delay(timeout, cancellationToken));

            if (!result.IsCompleted)
            {
                //stream.Close();
                throw new OperationCanceledException("Sender was disconnected.");
            }

            return await result;
        }
    }
}
