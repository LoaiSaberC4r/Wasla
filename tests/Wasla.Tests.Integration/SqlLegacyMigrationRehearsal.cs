using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

namespace Wasla.Tests.Integration;

internal static class SqlLegacyMigrationRehearsal
{
    internal static async Task RunAsync(WaslaDbContext source, string legacyMigration,
        Func<WaslaDbContext, Task> verify)
    {
        var ct = TestContext.Current.CancellationToken;
        Assert.True(source.Database.IsSqlServer());
        Assert.Equal(0, await source.TicketRegistry.CountAsync(x => x.IsArchived, ct));
        Assert.Equal(0, await source.ReservationRegistry.CountAsync(x => x.IsArchived, ct));
        var original = new SqlConnectionStringBuilder(source.Database.GetConnectionString());
        Assert.StartsWith("WaslaPhase13Tests_", original.InitialCatalog);
        var target = new SqlConnectionStringBuilder(original.ConnectionString)
            { InitialCatalog = "WaslaLegacyRehearsal_" + Guid.NewGuid().ToString("N") };
        await using var db = new WaslaDbContext(new DbContextOptionsBuilder<WaslaDbContext>()
            .UseSqlServer(target.ConnectionString).Options);
        try
        {
            await db.GetService<IMigrator>().MigrateAsync(legacyMigration, ct);
            await Phase18PartTwoMigrationTests.CopyNonemptyBaseline(original.InitialCatalog, db, ct);
            await db.GetService<IMigrator>().MigrateAsync("20261014170532_Phase18PartTwoArchiveExpansion", ct);
            var population = new ArchiveRegistryPopulation(db);
            while (await population.PopulateAsync(100, ct) != 0) { }
            await db.Database.MigrateAsync(ct);
            await verify(db);
        }
        finally
        {
            Assert.StartsWith("WaslaLegacyRehearsal_", target.InitialCatalog);
            await db.Database.EnsureDeletedAsync(ct);
        }
    }
}
