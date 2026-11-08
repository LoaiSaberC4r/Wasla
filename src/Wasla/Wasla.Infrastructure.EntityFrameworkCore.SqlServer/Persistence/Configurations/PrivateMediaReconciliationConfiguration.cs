using BuildingBlock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.Domain.Media;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Configurations;

internal sealed class PrivateMediaCheckpointConfiguration : IWriteEntityConfiguration<PrivateMediaReconciliationCheckpoint>
{
    public void ConfigureAggregate(EntityTypeBuilder<PrivateMediaReconciliationCheckpoint> b)
    {
        b.ToTable("PrivateMediaReconciliationCheckpoints"); b.HasKey(r => r.Module);
        b.Property(r => r.Module).HasMaxLength(64); b.Property(r => r.RowVersion).IsRowVersion();
        b.Property(r => r.LastFileKey).HasMaxLength(1000); b.Property(r => r.LastReferenceKey).HasMaxLength(1000);
    }
}
internal sealed class PrivateMediaRunConfiguration : IWriteEntityConfiguration<PrivateMediaReconciliationRun>
{
    public void ConfigureAggregate(EntityTypeBuilder<PrivateMediaReconciliationRun> b)
    {
        b.ToTable("PrivateMediaReconciliationRuns"); b.HasKey(r => r.Id); b.Property(r => r.Id).ValueGeneratedNever();
        b.Property(r => r.Module).HasMaxLength(64); b.HasIndex(r => new { r.Module, r.InspectedAtUtc, r.Id });
    }
}
