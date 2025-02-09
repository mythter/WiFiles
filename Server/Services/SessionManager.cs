using Server.Models;

namespace Server.Services
{
    public class SessionManager
    {
        private readonly List<Session> _sessions = [];

        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public IReadOnlyList<Session> Sessions => _sessions.AsReadOnly();

        public async Task LockAsync()
        {
            await _semaphore.WaitAsync();
        }

        public void Release()
        {
            _semaphore.Release();
        }

        public async Task<Session?> GetBySenderConnectionId(string senderConnectionId)
        {
            return await ReturnWithLockAsync(() =>
            {
                return _sessions.SingleOrDefault(s => s.SenderConnectionId == senderConnectionId);
            });
        }

        public async Task<Session?> GetByReceiverConnectionIdAsync(string receiverSessionId)
        {
            return await ReturnWithLockAsync(() =>
            {
                return _sessions.SingleOrDefault(s => s.ReceiverConnectionId == receiverSessionId);
            });
        }

        public async Task<Session?> GetBySenderAndReceiverConnectionIdAsync(string senderConnectionId, string receiverSessionId)
        {
            return await ReturnWithLockAsync(() =>
            {
                return _sessions.SingleOrDefault(s => s.SenderConnectionId == senderConnectionId && s.ReceiverConnectionId == receiverSessionId);
            });
        }

        public async Task AddAsync(string senderId, string receiverId)
        {
            await DoWithLockAsync(() =>
            {
                _sessions.Add(new Session(senderId, receiverId));
            });
        }

        public async Task<bool> TryAddAsync(string senderId, string receiverId)
        {
            return await ReturnWithLockAsync(() =>
            {
                if (!_sessions.Exists(s => s.SenderConnectionId == senderId && s.ReceiverConnectionId == receiverId))
                {
                    _sessions.Add(new Session(senderId, receiverId));
                    return true;
                }
                return false;
            });
        }

        public async Task RemoveBySenderAsync(string senderId)
        {
            await DoWithLockAsync(() =>
            {
                _sessions.RemoveAll(s => s.SenderConnectionId == senderId);
            });
        }

        public async Task RemoveByReceiverAsync(string receiverId)
        {
            await DoWithLockAsync(() =>
            {
                _sessions.RemoveAll(s => s.ReceiverConnectionId == receiverId);
            });
        }

        public async Task RemoveByConnectionIdAsync(string connectionId)
        {
            await DoWithLockAsync(() =>
            {
                _sessions.RemoveAll(s => s.SenderConnectionId == connectionId || s.ReceiverConnectionId == connectionId);
            });
        }

        public async Task Remove(Session session)
        {
            await DoWithLockAsync(() =>
            {
                _sessions.Remove(session);
            });
        }

        private async Task DoWithLockAsync(Action action)
        {
            await _semaphore.WaitAsync();
            try
            {
                action();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task<T> ReturnWithLockAsync<T>(Func<T> action)
        {
            await _semaphore.WaitAsync();
            try
            {
                return action();
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
