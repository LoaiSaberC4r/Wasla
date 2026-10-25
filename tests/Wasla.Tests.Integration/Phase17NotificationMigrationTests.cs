using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Email;

namespace Wasla.Tests.Integration;

public sealed class Phase17NotificationMigrationTests
{
    [Fact]
    public void O15_model_snapshot_matches_and_migrations_are_additive_without_backfill()
    {
        using var db = new WaslaDbContext(new DbContextOptionsBuilder<WaslaDbContext>().UseSqlServer("Server=localhost;Database=ModelOnly;Integrated Security=True;TrustServerCertificate=True").Options);
        Assert.False(db.Database.HasPendingModelChanges());
        var script = db.GetService<IMigrator>().GenerateScript("20261024174818_Phase16ExactFamilyRelationshipProof");
        Assert.Contains("CREATE TABLE [NotificationEvents]", script, StringComparison.Ordinal);
        Assert.Contains("ALTER TABLE [EmailOutboxMessages] ADD [NotificationDeliveryId]", script, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP TABLE", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT INTO [NotificationEvents]", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE FROM", script, StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    [Trait("Category", "SQLServerConcurrency")]
    public async Task O15_populated_legacy_outbox_survives_native_sql_upgrade_without_resend_or_backfill()
    {
        var source = Environment.GetEnvironmentVariable("WASLA_SQLSERVER_CONNECTION_STRING"); Assert.SkipWhen(string.IsNullOrWhiteSpace(source), "Native SQL Server is required.");
        var builder = new SqlConnectionStringBuilder(source) { InitialCatalog = "WaslaPhase17Migration_" + Guid.NewGuid().ToString("N") };
        Assert.StartsWith("WaslaPhase17Migration_", builder.InitialCatalog, StringComparison.Ordinal);
        await using var db = new WaslaDbContext(new DbContextOptionsBuilder<WaslaDbContext>().UseSqlServer(builder.ConnectionString).Options);
        var ct = TestContext.Current.CancellationToken; var id = Guid.NewGuid();
        try
        {
            await db.GetService<IMigrator>().MigrateAsync("20261024174818_Phase16ExactFamilyRelationshipProof", ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO [EmailOutboxMessages] ([Id],[IdempotencyKey],[RecipientEmail],[Subject],[HtmlBody],[CreatedOnUtc],[AttemptCount],[Status]) VALUES ({id},N'historical-sent',N'old@example.test',N'historical',N'historical',{DateTime.UtcNow},1,3)", ct);
            await db.Database.MigrateAsync(ct);
            var old = await db.EmailOutboxMessages.AsNoTracking().SingleAsync(m => m.Id == id, ct);
            Assert.Equal(EmailOutboxStatus.Sent, old.Status); Assert.Null(old.NotificationDeliveryId);
            Assert.Empty(await db.NotificationEvents.ToArrayAsync(ct)); Assert.Empty(await db.NotificationIntents.ToArrayAsync(ct));
            Assert.Equal(120, (await db.PlatformNotificationSettings.SingleAsync(ct)).ReminderLeadMinutes);
        }
        finally
        {
            await db.Database.EnsureDeletedAsync(ct);
        }
    }
}
