namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Options;

public sealed class RootSuperAdminOptions
{
    public const string SectionName = "RootSuperAdmin";
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string Password { get; set; } = string.Empty;
}
