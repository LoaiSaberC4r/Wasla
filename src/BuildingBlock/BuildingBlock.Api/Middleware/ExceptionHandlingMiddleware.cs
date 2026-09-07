using BuildingBlock.Api.ProblemDetails;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace BuildingBlock.Api.Middleware
{
    internal sealed class ExceptionHandlingMiddleware
    {
        private static readonly Action<ILogger, PathString, string?, string, Exception?> UnhandledException =
            LoggerMessage.Define<PathString, string?, string>(
                LogLevel.Error,
                new EventId(3000, nameof(UnhandledException)),
                "Unhandled exception for path {RequestPath}. CorrelationId={CorrelationId} TraceId={TraceId}");

        private readonly RequestDelegate _next;
        private readonly IProblemDetailsMapper _problemDetailsMapper;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(
            RequestDelegate next,
            IProblemDetailsMapper problemDetailsMapper,
            ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _problemDetailsMapper = problemDetailsMapper;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
                var correlationId = context.Items.TryGetValue(CorrelationIdMiddleware.CorrelationItemKey, out var value)
                    ? value?.ToString()
                    : null;

                UnhandledException(
                    _logger,
                    context.Request.Path,
                    correlationId,
                    traceId,
                    exception);

                if (context.Response.HasStarted)
                {
                    throw;
                }

                var problem = _problemDetailsMapper.MapException(context, exception);
                var writer = context.RequestServices.GetRequiredService<IBuildingBlockProblemDetailsWriter>();
                context.Response.Clear();
                await writer.WriteAsync(context, problem, context.RequestAborted);
            }
        }
    }
}
