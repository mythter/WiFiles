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

        public async Task AddAsync(string senderId, string receiverId)
        {
            await _semaphore.WaitAsync();
            try
            {
                _sessions.Add(new Session(senderId, receiverId));
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<bool> TryAddAsync(string senderId, string receiverId)
        {
            await _semaphore.WaitAsync();
            try
            {
                if (!_sessions.Exists(s => s.SenderConnectionId == senderId && s.ReceiverConnectionId == receiverId))
                {
                    _sessions.Add(new Session(senderId, receiverId));
                    return true;
                }
            }
            finally
            {
                _semaphore.Release();
            }

            return false;
        }

        public async Task RemoveBySenderAsync(string senderId)
        {
            await _semaphore.WaitAsync();
            try
            {
                _sessions.RemoveAll(s => s.SenderConnectionId == senderId);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task RemoveByReceiverAsync(string receiverId)
        {
            await _semaphore.WaitAsync();
            try
            {
                _sessions.RemoveAll(s => s.ReceiverConnectionId == receiverId);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task RemoveByConnectionIdAsync(string connectionId)
        {
            await _semaphore.WaitAsync();
            try
            {
                _sessions.RemoveAll(s => s.SenderConnectionId == connectionId || s.ReceiverConnectionId == connectionId);
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
