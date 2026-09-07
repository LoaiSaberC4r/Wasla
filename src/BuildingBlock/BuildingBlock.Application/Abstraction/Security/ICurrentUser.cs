namespace BuildingBlock.Application.Abstraction.Security
{
    public interface ICurrentUser
    {
        bool IsAuthenticated { get; }

        Guid? UserId { get; }

        string? UserName { get; }

        string? Email { get; }

        IReadOnlyCollection<string> Roles { get; }

        IReadOnlyCollection<string> Permissions { get; }

        string? GetClaimValue(string claimType);

        IReadOnlyCollection<string> GetClaimValues(string claimType);
    }
}
