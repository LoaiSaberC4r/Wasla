using BuildingBlock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.Domain.Practices;
using Wasla.Domain.Reporting;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Configurations;

internal sealed class PracticeReportingPolicySnapshotConfiguration : IWriteEntityConfiguration<PracticeReportingPolicySnapshot>
{
    public void ConfigureAggregate(EntityTypeBuilder<PracticeReportingPolicySnapshot> b)
    {
        b.ToTable("PracticeReportingPolicySnapshots", t => t.UseSqlOutputClause(false)); b.HasKey(x => x.Id);
        b.HasOne<DoctorPractice>().WithMany().HasForeignKey(x => new { x.DoctorId, x.PracticeId })
            .HasPrincipalKey(x => new { x.DoctorId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.PracticeId, x.RevisionNumber }).IsUnique();
        b.HasIndex(x => new { x.PracticeId, x.RecordedAtUtc });
    }
}
