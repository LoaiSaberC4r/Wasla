using BuildingBlock.Application.Abstraction.Security;
using Microsoft.AspNetCore.Http;

namespace BuildingBlock.Api.Security
{
    internal sealed class HttpCurrentUser : ICurrentUser
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ITokenReader _tokenReader;
        private TokenInfoDto? _cachedTokenInfo;

        public HttpCurrentUser(IHttpContextAccessor httpContextAccessor, ITokenReader tokenReader)
        {
            _httpContextAccessor = httpContextAccessor;
            _tokenReader = tokenReader;
        }

        public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

        public Guid? UserId => TokenInfo.UserId;

        public string? UserName => TokenInfo.UserName;

        public string? Email => TokenInfo.Email;

        public IReadOnlyCollection<string> Roles => Array.AsReadOnly(TokenInfo.Roles);

        public IReadOnlyCollection<string> Permissions => Array.AsReadOnly(TokenInfo.Permissions);

        public string? GetClaimValue(string claimType)
        {
            if (string.IsNullOrWhiteSpace(claimType))
            {
                throw new ArgumentException("Claim type is required.", nameof(claimType));
            }

            var principal = AuthenticatedPrincipal;
            return principal is null || !_tokenReader.TryGetClaim(principal, claimType, out var value)
                ? null
                : value;
        }

        public IReadOnlyCollection<string> GetClaimValues(string claimType)
        {
            if (string.IsNullOrWhiteSpace(claimType))
            {
                throw new ArgumentException("Claim type is required.", nameof(claimType));
            }

            return Array.AsReadOnly(AuthenticatedPrincipal?.GetClaimValues(claimType).ToArray() ?? Array.Empty<string>());
        }

        private System.Security.Claims.ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

        private System.Security.Claims.ClaimsPrincipal? AuthenticatedPrincipal
        {
            get
            {
                var principal = Principal;
                return principal?.Identity?.IsAuthenticated == true ? principal : null;
            }
        }

        private TokenInfoDto TokenInfo
        {
            get
            {
                if (_cachedTokenInfo is not null)
                {
                    return _cachedTokenInfo;
                }

                var principal = AuthenticatedPrincipal;
                _cachedTokenInfo = principal is null ? new TokenInfoDto() : _tokenReader.ReadFromPrincipal(principal);
                return _cachedTokenInfo;
            }
        }
    }
}
