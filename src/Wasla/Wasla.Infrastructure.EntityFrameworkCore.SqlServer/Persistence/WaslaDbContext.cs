using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Email;
using BuildingBlock.Infrastructure.Extensions;
using BuildingBlock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

public sealed class WaslaDbContext(DbContextOptions<WaslaDbContext> options)
    : DbContext(options)
{
    public DbSet<EmailOutboxMessage> EmailOutboxMessages => Set<EmailOutboxMessage>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PrepareSqliteRowVersions();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        PrepareSqliteRowVersions();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyWriteConfigurations(typeof(WaslaDbContext).Assembly);
        modelBuilder.ApplySoftDeleteQueryFilter();

        if (string.Equals(
                Database.ProviderName,
                "Microsoft.EntityFrameworkCore.Sqlite",
                StringComparison.Ordinal))
        {
            ConfigureSqliteConcurrencyFallback(modelBuilder);
        }
    }

    private static void ConfigureSqliteConcurrencyFallback(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var rowVersion = entityType.FindProperty(nameof(EmailOutboxMessage.RowVersion));
            if (rowVersion is null)
            {
                continue;
            }

            rowVersion.ValueGenerated = ValueGenerated.Never;
            rowVersion.SetBeforeSaveBehavior(PropertySaveBehavior.Save);
            rowVersion.SetAfterSaveBehavior(PropertySaveBehavior.Save);
        }
    }

    private void PrepareSqliteRowVersions()
    {
        if (!string.Equals(
                Database.ProviderName,
                "Microsoft.EntityFrameworkCore.Sqlite",
                StringComparison.Ordinal))
        {
            return;
        }

        foreach (var entry in ChangeTracker.Entries()
                     .Where(entry => entry.State is EntityState.Added or EntityState.Modified))
        {
            var rowVersion = entry.Metadata.FindProperty(nameof(EmailOutboxMessage.RowVersion));
            if (rowVersion is not null)
            {
                entry.Property(nameof(EmailOutboxMessage.RowVersion)).CurrentValue =
                    Guid.NewGuid().ToByteArray()[..8];
            }
        }
    }
}
