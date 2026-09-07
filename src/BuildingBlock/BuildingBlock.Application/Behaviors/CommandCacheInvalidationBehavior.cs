using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Caching;
using BuildingBlock.Application.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace BuildingBlock.Application.Behaviors
{
    internal sealed class CommandCacheInvalidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull, IRequest<TResponse>
    {
        private static readonly Action<ILogger, string, int, Exception?> CacheInvalidated =
            LoggerMessage.Define<string, int>(
                LogLevel.Information,
                new EventId(1100, nameof(CacheInvalidated)),
                "Cache invalidated for command {CommandName} ({TagCount} tags).");

        private static readonly Action<ILogger, string, int, string?, Exception?> CacheInvalidationFailed =
            LoggerMessage.Define<string, int, string?>(
                LogLevel.Error,
                new EventId(1101, nameof(CacheInvalidationFailed)),
                "Cache invalidation failed after successful command {CommandName}. TagCount={TagCount} TraceId={TraceId}");

        private static readonly Action<ILogger, string, Exception?> CacheInvalidationSkipped =
            LoggerMessage.Define<string>(
                LogLevel.Warning,
                new EventId(1102, nameof(CacheInvalidationSkipped)),
                "Cache invalidation skipped for command {CommandName}: no tags were declared.");

        private static readonly string CommandName = typeof(TRequest).Name;

        private readonly ICacheService _cache;
        private readonly ILogger<CommandCacheInvalidationBehavior<TRequest, TResponse>> _log;

        public CommandCacheInvalidationBehavior(
            ICacheService cache,
            ILogger<CommandCacheInvalidationBehavior<TRequest, TResponse>> log)
            => (_cache, _log) = (cache, log);

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            var response = await next(cancellationToken);

            if (request is ICacheInvalidator invalidator &&
                ResultInspector.IsSuccess(response))
            {
                var tags = (invalidator.Tags ?? Enumerable.Empty<string>())
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                if (tags.Length > 0)
                {
                    try
                    {
                        await _cache.InvalidateByTagsAsync(tags, CancellationToken.None);
                        CacheInvalidated(_log, CommandName, tags.Length, null);
                    }
                    catch (Exception exception)
                    {
                        BuildingBlockDiagnostics.RecordCacheInvalidationFailure();
                        if (_log.IsEnabled(LogLevel.Error))
                        {
                            CacheInvalidationFailed(
                                _log,
                                CommandName,
                                tags.Length,
                                Activity.Current?.TraceId.ToString(),
                                exception);
                        }
                    }
                }
                else
                {
                    CacheInvalidationSkipped(_log, CommandName, null);
                }
            }

            return response;
        }
    }
}
