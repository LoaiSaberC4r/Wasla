using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed partial class DatabaseInitializationHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<DatabaseInitializationOptions> options,
    IHostEnvironment environment,
    ILogger<DatabaseInitializationHostedService> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<WaslaDbContext>();
        var migrationService = scope.ServiceProvider
            .GetRequiredService<IDatabaseMigrationService>();
        var connection = DatabaseConnectionDetails.From(dbContext);

        InitializationResolved(
            logger,
            environment.EnvironmentName,
            options.Value.ApplyMigrationsOnStartup,
            connection.DataSource,
            connection.Database,
            connection.IntegratedSecurity);

        if (!options.Value.ApplyMigrationsOnStartup)
        {
            InitializationSkipped(logger);
            return;
        }

        var compiledMigrations = migrationService.GetCompiledMigrations(dbContext);
        var pendingMigrations = await migrationService
            .GetPendingMigrationsAsync(dbContext, cancellationToken);

        MigrationsDiscovered(
            logger,
            compiledMigrations.Count,
            pendingMigrations.Count);

        await migrationService.MigrateAsync(dbContext, cancellationToken);
        InitializationCompleted(logger);
    }

    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    [LoggerMessage(
        EventId = 4120,
        Level = LogLevel.Information,
        Message = "Database initialization resolved for {EnvironmentName}. Apply migrations: {ApplyMigrations}. Server: {DataSource}. Database: {Database}. Integrated security: {IntegratedSecurity}.")]
    private static partial void InitializationResolved(
        ILogger logger,
        string environmentName,
        bool applyMigrations,
        string dataSource,
        string database,
        bool integratedSecurity);

    [LoggerMessage(
        EventId = 4121,
        Level = LogLevel.Information,
        Message = "Database migration skipped because ApplyMigrationsOnStartup is disabled.")]
    private static partial void InitializationSkipped(ILogger logger);

    [LoggerMessage(
        EventId = 4122,
        Level = LogLevel.Information,
        Message = "Compiled migrations: {CompiledCount}. Pending migrations: {PendingCount}.")]
    private static partial void MigrationsDiscovered(
        ILogger logger,
        int compiledCount,
        int pendingCount);

    [LoggerMessage(
        EventId = 4123,
        Level = LogLevel.Information,
        Message = "Database initialization completed.")]
    private static partial void InitializationCompleted(ILogger logger);
}
