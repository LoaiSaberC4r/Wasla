using BuildingBlock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.Domain.Doctors;
using Wasla.Domain.OperatingFinance;
using Wasla.Domain.Operations;
using Wasla.Domain.Security;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Email;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Configurations;

internal sealed class AdministrativeInvestigationConfiguration : IWriteEntityConfiguration<AdministrativeInvestigation>
{
    public void ConfigureAggregate(EntityTypeBuilder<AdministrativeInvestigation> b)
    {
        b.ToTable("AdministrativeInvestigations", t =>
        {
            t.UseSqlOutputClause(false);
            t.HasCheckConstraint("CK_Investigation_State", "[Status] IN (1,2,3,4) AND [Priority] IN (1,2) AND [IssueType] BETWEEN 1 AND 7");
            t.HasCheckConstraint("CK_Investigation_Assignee", "[Status] <> 2 OR [AssignedSuperAdminId] IS NOT NULL");
            t.HasCheckConstraint("CK_Investigation_Resolution", "[Status] <> 3 OR ([ResolutionType] IS NOT NULL AND [ResolutionNotes] IS NOT NULL AND [ResolvedOnUtc] IS NOT NULL)");
            t.HasCheckConstraint("CK_Investigation_Closure", "[Status] <> 4 OR ([ClosureNotes] IS NOT NULL AND [ClosedOnUtc] IS NOT NULL)");
            t.HasCheckConstraint("CK_Investigation_Link", "([LinkedResourceType] = 0 AND [LinkedResourceId] IS NULL AND [LinkedDoctorId] IS NULL AND [LinkedSpecializationRequestId] IS NULL AND [LinkedBackgroundJobRunId] IS NULL AND [LinkedOutboxMessageId] IS NULL) OR ([LinkedResourceType] = 1 AND [LinkedResourceId] = [LinkedDoctorId] AND [LinkedDoctorId] IS NOT NULL AND [LinkedSpecializationRequestId] IS NULL AND [LinkedBackgroundJobRunId] IS NULL AND [LinkedOutboxMessageId] IS NULL) OR ([LinkedResourceType] = 2 AND [LinkedResourceId] = [LinkedSpecializationRequestId] AND [LinkedSpecializationRequestId] IS NOT NULL AND [LinkedDoctorId] IS NULL AND [LinkedBackgroundJobRunId] IS NULL AND [LinkedOutboxMessageId] IS NULL) OR ([LinkedResourceType] = 3 AND [LinkedResourceId] = [LinkedBackgroundJobRunId] AND [LinkedBackgroundJobRunId] IS NOT NULL AND [LinkedDoctorId] IS NULL AND [LinkedSpecializationRequestId] IS NULL AND [LinkedOutboxMessageId] IS NULL) OR ([LinkedResourceType] = 4 AND [LinkedResourceId] = [LinkedOutboxMessageId] AND [LinkedOutboxMessageId] IS NOT NULL AND [LinkedDoctorId] IS NULL AND [LinkedSpecializationRequestId] IS NULL AND [LinkedBackgroundJobRunId] IS NULL)");
        });
        b.HasKey(x => x.Id); b.Property(x => x.RowVersion).IsRowVersion();
        b.Property(x => x.TitleAr).HasMaxLength(1200); b.Property(x => x.TitleEn).HasMaxLength(1200);
        b.Property(x => x.Description).HasMaxLength(12000); b.Property(x => x.ResolutionNotes).HasMaxLength(12000); b.Property(x => x.ClosureNotes).HasMaxLength(12000);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<SuperAdmin>().WithMany().HasForeignKey(x => x.AssignedSuperAdminId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Doctor>().WithMany().HasForeignKey(x => x.LinkedDoctorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<DoctorSpecializationRequest>().WithMany().HasForeignKey(x => x.LinkedSpecializationRequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OperatingGenerationRun>().WithMany().HasForeignKey(x => x.LinkedBackgroundJobRunId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<EmailOutboxMessage>().WithMany().HasForeignKey(x => x.LinkedOutboxMessageId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.History).WithOne().HasForeignKey(x => x.CaseId).OnDelete(DeleteBehavior.Restrict);
        b.Navigation(x => x.History).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.HasIndex(x => new { x.Status, x.Priority, x.CreatedOnUtc }); b.HasIndex(x => new { x.AssignedSuperAdminId, x.Status });
        b.HasIndex(x => new { x.IssueType, x.CreatedOnUtc });
    }
}
internal sealed class InvestigationHistoryConfiguration : IWriteEntityConfiguration<InvestigationHistory>
{
    public void ConfigureAggregate(EntityTypeBuilder<InvestigationHistory> b)
    {
        b.ToTable("InvestigationHistories", t => t.UseSqlOutputClause(false)); b.HasKey(x => x.Id);
        b.Property(x => x.Operation).HasMaxLength(50); b.Property(x => x.Note).HasMaxLength(12000);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<SuperAdmin>().WithMany().HasForeignKey(x => x.AssignedSuperAdminId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.CaseId, x.SequenceNumber }).IsUnique();
    }
}
