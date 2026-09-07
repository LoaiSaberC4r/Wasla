using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Caching;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BuildingBlock.Application.Behaviors
{
    internal sealed class QueryCacheBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull, IRequest<TResponse>
    {
        private static readonly TimeSpan DefaultTimeToLive = TimeSpan.FromMinutes(5);
        private static readonly Action<ILogger, string, string?, Exception?> CacheSkipped =
            LoggerMessage.Define<string, string?>(
                LogLevel.Warning,
                new EventId(1200, nameof(CacheSkipped)),
                "Cache skipped for request {RequestName}: {Reason}");

        private static readonly Action<ILogger, string, string, Exception?> CacheHit =
            LoggerMessage.Define<string, string>(
                LogLevel.Debug,
                new EventId(1201, nameof(CacheHit)),
                "Cache hit for request {RequestName} key {CacheKey}");

        private static readonly Action<ILogger, string, string, Exception?> CacheHitAfterLock =
            LoggerMessage.Define<string, string>(
                LogLevel.Debug,
                new EventId(1202, nameof(CacheHitAfterLock)),
                "Cache hit after lock for request {RequestName} key {CacheKey}");

        private static readonly string RequestName = typeof(TRequest).Name;

        private readonly ICacheService _cache;
        private readonly KeyedSemaphore _locks;
        private readonly ICacheScopeValueProvider _scopeValueProvider;
        private readonly ILogger<QueryCacheBehavior<TRequest, TResponse>> _log;

        public QueryCacheBehavior(
            ICacheService cache,
            KeyedSemaphore locks,
            ICacheScopeValueProvider scopeValueProvider,
            ILogger<QueryCacheBehavior<TRequest, TResponse>> log)
        {
            _cache = cache;
            _locks = locks;
            _scopeValueProvider = scopeValueProvider;
            _log = log;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            if (request is not ICacheableRequest cacheable)
            {
                return await next(cancellationToken);
            }

            if (!CacheKeyFactory.TryBuild(request!, cacheable, _scopeValueProvider, out var key, out var skipReason))
            {
                CacheSkipped(_log, RequestName, skipReason, null);
                return await next(cancellationToken);
            }

            var ttl = cacheable.TimeToLive ?? DefaultTimeToLive;
            var tags = (cacheable.Tags ?? Enumerable.Empty<string>())
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var (found, cached) = await _cache.TryGetAsync<TResponse>(key, cancellationToken);
            if (found)
            {
                CacheHit(_log, RequestName, key, null);
                return cached!;
            }

            using (await _locks.WaitAsync(key, cancellationToken))
            {
                (found, cached) = await _cache.TryGetAsync<TResponse>(key, cancellationToken);
                if (found)
                {
                    CacheHitAfterLock(_log, RequestName, key, null);
                    return cached!;
                }

                var response = await next(cancellationToken);
                if (ResultInspector.IsSuccess(response))
                {
                    await _cache.SetAsync(key, response, ttl, tags, cancellationToken);
                }

                return response;
            }
        }
    }
}
