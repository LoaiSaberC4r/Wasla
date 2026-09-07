using BuildingBlock.Api.ProblemDetails;
using BuildingBlock.Domain.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;

namespace BuildingBlock.Api.Middleware
{
    internal sealed class ProblemDetailsStatusCodeMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IProblemDetailsMapper _mapper;

        public ProblemDetailsStatusCodeMiddleware(RequestDelegate next, IProblemDetailsMapper mapper)
        {
            _next = next;
            _mapper = mapper;
        }

        public async Task Invoke(HttpContext context)
        {
            await _next(context);

            if (context.Response.HasStarted ||
                context.Response.StatusCode < 400 ||
                context.Response.ContentLength > 0 ||
                !string.IsNullOrWhiteSpace(context.Response.ContentType))
            {
                return;
            }

            var error = MapStatusCode(context);
            if (error is null)
            {
                return;
            }

            var problem = _mapper.Map(context, new[] { error });
            var writer = context.RequestServices.GetRequiredService<IBuildingBlockProblemDetailsWriter>();
            await writer.WriteAsync(context, problem, context.RequestAborted);
        }

        private static Error? MapStatusCode(HttpContext context)
            => context.Response.StatusCode switch
            {
                StatusCodes.Status404NotFound => Error.NotFound(
                    ErrorCodes.Common.NotFound,
                    "The requested resource was not found.",
                    source: "HTTP"),
                StatusCodes.Status405MethodNotAllowed => Error.Domain(
                    ErrorCodes.Common.MethodNotAllowed,
                    "The HTTP method is not allowed for this resource.",
                    source: "HTTP"),
                StatusCodes.Status415UnsupportedMediaType => Error.Domain(
                    ErrorCodes.Common.UnsupportedMediaType,
                    "The request content type is not supported.",
                    source: "HTTP"),
                StatusCodes.Status429TooManyRequests => new Error(
                    ErrorCodes.Common.RateLimited,
                    "Too many requests.",
                    ErrorType.RateLimit,
                    Source: "HTTP",
                    RetryAfter: ReadRetryAfter(context.Response.Headers.RetryAfter)),
                _ => null
            };

        private static TimeSpan? ReadRetryAfter(string? retryAfter)
            => int.TryParse(retryAfter, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) && seconds > 0
                ? TimeSpan.FromSeconds(seconds)
                : null;
    }
}
