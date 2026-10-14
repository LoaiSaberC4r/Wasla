using BuildingBlock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.Domain.Reporting;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Configurations;

internal sealed class ReportingRefreshRunConfiguration : IWriteEntityConfiguration<ReportingRefreshRun>
{
    public void ConfigureAggregate(EntityTypeBuilder<ReportingRefreshRun> b)
    {
        b.ToTable("ReportingRefreshRuns"); b.HasKey(x => x.Id); b.Property(x => x.Status).HasMaxLength(20);
        b.Property(x => x.FailureCode).HasMaxLength(100); b.HasIndex(x => x.StartedAtUtc);
    }
}
