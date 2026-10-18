using BuildingBlock.Application.Abstraction.Security;
internal sealed class MaintenanceExecutionContext : ICurrentUser
{
    public bool IsAuthenticated => false;
    public Guid? UserId => null;
    public string? UserName => null;
    public string? Email => null;
    public IReadOnlyCollection<string> Roles => [];
    public IReadOnlyCollection<string> Permissions => [];
    public string? GetClaimValue(string claimType) => null;
    public IReadOnlyCollection<string> GetClaimValues(string claimType) => [];
}
