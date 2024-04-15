using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Client.Extensions
{
    public static class NetworkStreamExtensions
    {
        public static async ValueTask<int> ReadWithTimeoutAsync(this NetworkStream stream, byte[] buffer, int timeout)
        {
            Task<int> result = stream.ReadAsync(buffer, 0 ,buffer.Length);

            await Task.WhenAny(result, Task.Delay(timeout));

            if (!result.IsCompleted)
            {
                stream.Close();
                throw new OperationCanceledException("Sender was disconnected.");
            }

            return await result;
        }
    }
}
