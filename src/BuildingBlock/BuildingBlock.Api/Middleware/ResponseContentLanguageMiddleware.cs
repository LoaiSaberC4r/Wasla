using Microsoft.AspNetCore.Http;
using System.Globalization;

namespace BuildingBlock.Api.Middleware
{
    internal sealed class ResponseContentLanguageMiddleware
    {
        private readonly RequestDelegate _next;

        public ResponseContentLanguageMiddleware(RequestDelegate next) => _next = next;

        public async Task Invoke(HttpContext context)
        {
            context.Response.OnStarting(() =>
            {
                var lang = CultureInfo.CurrentUICulture?.Name ?? "en";
                context.Response.Headers["Content-Language"] = lang;
                return Task.CompletedTask;
            });

            await _next(context);
        }
    }
}
