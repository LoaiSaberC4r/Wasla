using Microsoft.EntityFrameworkCore;
using Wasla.Domain.Subscriptions;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

namespace Wasla.Tests.Integration;

public sealed class SubscriptionMigrationTests
{
    [Fact, Trait("Category", "SQLServerConcurrency")]
    public async Task Sql_server_rejects_overlapping_active_and_scheduled_periods_in_the_same_contract()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(true);
        await app.WithDbAsync(async db =>
        {
            var active = await db.DoctorSubscriptions.SingleAsync(p => p.DoctorId == app.DoctorId);
            var overlap = DoctorSubscription.Select(app.DoctorId, active.Terms, app.Clock.UtcNow, active.SubscriptionContractId).Value;
            Assert.True(overlap.SchedulePaidPeriod(app.Clock.UtcNow.AddDays(1), app.DoctorUserId, app.Clock.UtcNow).IsSuccess);
            db.Add(overlap);
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
            Assert.Equal(51063, Assert.IsType<Microsoft.Data.SqlClient.SqlException>(error.InnerException).Number);
        });
        await app.WithDbAsync(async db => Assert.Single(await db.DoctorSubscriptions.Where(p => p.DoctorId == app.DoctorId).ToArrayAsync()));
    }
    [Fact]
    public void Sql_model_matches_migration_and_has_filtered_uniqueness_and_restricted_history()
    {
        using var db = new WaslaDbContext(new DbContextOptionsBuilder<WaslaDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=SubscriptionModelOnly;Integrated Security=true;TrustServerCertificate=true").Options);
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.EndsWith("_CommercialContractsAndPracticeAllocations", db.Database.GetMigrations().Last());
        var entity = db.Model.FindEntityType(typeof(DoctorSubscription))!;
        Assert.Equal(4, entity.GetIndexes().Count(i => i.IsUnique && i.GetFilter() is not null));
        Assert.True(entity.FindProperty("RowVersion")!.IsConcurrencyToken);
        var payment = db.Model.FindEntityType(typeof(SubscriptionPayment))!;
        Assert.Equal(18, payment.FindProperty("Amount")!.GetPrecision()); Assert.Equal(2, payment.FindProperty("Amount")!.GetScale());
        Assert.All(payment.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
    }
}
