using BuildingBlock.Application.Abstraction.Security;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Security.Claims;

namespace BuildingBlock.Infrastructure.Service
{
    /// <summary>
    /// Reads claims from a supplied principal. Authentication boundaries are enforced by callers such as HttpCurrentUser.
    /// </summary>
    internal sealed class TokenReader : ITokenReader
    {
        private const string SubjectClaimType = "sub";
        private const string NameClaimType = "name";
        private const string EmailClaimType = "email";
        private const string IssuerClaimType = "iss";
        private const string AudienceClaimType = "aud";
        private const string JwtIdClaimType = "jti";
        private const string IssuedAtClaimType = "iat";
        private const string ExpiresAtClaimType = "exp";
        private static readonly char[] CommaSeparators = { ',' };
        private static readonly char[] ScopeSeparators = { ' ', ',' };
        private readonly CurrentUserClaimOptions _options;

        public TokenReader(IOptions<CurrentUserClaimOptions> options)
        {
            ArgumentNullException.ThrowIfNull(options);
            _options = options.Value;
        }

        public TokenInfoDto ReadFromPrincipal(ClaimsPrincipal principal)
        {
            ArgumentNullException.ThrowIfNull(principal);

            return new TokenInfoDto
            {
                UserId = TryGetGuid(principal, ClaimCandidates(
                    _options.UserIdClaimType,
                    JwtClaimTypesCustom.UserId,
                    ClaimTypes.NameIdentifier,
                    SubjectClaimType,
                    "sub",
                    "nameid")),
                UserName = TryGetString(principal, ClaimCandidates(
                    _options.UserNameClaimType,
                    JwtClaimTypesCustom.UserName,
                    ClaimTypes.Name,
                    NameClaimType,
                    "name",
                    "username",
                    "preferred_username")),
                Email = TryGetString(principal, ClaimCandidates(
                    _options.EmailClaimType,
                    JwtClaimTypesCustom.Email,
                    ClaimTypes.Email,
                    EmailClaimType,
                    "email")),
                Roles = GetDistinctClaimValues(
                    principal,
                    ClaimCandidates(
                        _options.RoleClaimType,
                        JwtClaimTypesCustom.Role,
                        ClaimTypes.Role,
                        "role",
                        "roles"),
                    splitSpaceSeparated: false,
                    maximumValues: _options.MaximumRoles),
                Permissions = GetDistinctClaimValues(
                    principal,
                    ClaimCandidates(
                        _options.PermissionClaimType,
                        JwtClaimTypesCustom.Permission,
                        "permission",
                        "permissions",
                        "scope",
                        "scp"),
                    splitSpaceSeparated: true,
                    maximumValues: _options.MaximumPermissions),
                Issuer = TryGetString(principal, IssuerClaimType),
                Audience = TryGetString(principal, AudienceClaimType),
                Subject = TryGetString(principal, SubjectClaimType),
                JwtId = TryGetString(principal, JwtIdClaimType),
                IssuedAtUtc = TryGetDateTimeUtcFromEpoch(principal, IssuedAtClaimType),
                ExpiresAtUtc = TryGetDateTimeUtcFromEpoch(principal, ExpiresAtClaimType)
            };
        }

        public bool TryGetClaim(ClaimsPrincipal principal, string claimType, out string? value)
        {
            ArgumentNullException.ThrowIfNull(principal);
            if (string.IsNullOrWhiteSpace(claimType))
            {
                throw new ArgumentException("Claim type is required.", nameof(claimType));
            }

            value = GetClaimValueAny(principal, claimType);
            return !string.IsNullOrWhiteSpace(value);
        }

        public (bool ok, T? value) TryGet<T>(ClaimsPrincipal principal, string claimType)
        {
            ArgumentNullException.ThrowIfNull(principal);
            if (string.IsNullOrWhiteSpace(claimType))
            {
                throw new ArgumentException("Claim type is required.", nameof(claimType));
            }

            var raw = GetClaimValueAny(principal, claimType);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return (false, default);
            }

            return TryConvert<T>(raw, out var value) ? (true, value) : (false, default);
        }

        private static string[] ClaimCandidates(params string?[] claimTypes)
            => claimTypes
                .Where(claimType => !string.IsNullOrWhiteSpace(claimType))
                .Select(claimType => claimType!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

        private string? GetClaimValueAny(ClaimsPrincipal principal, params string[] claimTypes)
        {
            var claimTypeSet = new HashSet<string>(claimTypes, StringComparer.OrdinalIgnoreCase);
            var claim = principal.Claims.FirstOrDefault(candidate =>
                claimTypeSet.Contains(candidate.Type) &&
                IsUsableClaimValue(candidate.Value));

            return string.IsNullOrWhiteSpace(claim?.Value) ? null : claim.Value;
        }

        private string[] GetDistinctClaimValues(
            ClaimsPrincipal principal,
            string[] claimTypes,
            bool splitSpaceSeparated,
            int maximumValues)
        {
            var claimTypeSet = new HashSet<string>(claimTypes, StringComparer.OrdinalIgnoreCase);
            var separators = splitSpaceSeparated ? ScopeSeparators : CommaSeparators;

            var values = principal.Claims
                .Where(claim => claimTypeSet.Contains(claim.Type) && IsUsableClaimValue(claim.Value))
                .SelectMany(claim => SplitClaimValue(claim.Value, separators))
                .Where(value => !string.IsNullOrWhiteSpace(value) && value.Length <= _options.MaximumClaimValueLength)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return values.Length <= maximumValues ? values : Array.Empty<string>();
        }

        private bool IsUsableClaimValue(string? value)
            => !string.IsNullOrWhiteSpace(value) && value.Length <= _options.MaximumClaimValueLength;

        private static string[] SplitClaimValue(string value, char[] separators)
            => value.Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        private string? TryGetString(ClaimsPrincipal principal, params string[] claimTypes)
            => GetClaimValueAny(principal, claimTypes);

        private Guid? TryGetGuid(ClaimsPrincipal principal, params string[] claimTypes)
        {
            var raw = GetClaimValueAny(principal, claimTypes);
            return Guid.TryParse(raw, out var value) && value != Guid.Empty ? value : null;
        }

        private DateTime? TryGetDateTimeUtcFromEpoch(ClaimsPrincipal principal, string claimType)
        {
            var raw = GetClaimValueAny(principal, claimType);
            if (!long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
            {
                return null;
            }

            try
            {
                return DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        private static bool TryConvert<T>(string raw, out T? value)
        {
            value = default;
            var targetType = typeof(T);
            var nonNullableType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            try
            {
                object? converted;
                if (nonNullableType == typeof(string))
                {
                    converted = raw;
                }
                else if (nonNullableType == typeof(Guid))
                {
                    if (!Guid.TryParse(raw, out var guid))
                    {
                        return false;
                    }

                    converted = guid;
                }
                else if (nonNullableType.IsEnum)
                {
                    converted = Enum.Parse(nonNullableType, raw, ignoreCase: true);
                }
                else if (nonNullableType == typeof(DateTime))
                {
                    converted = long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
                        ? DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime
                        : DateTime.Parse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
                }
                else if (nonNullableType == typeof(DateTimeOffset))
                {
                    converted = long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
                        ? DateTimeOffset.FromUnixTimeSeconds(seconds)
                        : DateTimeOffset.Parse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
                }
                else
                {
                    converted = Convert.ChangeType(raw, nonNullableType, CultureInfo.InvariantCulture);
                }

                value = (T?)converted;
                return true;
            }
            catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException or ArgumentException)
            {
                return false;
            }
        }
    }
}
