using BuildingBlock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.Domain.Practices;
using Wasla.Domain.Reporting;
using Wasla.Domain.Security;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Configurations;

internal sealed class DoctorInsightFollowUpConfiguration : IWriteEntityConfiguration<DoctorInsightFollowUp>
{
    public void ConfigureAggregate(EntityTypeBuilder<DoctorInsightFollowUp> b)
    {
        b.ToTable("DoctorInsightFollowUps", t => { t.UseSqlOutputClause(false); t.HasCheckConstraint("CK_DoctorInsight_State", "[State] IN (1,2,3,4)"); });
        b.HasKey(x => x.Id); b.Property(x => x.MetricCode).HasMaxLength(80); b.Property(x => x.EvidenceJson).HasMaxLength(12000);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasOne<DoctorPractice>().WithMany().HasForeignKey(x => new { x.DoctorId, x.PracticeId })
            .HasPrincipalKey(x => new { x.DoctorId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.History).WithOne().HasForeignKey(x => x.InsightId).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.History).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.HasIndex(x => new { x.DoctorId, x.PracticeId, x.State, x.CreatedOnUtc });
    }
}
internal sealed class DoctorInsightHistoryConfiguration : IWriteEntityConfiguration<DoctorInsightHistory>
{
    public void ConfigureAggregate(EntityTypeBuilder<DoctorInsightHistory> b)
    {
        b.ToTable("DoctorInsightHistories", t => t.UseSqlOutputClause(false)); b.HasKey(x => x.Id);
        b.Property(x => x.Operation).HasMaxLength(80); b.Property(x => x.Reason).HasMaxLength(2000);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.InsightId, x.RecordedAtUtc });
        b.HasIndex(x => new { x.InsightId, x.SequenceNumber }).IsUnique();
    }
}
