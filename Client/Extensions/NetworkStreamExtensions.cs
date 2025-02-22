﻿using System.Net.Sockets;
using System.Text;

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

            try
            {
                return await stream.ReadAsync(buffer, linkedCts.Token);
            }
            catch (OperationCanceledException)
            {
                if (readCts.IsCancellationRequested)
                {
                    throw new TimeoutException();
                }

                throw;
            }
        }

        public static async Task WriteWithTimeoutAsync(
            this NetworkStream stream,
            byte[] buffer,
            int timeout,
            CancellationToken cancellationToken)
        {
            using var writeCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeout));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(writeCts.Token, cancellationToken);

            try
            {
                await stream.WriteAsync(buffer, linkedCts.Token);
            }
            catch (OperationCanceledException)
            {
                if (writeCts.IsCancellationRequested)
                {
                    throw new TimeoutException();
                }

                throw;
            }
        }

        public static async Task<T> ReadAsync<T>(
            this NetworkStream stream,
            Func<byte[], T> converter,
            int size,
            CancellationToken cancellationToken = default)
        {
            byte[] buffer = new byte[size];
            await stream.ReadExactlyAsync(buffer, 0, size, cancellationToken);
            return converter(buffer);
        }

        public static async Task<bool> ReadBooleanAsync(
            this NetworkStream stream,
            CancellationToken cancellationToken = default)
        {
            return await stream.ReadAsync(
                (bytes) => BitConverter.ToBoolean(bytes, 0),
                sizeof(bool),
                cancellationToken);
        }

        public static async Task<int> ReadInt32Async(
            this NetworkStream stream,
            CancellationToken cancellationToken = default)
        {
            return await stream.ReadAsync(
                (bytes) => BitConverter.ToInt32(bytes, 0),
                sizeof(int),
                cancellationToken);
        }

        public static async Task<string> ReadStringAsync(
            this NetworkStream stream,
            int size,
            CancellationToken cancellationToken = default)
        {
            return await stream.ReadAsync(Encoding.UTF8.GetString, size, cancellationToken);
        }

        public static async Task WriteBooleanAsync(
            this NetworkStream stream,
            bool value,
            CancellationToken cancellationToken = default)
        {
            await stream.WriteAsync(
                BitConverter.GetBytes(value),
                0,
                sizeof(bool),
                cancellationToken);
        }
    }
}
