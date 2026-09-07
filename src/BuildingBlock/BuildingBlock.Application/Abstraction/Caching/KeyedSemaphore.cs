namespace BuildingBlock.Application.Abstraction.Caching
{
    public sealed class KeyedSemaphore
    {
        private readonly object _gate = new();
        private readonly Dictionary<string, LockEntry> _locks = new(StringComparer.Ordinal);

        public async Task<IDisposable> WaitAsync(string key, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Lock key is required.", nameof(key));
            }

            LockEntry entry;
            lock (_gate)
            {
                if (!_locks.TryGetValue(key, out entry!))
                {
                    entry = new LockEntry();
                    _locks.Add(key, entry);
                }

                entry.ReferenceCount++;
            }

            try
            {
                await entry.Semaphore.WaitAsync(ct);
                return new Releaser(this, key, entry);
            }
            catch
            {
                ReleaseReference(key, entry, releaseSemaphore: false);
                throw;
            }
        }

        private void ReleaseReference(string key, LockEntry entry, bool releaseSemaphore)
        {
            if (releaseSemaphore)
            {
                entry.Semaphore.Release();
            }

            lock (_gate)
            {
                entry.ReferenceCount--;
                if (entry.ReferenceCount == 0 && entry.Semaphore.CurrentCount == 1)
                {
                    _locks.Remove(key);
                    entry.Semaphore.Dispose();
                }
            }
        }

        private sealed class LockEntry
        {
            public SemaphoreSlim Semaphore { get; } = new(1, 1);
            public int ReferenceCount { get; set; }
        }

        private sealed class Releaser : IDisposable
        {
            private readonly KeyedSemaphore _owner;
            private readonly string _key;
            private readonly LockEntry _entry;
            private bool _disposed;

            public Releaser(KeyedSemaphore owner, string key, LockEntry entry)
            {
                _owner = owner;
                _key = key;
                _entry = entry;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _owner.ReleaseReference(_key, _entry, releaseSemaphore: true);
            }
        }
    }
}
