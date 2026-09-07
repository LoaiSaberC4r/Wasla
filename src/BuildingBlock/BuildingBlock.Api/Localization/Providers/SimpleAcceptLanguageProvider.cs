using BuildingBlock.Api.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using System.Globalization;

namespace BuildingBlock.Api.Localization.Providers
{
    internal sealed class SimpleAcceptLanguageProvider : RequestCultureProvider
    {
        private readonly IOptions<SharedLocalizationOptions> _options;

        public SimpleAcceptLanguageProvider(SharedLocalizationOptions options)
            : this(Microsoft.Extensions.Options.Options.Create(options))
        {
        }

        public SimpleAcceptLanguageProvider(IOptions<SharedLocalizationOptions> options)
        {
            _options = options;
        }

        public override Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
        {
            ArgumentNullException.ThrowIfNull(httpContext);

            if (!httpContext.Request.Headers.TryGetValue("Accept-Language", out StringValues values))
            {
                return Task.FromResult<ProviderCultureResult?>(null);
            }

            var localizationOptions = _options.Value;
            var supported = localizationOptions.SupportedCultures.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var requestedCultures = values.ToString()
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select((value, index) => ParseLanguage(value, index))
                .Where(item => item is not null)
                .Select(item => item!.Value)
                .OrderByDescending(item => item.Quality)
                .ThenBy(item => item.Index);

            foreach (var item in requestedCultures)
            {
                if (item.Tag == "*")
                {
                    return Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult(localizationOptions.DefaultCulture));
                }

                if (supported.Contains(item.Tag))
                {
                    return Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult(item.Tag));
                }

                var neutral = GetNeutralCulture(item.Tag);
                if (neutral is not null && supported.Contains(neutral))
                {
                    return Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult(neutral));
                }
            }

            return Task.FromResult<ProviderCultureResult?>(null);
        }

        private static (string Tag, double Quality, int Index)? ParseLanguage(string value, int index)
        {
            var parts = value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0 || string.IsNullOrWhiteSpace(parts[0]))
            {
                return null;
            }

            var tag = parts[0];
            var quality = 1.0;

            foreach (var parameter in parts.Skip(1))
            {
                if (!parameter.StartsWith("q=", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!double.TryParse(parameter[2..], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out quality) ||
                    quality is < 0 or > 1)
                {
                    return null;
                }
            }

            return quality <= 0 ? null : (tag, quality, index);
        }

        private static string? GetNeutralCulture(string tag)
        {
            try
            {
                var culture = CultureInfo.GetCultureInfo(tag);
                return culture.IsNeutralCulture ? culture.Name : culture.Parent.Name;
            }
            catch (CultureNotFoundException)
            {
                return null;
            }
        }
    }
}
