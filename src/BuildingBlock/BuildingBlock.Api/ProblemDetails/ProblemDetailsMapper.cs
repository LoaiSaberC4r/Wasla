using BuildingBlock.Api.Middleware;
using BuildingBlock.Domain.Results;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using System.Globalization;
using System.Net;

namespace BuildingBlock.Api.ProblemDetails
{
    internal sealed class ProblemDetailsMapper : IProblemDetailsMapper
    {
        private const int MaxDetailLength = 1000;
        private readonly IWebHostEnvironment? _environment;

        public ProblemDetailsMapper(IWebHostEnvironment? environment = null)
        {
            _environment = environment;
        }

        public Microsoft.AspNetCore.Mvc.ProblemDetails Map(HttpContext httpContext, IReadOnlyCollection<Error> errors)
        {
            ArgumentNullException.ThrowIfNull(httpContext);
            ArgumentNullException.ThrowIfNull(errors);

            var safeErrors = errors.Count == 0
                ? new[] { Error.Unknown(ErrorCodes.Common.Unknown, "An unknown error occurred.") }
                : errors;
            var primary = SelectPrimaryError(safeErrors);
            var (status, typeUri, title) = Map(primary);

            var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Type = typeUri,
                Title = title,
                Status = (int)status,
                Detail = GetSafeDetail(primary),
                Instance = httpContext.Request.Path
            };

            AddCommonExtensions(problem, httpContext);
            problem.Extensions["errors"] = safeErrors.Select(ToErrorPayload).ToArray();

            if (status == HttpStatusCode.TooManyRequests && primary.RetryAfter is { } retryAfter)
            {
                httpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
            }

            return problem;
        }

        public Microsoft.AspNetCore.Mvc.ProblemDetails MapException(HttpContext httpContext, Exception exception)
        {
            ArgumentNullException.ThrowIfNull(httpContext);
            ArgumentNullException.ThrowIfNull(exception);

            var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Type = "about:blank#unknown",
                Title = "Unexpected Error",
                Status = StatusCodes.Status500InternalServerError,
                Detail = _environment?.IsDevelopment() == true
                    ? Truncate(exception.ToString())
                    : "An unexpected error occurred.",
                Instance = httpContext.Request.Path
            };

            AddCommonExtensions(problem, httpContext);
            problem.Extensions["errors"] = new[]
            {
                ToErrorPayload(Error.Unknown(ErrorCodes.Common.Unknown, "An unexpected error occurred.", source: "Exception"))
            };

            return problem;
        }

        private static Error SelectPrimaryError(IReadOnlyCollection<Error> errors)
            => errors
                .OrderBy(error => GetPrecedence(error.Type))
                .First();

        private static int GetPrecedence(ErrorType type) => type switch
        {
            ErrorType.Unknown => 0,
            ErrorType.Infrastructure => 1,
            ErrorType.Unauthorized => 2,
            ErrorType.Security => 3,
            ErrorType.RateLimit => 4,
            ErrorType.Conflict => 5,
            ErrorType.NotFound => 6,
            ErrorType.Validation => 7,
            ErrorType.Domain => 8,
            _ => 9
        };

        private static (HttpStatusCode Status, string TypeUri, string Title) Map(Error error)
        {
            if (error.Code == ErrorCodes.Common.MethodNotAllowed)
            {
                return (HttpStatusCode.MethodNotAllowed, "about:blank#method-not-allowed", "Method Not Allowed");
            }

            if (error.Code == ErrorCodes.Common.UnsupportedMediaType)
            {
                return (HttpStatusCode.UnsupportedMediaType, "about:blank#unsupported-media-type", "Unsupported Media Type");
            }

            return Map(error.Type);
        }

        private static (HttpStatusCode Status, string TypeUri, string Title) Map(ErrorType type) => type switch
        {
            ErrorType.Validation => (HttpStatusCode.UnprocessableEntity, "about:blank#validation", "Validation Error"),
            ErrorType.Domain => (HttpStatusCode.UnprocessableEntity, "about:blank#domain", "Domain Error"),
            ErrorType.NotFound => (HttpStatusCode.NotFound, "about:blank#not-found", "Resource Not Found"),
            ErrorType.Conflict => (HttpStatusCode.Conflict, "about:blank#conflict", "Conflict"),
            ErrorType.Unauthorized => (HttpStatusCode.Unauthorized, "about:blank#unauthorized", "Unauthorized"),
            ErrorType.Security => (HttpStatusCode.Forbidden, "about:blank#security", "Security Error"),
            ErrorType.RateLimit => (HttpStatusCode.TooManyRequests, "about:blank#rate-limit", "Too Many Requests"),
            ErrorType.Infrastructure => (HttpStatusCode.ServiceUnavailable, "about:blank#infrastructure", "Infrastructure Error"),
            _ => (HttpStatusCode.InternalServerError, "about:blank#unknown", "Unexpected Error")
        };

        private static void AddCommonExtensions(Microsoft.AspNetCore.Mvc.ProblemDetails problem, HttpContext httpContext)
        {
            problem.Extensions["traceId"] = Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;

            if (httpContext.Items.TryGetValue(CorrelationIdMiddleware.CorrelationItemKey, out var correlationId) &&
                !string.IsNullOrWhiteSpace(correlationId?.ToString()))
            {
                problem.Extensions["correlationId"] = correlationId.ToString();
            }
        }

        private ProblemDetailsErrorPayload ToErrorPayload(Error error)
        {
            var expose = ShouldExpose(error);
            return new ProblemDetailsErrorPayload(
                error.Code,
                expose ? Truncate(error.Message) : "The request could not be completed.",
                error.Type.ToString(),
                expose && !string.IsNullOrWhiteSpace(error.Details) ? Truncate(error.Details) : null,
                expose ? error.Source : null,
                error.RetryAfter?.TotalSeconds);
        }

        private string GetSafeDetail(Error error)
            => ShouldExpose(error)
                ? Truncate(error.Message)
                : "The request could not be completed.";

        private bool ShouldExpose(Error error)
            => error.Type is not (ErrorType.Infrastructure or ErrorType.Unknown) ||
               _environment?.IsDevelopment() == true;

        private static string Truncate(string value)
            => value.Length <= MaxDetailLength ? value : value[..MaxDetailLength];
    }
}
