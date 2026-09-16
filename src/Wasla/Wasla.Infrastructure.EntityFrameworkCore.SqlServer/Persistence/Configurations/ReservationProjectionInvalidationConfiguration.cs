using BuildingBlock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Configurations;

internal sealed class ReservationProjectionInvalidationConfiguration
    : IWriteEntityConfiguration<ReservationProjectionInvalidation>
{
    public void ConfigureAggregate(EntityTypeBuilder<ReservationProjectionInvalidation> builder)
    {
        builder.ToTable("ReservationProjectionInvalidations");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.IdempotencyKey).HasMaxLength(450).IsRequired();
        builder.Property(item => item.LastError).HasMaxLength(500);
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();

        builder.HasIndex(item => item.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("UX_ReservationProjectionInvalidations_IdempotencyKey");
        builder.HasIndex(item => new { item.ProcessedOnUtc, item.NextAttemptOnUtc })
            .HasDatabaseName("IX_ReservationProjectionInvalidations_Pending");
    }
}
