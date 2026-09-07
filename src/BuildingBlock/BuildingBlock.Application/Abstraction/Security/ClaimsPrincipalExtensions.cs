using System.Security.Claims;

namespace BuildingBlock.Application.Abstraction.Security
{
    public static class ClaimsPrincipalExtensions
    {
        public static string? GetClaimValue(this ClaimsPrincipal user, string claimType)
        {
            if (string.IsNullOrWhiteSpace(claimType))
            {
                throw new ArgumentException("Claim type is required.", nameof(claimType));
            }

            return user?.Claims.FirstOrDefault(claim =>
                string.Equals(claim.Type, claimType, StringComparison.OrdinalIgnoreCase))?.Value;
        }

        public static IEnumerable<string> GetClaimValues(this ClaimsPrincipal user, string claimType)
        {
            if (string.IsNullOrWhiteSpace(claimType))
            {
                throw new ArgumentException("Claim type is required.", nameof(claimType));
            }

            return user?.Claims
                .Where(claim => string.Equals(claim.Type, claimType, StringComparison.OrdinalIgnoreCase))
                .Select(claim => claim.Value) ?? Enumerable.Empty<string>();
        }
    }
}
