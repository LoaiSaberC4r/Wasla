using System.Security.Claims;

namespace BuildingBlock.Application.Abstraction.Security
{
    public interface ITokenReader
    {
        TokenInfoDto ReadFromPrincipal(ClaimsPrincipal principal);

        bool TryGetClaim(ClaimsPrincipal principal, string claimType, out string? value);

        (bool ok, T? value) TryGet<T>(ClaimsPrincipal principal, string claimType);
    }
}
