using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace BuildingBlock.Application.Abstraction.Caching
{
    internal static class CacheKeyFactory
    {
        private const int PrefixMaxLength = 80;

        public static bool TryBuild(
            object request,
            ICacheableRequest cacheable,
            ICacheScopeValueProvider scopeValueProvider,
            out string key,
            out string? skipReason)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(cacheable);
            ArgumentNullException.ThrowIfNull(scopeValueProvider);

            key = string.Empty;
            skipReason = null;

            CacheScope scope;
            string? customCacheKey;
            try
            {
                scope = cacheable.Scope;
                customCacheKey = cacheable.CacheKey;
            }
            catch (Exception exception) when (!IsProcessCritical(exception))
            {
                skipReason = "cache-key-value-unreadable";
                return false;
            }

            if (!scopeValueProvider.TryGetScopeValue(scope, out var scopeValue) ||
                string.IsNullOrWhiteSpace(scopeValue))
            {
                skipReason = "cache-scope-unavailable";
                return false;
            }

            var requestType = request.GetType();
            var requestName = requestType.FullName ?? requestType.Name;
            var culture = CultureInfo.CurrentUICulture.Name;
            if (string.IsNullOrWhiteSpace(culture))
            {
                culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            }

            object?[] components =
            [
                requestName,
                culture,
                scope,
                scopeValue,
                customCacheKey,
                request
            ];

            if (!CanonicalCacheKeySerializer.TrySerialize(components, out var canonicalPayload, out skipReason))
            {
                return false;
            }

            var hash = Convert.ToHexString(
                    SHA256.HashData(Encoding.UTF8.GetBytes(canonicalPayload)))
                .ToLowerInvariant();
            var prefix = requestType.Name.Length <= PrefixMaxLength
                ? requestType.Name
                : requestType.Name[..PrefixMaxLength];

            key = $"bb:{prefix}:{hash}";
            return true;
        }

        private static bool IsProcessCritical(Exception exception)
            => exception is OutOfMemoryException or StackOverflowException or AccessViolationException ||
               exception.InnerException is not null && IsProcessCritical(exception.InnerException);
    }
}
