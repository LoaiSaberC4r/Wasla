using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

public interface IDatabaseMigrationService
{
    IReadOnlyList<string> GetCompiledMigrations(WaslaDbContext dbContext);

    Task<IReadOnlyList<string>> GetPendingMigrationsAsync(
        WaslaDbContext dbContext,
        CancellationToken cancellationToken = default);

    Task MigrateAsync(
        WaslaDbContext dbContext,
        CancellationToken cancellationToken = default);
}

internal sealed class EfCoreDatabaseMigrationService : IDatabaseMigrationService
{
    public IReadOnlyList<string> GetCompiledMigrations(WaslaDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        return dbContext.Database.GetMigrations().ToArray();
    }

    public async Task<IReadOnlyList<string>> GetPendingMigrationsAsync(
        WaslaDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        var migrations = await dbContext.Database
            .GetPendingMigrationsAsync(cancellationToken);
        return migrations.ToArray();
    }

    public Task MigrateAsync(
        WaslaDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        return dbContext.Database.MigrateAsync(cancellationToken);
    }
}

internal readonly record struct DatabaseConnectionDetails(
    string DataSource,
    string Database,
    bool IntegratedSecurity)
{
    public static DatabaseConnectionDetails From(WaslaDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var connection = dbContext.Database.GetDbConnection();
        if (connection is not SqlConnection sqlConnection)
        {
            return new DatabaseConnectionDetails(
                connection.DataSource,
                connection.Database,
                IntegratedSecurity: false);
        }

        var builder = new SqlConnectionStringBuilder(sqlConnection.ConnectionString);
        return new DatabaseConnectionDetails(
            builder.DataSource,
            builder.InitialCatalog,
            builder.IntegratedSecurity);
    }
}
