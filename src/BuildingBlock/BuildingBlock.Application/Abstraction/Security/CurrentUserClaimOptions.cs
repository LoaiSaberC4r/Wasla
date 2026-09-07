using System.Security.Claims;

namespace BuildingBlock.Application.Abstraction.Security
{
    public sealed class CurrentUserClaimOptions
    {
        public const string SectionName = "CurrentUserClaims";

        public string UserIdClaimType { get; set; } = ClaimTypes.NameIdentifier;

        public string UserNameClaimType { get; set; } = ClaimTypes.Name;

        public string EmailClaimType { get; set; } = ClaimTypes.Email;

        public string RoleClaimType { get; set; } = ClaimTypes.Role;

        public string PermissionClaimType { get; set; } = "permission";

        public int MaximumClaimValueLength { get; set; } = 4096;

        public int MaximumRoles { get; set; } = 128;

        public int MaximumPermissions { get; set; } = 512;
    }
}
