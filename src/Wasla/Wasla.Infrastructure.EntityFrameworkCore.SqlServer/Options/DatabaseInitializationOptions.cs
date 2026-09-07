namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Options;

public sealed class DatabaseInitializationOptions
{
    public const string SectionName = "DatabaseInitialization";

    public bool ApplyMigrationsOnStartup { get; set; }
}
