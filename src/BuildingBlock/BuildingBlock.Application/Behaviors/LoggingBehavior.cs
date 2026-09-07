using BuildingBlock.Domain.Results;
using BuildingBlock.Application.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace BuildingBlock.Application.Behaviors
{
    internal sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private static readonly Action<ILogger, string, long, string, ErrorType, string?, Exception?> RequestCompletedWithFailure =
            LoggerMessage.Define<string, long, string, ErrorType, string?>(
                LogLevel.Warning,
                new EventId(1000, nameof(RequestCompletedWithFailure)),
                "Request {RequestName} completed with failure in {ElapsedMs} ms. ErrorCode={ErrorCode} ErrorType={ErrorType} TraceId={TraceId}");

        private static readonly Action<ILogger, string, long, string?, Exception?> RequestCompleted =
            LoggerMessage.Define<string, long, string?>(
                LogLevel.Debug,
                new EventId(1001, nameof(RequestCompleted)),
                "Request {RequestName} completed in {ElapsedMs} ms. TraceId={TraceId}");

        private static readonly string RequestName = typeof(TRequest).Name;

        private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

        public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
        {
            _logger = logger;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();

            var response = await next(cancellationToken);
            stopwatch.Stop();

            if (ResultDiagnostics.TryInspect(response, out var result) && result.IsFailure)
            {
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    var primary = result.Errors.Count > 0
                        ? result.Errors[0]
                        : Error.Unknown("Unknown", "Unknown failure.");

                    RequestCompletedWithFailure(
                        _logger,
                        RequestName,
                        stopwatch.ElapsedMilliseconds,
                        primary.Code,
                        primary.Type,
                        Activity.Current?.TraceId.ToString(),
                        null);
                }
            }
            else if (_logger.IsEnabled(LogLevel.Debug))
            {
                RequestCompleted(
                    _logger,
                    RequestName,
                    stopwatch.ElapsedMilliseconds,
                    Activity.Current?.TraceId.ToString(),
                    null);
            }

            return response;
        }

    }
}
