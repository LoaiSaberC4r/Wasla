namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal static class SystemSeedIds
{
    public static readonly Guid RootApplicationUserId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    public static readonly Guid RootSuperAdminId = Guid.Parse("30000000-0000-0000-0000-000000000002");
    public static readonly Guid RootUserRoleId = Guid.Parse("30000000-0000-0000-0000-000000000003");
}
