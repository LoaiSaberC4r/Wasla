using BuildingBlock.Api.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;

namespace BuildingBlock.Api.Logging
{
    internal sealed class RequestLogSanitizer
    {
        private static readonly HashSet<string> ForbiddenHeaders = new(StringComparer.OrdinalIgnoreCase)
        {
            "Authorization",
            "Cookie",
            "Set-Cookie",
            "Proxy-Authorization",
            "Proxy-Authenticate",
            "X-Api-Key",
            "Api-Key"
        };

        private readonly ILogRedactionPolicy _redactionPolicy;
        private readonly RequestLoggingOptions _options;

        public RequestLogSanitizer(
            ILogRedactionPolicy redactionPolicy,
            RequestLoggingOptions options)
        {
            _redactionPolicy = redactionPolicy;
            _options = options;
        }

        public bool IsExcluded(PathString path)
            => _options.ExcludedPaths.Any(excluded =>
                path.StartsWithSegments(new PathString(excluded), StringComparison.OrdinalIgnoreCase));

        public string NormalizePath(PathString path)
            => RedactText("RequestPath", path.HasValue ? path.Value! : "/");

        public object SanitizeQuery(QueryString queryString)
        {
            if (!queryString.HasValue)
            {
                return Array.Empty<object>();
            }

            try
            {
                var parsed = QueryHelpers.ParseQuery(queryString.Value);
                return parsed
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .ToDictionary(
                        pair => pair.Key,
                        pair => SanitizeValues(pair.Key, pair.Value),
                        StringComparer.Ordinal);
            }
            catch
            {
                return new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["malformed"] = "***"
                };
            }
        }

        public object SanitizeHeaders(IHeaderDictionary headers, IEnumerable<string> allowList)
        {
            var allowed = allowList
                .Where(header => !string.IsNullOrWhiteSpace(header))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return headers
                .Where(pair => allowed.Contains(pair.Key) && !ForbiddenHeaders.Contains(pair.Key))
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(
                    pair => pair.Key,
                    pair => SanitizeValues(pair.Key, pair.Value),
                    StringComparer.Ordinal);
        }

        public string RedactText(string propertyName, string value)
            => Limit(_redactionPolicy.Redact(propertyName, value).Value?.ToString() ?? string.Empty);

        private string[] SanitizeValues(string key, StringValues values)
            => values
                .Select(value => RedactText(key, value ?? string.Empty))
                .ToArray();

        private string Limit(string value)
        {
            if (value.Length <= _options.MaximumValueLength)
            {
                return value;
            }

            const string suffix = "...[truncated]";
            var prefixLength = Math.Max(0, _options.MaximumValueLength - suffix.Length);
            return value[..prefixLength] + suffix;
        }
    }
}
