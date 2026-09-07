using Microsoft.AspNetCore.Http;
using Serilog.Context;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace BuildingBlock.Api.Middleware
{
    internal sealed class CorrelationIdMiddleware
    {
        public const string CorrelationHeader = "X-Correlation-Id";
        public const string CorrelationItemKey = "__CorrelationId";

        private static readonly Regex SafeCorrelationId = new(
            "^[A-Za-z0-9_-]{1,64}$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));

        private readonly RequestDelegate _next;

        public CorrelationIdMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            var correlationId = GetSafeCorrelationId(context);
            context.Items[CorrelationItemKey] = correlationId;
            context.Response.OnStarting(() =>
            {
                context.Response.Headers[CorrelationHeader] = correlationId;
                return Task.CompletedTask;
            });

            var createdActivity = false;
            var activity = Activity.Current;
            if (activity is null)
            {
                activity = new Activity("IncomingRequest");
                activity.Start();
                createdActivity = true;
            }

            using (LogContext.PushProperty("CorrelationId", correlationId))
            using (LogContext.PushProperty("TraceId", activity.TraceId.ToString()))
            using (LogContext.PushProperty("SpanId", activity.SpanId.ToString()))
            {
                try
                {
                    await _next(context);
                }
                finally
                {
                    if (createdActivity)
                    {
                        activity.Stop();
                    }
                }
            }
        }

        private static string GetSafeCorrelationId(HttpContext context)
        {
            if (!context.Request.Headers.TryGetValue(CorrelationHeader, out var values) || values.Count != 1)
            {
                return Guid.NewGuid().ToString("N");
            }

            var candidate = values[0]?.Trim();
            return !string.IsNullOrWhiteSpace(candidate) && SafeCorrelationId.IsMatch(candidate)
                ? candidate
                : Guid.NewGuid().ToString("N");
        }
    }
}
