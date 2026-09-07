using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace BuildingBlock.Infrastructure.Service
{
    internal sealed class MemoryCacheService : ICacheService, IDisposable
    {
        private static readonly Action<ILogger, Exception?> CacheHit =
            LoggerMessage.Define(
                LogLevel.Debug,
                new EventId(2400, nameof(CacheHit)),
                "Cache hit for tracked memory cache entry.");

        private static readonly Action<ILogger, int, int, Exception?> CacheInvalidated =
            LoggerMessage.Define<int, int>(
                LogLevel.Information,
                new EventId(2401, nameof(CacheInvalidated)),
                "Cache invalidated for {TagCount} tags ({KeyCount} keys).");

        private static readonly Action<ILogger, int, Exception?> CacheCleared =
            LoggerMessage.Define<int>(
                LogLevel.Warning,
                new EventId(2402, nameof(CacheCleared)),
                "Tracked memory cache entries were cleared. Removed {Count} keys.");

        private readonly IMemoryCache _cache;
        private readonly ILogger<MemoryCacheService> _logger;
        private readonly object _gate = new();
        private readonly SemaphoreSlim _mutationGate = new(1, 1);
        private readonly Dictionary<string, CacheEntryRegistration> _registrations =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<string>> _tagIndex =
            new(StringComparer.OrdinalIgnoreCase);
        private long _nextVersion;

        public MemoryCacheService(
            IMemoryCache cache,
            ILogger<MemoryCacheService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public void Dispose()
            => _mutationGate.Dispose();

        public Task<(bool found, T? value)> TryGetAsync<T>(string key, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var normalizedKey = NormalizeKey(key);

            if (_cache.TryGetValue(normalizedKey, out var value) && value is T typedValue)
            {
                BuildingBlockDiagnostics.RecordCacheHit();
                CacheHit(_logger, null);
                return Task.FromResult<(bool found, T? value)>((true, typedValue));
            }

            BuildingBlockDiagnostics.RecordCacheMiss();
            return Task.FromResult<(bool found, T? value)>((false, default));
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan ttl, IEnumerable<string> tags, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            if (ttl <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(ttl), "Cache TTL must be positive.");
            }

            var normalizedKey = NormalizeKey(key);
            var normalizedTags = NormalizeTags(tags).ToArray();
            var version = Interlocked.Increment(ref _nextVersion);

            var options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl
            };
            options.RegisterPostEvictionCallback((evictedKey, _, _, state) =>
            {
                if (state is EvictionRegistration registration && evictedKey is string cacheKey)
                {
                    registration.Service.RemoveIndexesForEviction(cacheKey, registration.Version);
                }
            }, new EvictionRegistration(this, version));

            await _mutationGate.WaitAsync(ct);
            try
            {
                ct.ThrowIfCancellationRequested();

                lock (_gate)
                {
                    RemoveRegistrationNoLock(normalizedKey);
                    AddRegistrationNoLock(normalizedKey, new CacheEntryRegistration(version, normalizedTags));
                }

                try
                {
                    _cache.Set(normalizedKey, value!, options);
                }
                catch
                {
                    RemoveRegistrationForVersion(normalizedKey, version);
                    throw;
                }
            }
            finally
            {
                _mutationGate.Release();
            }

            BuildingBlockDiagnostics.RecordCacheSet();
        }

        public async Task RemoveAsync(string key, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var normalizedKey = NormalizeKey(key);

            await _mutationGate.WaitAsync(ct);
            try
            {
                ct.ThrowIfCancellationRequested();

                lock (_gate)
                {
                    RemoveRegistrationNoLock(normalizedKey);
                }

                _cache.Remove(normalizedKey);
            }
            finally
            {
                _mutationGate.Release();
            }
        }

        public async Task InvalidateByTagsAsync(IEnumerable<string> tags, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(tags);
            ct.ThrowIfCancellationRequested();
            var normalizedTags = NormalizeTags(tags).ToArray();

            await _mutationGate.WaitAsync(ct);
            HashSet<string> affectedKeys;
            try
            {
                ct.ThrowIfCancellationRequested();

                lock (_gate)
                {
                    affectedKeys = DetachRegistrationsForTagsNoLock(normalizedTags);
                }

                // Cancellation is deliberately ignored after the atomic index transition.
                // Completing all removals keeps IMemoryCache and both indexes consistent.
                foreach (var key in affectedKeys)
                {
                    _cache.Remove(key);
                }
            }
            finally
            {
                _mutationGate.Release();
            }

            if (normalizedTags.Length > 0)
            {
                BuildingBlockDiagnostics.RecordCacheInvalidation();
                CacheInvalidated(_logger, normalizedTags.Length, affectedKeys.Count, null);
            }
        }

        /// <summary>
        /// Removes every entry tracked by this service. Entries owned by other consumers of the
        /// shared <see cref="IMemoryCache"/> are outside the scope of this operation.
        /// </summary>
        public async Task ClearAllAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            await _mutationGate.WaitAsync(ct);
            string[] keys;
            try
            {
                ct.ThrowIfCancellationRequested();

                lock (_gate)
                {
                    keys = _registrations.Keys.ToArray();
                    _registrations.Clear();
                    _tagIndex.Clear();
                }

                // Cancellation is deliberately ignored after the atomic index transition.
                foreach (var key in keys)
                {
                    _cache.Remove(key);
                }
            }
            finally
            {
                _mutationGate.Release();
            }

            CacheCleared(_logger, keys.Length, null);
        }

        private HashSet<string> DetachRegistrationsForTagsNoLock(IReadOnlyCollection<string> tags)
        {
            var affectedKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var tag in tags)
            {
                if (!_tagIndex.TryGetValue(tag, out var keys))
                {
                    continue;
                }

                foreach (var key in keys.ToArray())
                {
                    if (_registrations.TryGetValue(key, out var registration) &&
                        registration.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
                    {
                        affectedKeys.Add(key);
                    }
                }
            }

            foreach (var key in affectedKeys)
            {
                RemoveRegistrationNoLock(key);
            }

            return affectedKeys;
        }

        private void RemoveRegistrationForVersion(string key, long version)
        {
            lock (_gate)
            {
                if (_registrations.TryGetValue(key, out var registration) &&
                    registration.Version == version)
                {
                    RemoveRegistrationNoLock(key);
                }
            }
        }

        private void RemoveIndexesForEviction(string key, long version)
        {
            lock (_gate)
            {
                if (!_registrations.TryGetValue(key, out var registration) ||
                    registration.Version != version)
                {
                    return;
                }

                RemoveRegistrationNoLock(key);
            }
        }

        private void AddRegistrationNoLock(string key, CacheEntryRegistration registration)
        {
            _registrations[key] = registration;

            foreach (var tag in registration.Tags)
            {
                if (!_tagIndex.TryGetValue(tag, out var keys))
                {
                    keys = new HashSet<string>(StringComparer.Ordinal);
                    _tagIndex.Add(tag, keys);
                }

                keys.Add(key);
            }
        }

        private void RemoveRegistrationNoLock(string key)
        {
            if (!_registrations.Remove(key, out var registration))
            {
                return;
            }

            foreach (var tag in registration.Tags)
            {
                if (!_tagIndex.TryGetValue(tag, out var keys))
                {
                    continue;
                }

                keys.Remove(key);
                if (keys.Count == 0)
                {
                    _tagIndex.Remove(tag);
                }
            }
        }

        private static string NormalizeKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Cache key is required.", nameof(key));
            }

            return key.Trim();
        }

        private static IEnumerable<string> NormalizeTags(IEnumerable<string>? tags)
            => (tags ?? Enumerable.Empty<string>())
                .Select(tag => tag?.Trim())
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Select(tag => tag!.ToLowerInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase);

        private sealed record CacheEntryRegistration(long Version, IReadOnlyCollection<string> Tags);

        private sealed record EvictionRegistration(MemoryCacheService Service, long Version);
    }
}
