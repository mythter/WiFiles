using System.Collections.Concurrent;

namespace Server.Services
{
    public class ConnectionManager
    {
        private readonly ConcurrentDictionary<string, long> _connectionIds = new();
        private readonly ConcurrentDictionary<long, string> _sessionIds = new();

        private readonly SemaphoreSlim _semaphore = new(1, 1);

        private readonly Random _random = new();

        public string? GetBySessionId(long sessionId)
        {
            if (_sessionIds.TryGetValue(sessionId, out var connectionId))
            {
                return connectionId;
            }

            return null;
        }

        public long? GetByConnectionId(string connectionId)
        {
            if (_connectionIds.TryGetValue(connectionId, out var sessionId))
            {
                return sessionId;
            }

            return null;
        }

        public async Task<long> AddConnectionAsync(string connectionId)
        {
            long sessionId;

            await _semaphore.WaitAsync();

            do
            {
                sessionId = GenerateSessionId();
            } while (_sessionIds.ContainsKey(sessionId));

            _semaphore.Release();

            _connectionIds[connectionId] = sessionId;
            _sessionIds[sessionId] = connectionId;

            return sessionId;
        }

        public void RemoveByConnectionId(string connectionId)
        {
            if (_connectionIds.TryRemove(connectionId, out var id))
            {
                _sessionIds.TryRemove(id, out _);
            }
        }

        public void RemoveBySessionId(long sessionId)
        {
            if (_sessionIds.TryRemove(sessionId, out var connectionId))
            {
                _connectionIds.TryRemove(connectionId, out _);
            }
        }

        private long GenerateSessionId()
        {
            return _random.NextInt64(10_000_000_000, 99_999_999_999);
        }
    }
}
