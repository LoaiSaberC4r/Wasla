using Microsoft.EntityFrameworkCore;
using Wasla.Domain.Subscriptions;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

namespace Wasla.Tests.Integration;

public sealed class SubscriptionMigrationTests
{
    [Fact]
    public void Sql_model_matches_migration_and_has_filtered_uniqueness_and_restricted_history()
    {
        using var db = new WaslaDbContext(new DbContextOptionsBuilder<WaslaDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=SubscriptionModelOnly;Integrated Security=true;TrustServerCertificate=true").Options);
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.EndsWith("_CommercialSubscriptionFoundation", db.Database.GetMigrations().Last());
        var entity = db.Model.FindEntityType(typeof(DoctorSubscription))!;
        Assert.Equal(4, entity.GetIndexes().Count(i => i.IsUnique && i.GetFilter() is not null));
        Assert.True(entity.FindProperty("RowVersion")!.IsConcurrencyToken);
        var payment = db.Model.FindEntityType(typeof(SubscriptionPayment))!;
        Assert.Equal(18, payment.FindProperty("Amount")!.GetPrecision()); Assert.Equal(2, payment.FindProperty("Amount")!.GetScale());
        Assert.All(payment.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
    }
}
