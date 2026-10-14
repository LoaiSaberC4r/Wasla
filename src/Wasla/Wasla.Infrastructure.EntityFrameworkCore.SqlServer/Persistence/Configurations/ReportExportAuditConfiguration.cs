using BuildingBlock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.Domain.Reporting;
using Wasla.Domain.Security;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Configurations;

internal sealed class ReportExportAuditConfiguration : IWriteEntityConfiguration<ReportExportAudit>
{
    public void ConfigureAggregate(EntityTypeBuilder<ReportExportAudit> b)
    {
        b.ToTable("ReportExportAudits", t => t.UseSqlOutputClause(false)); b.HasKey(x => x.Id);
        b.Property(x => x.ActorRole).HasMaxLength(30); b.Property(x => x.ReportCode).HasMaxLength(50);
        b.Property(x => x.Format).HasMaxLength(5); b.Property(x => x.Language).HasMaxLength(2); b.Property(x => x.Outcome).HasMaxLength(30);
        b.Property(x => x.FailureCode).HasMaxLength(100); b.Property(x => x.AppliedFiltersJson).HasMaxLength(4000);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.ActorApplicationUserId, x.RecordedAtUtc }); b.HasIndex(x => new { x.ReportCode, x.RecordedAtUtc });
    }
}
