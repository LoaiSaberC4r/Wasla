using BuildingBlock.Api.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Options;

namespace BuildingBlock.Api.Localization.Providers
{
    internal sealed class SupportedQueryStringRequestCultureProvider : RequestCultureProvider
    {
        private readonly IOptions<SharedLocalizationOptions> _options;

        public SupportedQueryStringRequestCultureProvider(IOptions<SharedLocalizationOptions> options)
        {
            _options = options;
        }

        public string QueryStringKey { get; init; } = "lang";

        public override Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
        {
            ArgumentNullException.ThrowIfNull(httpContext);

            var requested = httpContext.Request.Query[QueryStringKey].ToString();
            if (string.IsNullOrWhiteSpace(requested))
            {
                return Task.FromResult<ProviderCultureResult?>(null);
            }

            var supported = _options.Value.SupportedCultures;
            return supported.Contains(requested, StringComparer.OrdinalIgnoreCase)
                ? Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult(requested))
                : Task.FromResult<ProviderCultureResult?>(null);
        }
    }
}
