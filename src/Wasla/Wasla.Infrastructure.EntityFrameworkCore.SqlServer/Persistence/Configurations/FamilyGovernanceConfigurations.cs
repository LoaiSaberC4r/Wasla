using BuildingBlock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.Domain.Families;
using Wasla.Domain.Patients;
using Wasla.Domain.Security;
namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Configurations;
internal sealed class FamilyGovernanceCaseConfiguration : IWriteEntityConfiguration<FamilyGovernanceCase>
{
    public void ConfigureAggregate(EntityTypeBuilder<FamilyGovernanceCase> b)
    {
        b.ToTable("FamilyGovernanceCases", t => {
            t.HasCheckConstraint("CK_FamilyGovernance_Kind", "[Kind] IN (1,2)"); t.HasCheckConstraint("CK_FamilyGovernance_Status", "[Status] IN (1,2,3,4,5)");
            t.HasCheckConstraint("CK_FamilyGovernance_Subject", "[ParentPatientId] <> [ChildPatientId]"); t.HasCheckConstraint("CK_FamilyGovernance_Revision", "[Revision] > 0");
        });
        b.HasKey(r => r.Id); b.Property(r => r.Id).ValueGeneratedNever(); b.Property(r => r.RowVersion).IsRowVersion();
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Patient>().WithMany().HasForeignKey(r => r.ParentPatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Patient>().WithMany().HasForeignKey(r => r.ChildPatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ParentChildOperationalAuthority>().WithMany().HasForeignKey(r => r.OperationalAuthorityId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(r => new { r.ParentPatientId, r.ChildPatientId, r.Kind }).IsUnique().HasFilter("[Status] IN (1,2,5)");
        b.HasIndex(r => new { r.Status, r.Kind, r.CreatedAtUtc, r.Id }); b.HasIndex(r => new { r.ActorUserId, r.CreatedAtUtc, r.Id });
    }
}
internal sealed class FamilyGovernanceRevisionConfiguration : IWriteEntityConfiguration<FamilyGovernanceRevision>
{
    public void ConfigureAggregate(EntityTypeBuilder<FamilyGovernanceRevision> b)
    {
        b.ToTable("FamilyGovernanceRevisions"); b.HasKey(r => r.Id); b.Property(r => r.Id).ValueGeneratedNever();
        b.Property(r => r.BeforeSnapshot).HasMaxLength(4000).IsRequired(); b.Property(r => r.AfterSnapshot).HasMaxLength(4000).IsRequired(); b.Property(r => r.Reason).HasMaxLength(2000).IsRequired(); b.Property(r => r.AuthorityVersion).HasMaxLength(2000);
        b.HasOne<FamilyGovernanceCase>().WithMany().HasForeignKey(r => r.CaseId).OnDelete(DeleteBehavior.Restrict); b.HasIndex(r => new { r.CaseId, r.Revision }).IsUnique();
    }
}
internal sealed class FamilyGovernanceEvidenceConfiguration : IWriteEntityConfiguration<FamilyGovernanceEvidence>
{
    public void ConfigureAggregate(EntityTypeBuilder<FamilyGovernanceEvidence> b)
    {
        b.ToTable("FamilyGovernanceEvidence"); b.HasKey(r => r.Id); b.Property(r => r.Id).ValueGeneratedNever();
        b.Property(r => r.MediaKey).HasMaxLength(1000).IsRequired(); b.Property(r => r.FileName).HasMaxLength(500).IsRequired(); b.Property(r => r.ContentType).HasMaxLength(200).IsRequired();
        b.HasOne<FamilyGovernanceCase>().WithMany().HasForeignKey(r => r.CaseId).OnDelete(DeleteBehavior.Restrict); b.HasIndex(r => new { r.CaseId, r.Revision, r.Id });
    }
}
internal sealed class FamilyGovernanceHistoryConfiguration : IWriteEntityConfiguration<FamilyGovernanceHistory>
{
    public void ConfigureAggregate(EntityTypeBuilder<FamilyGovernanceHistory> b)
    {
        b.ToTable("FamilyGovernanceHistories"); b.HasKey(r => r.Id); b.Property(r => r.Id).ValueGeneratedNever(); b.Property(r => r.Action).HasMaxLength(50).IsRequired(); b.Property(r => r.Reason).HasMaxLength(2000);
        b.HasOne<FamilyGovernanceCase>().WithMany().HasForeignKey(r => r.CaseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.ActorUserId).OnDelete(DeleteBehavior.Restrict); b.HasIndex(r => new { r.CaseId, r.Revision, r.OccurredAtUtc, r.Id });
    }
}
internal sealed class FamilyGovernanceOperationConfiguration : IWriteEntityConfiguration<FamilyGovernanceOperation>
{
    public void ConfigureAggregate(EntityTypeBuilder<FamilyGovernanceOperation> b)
    {
        b.ToTable("FamilyGovernanceOperations"); b.HasKey(r => r.Id); b.Property(r => r.Id).ValueGeneratedNever();
        b.Property(r => r.Operation).HasMaxLength(100).IsRequired(); b.Property(r => r.IdempotencyKey).HasMaxLength(100).IsRequired(); b.Property(r => r.Fingerprint).HasMaxLength(64).IsRequired(); b.Property(r => r.ResponseJson).HasMaxLength(4000).IsRequired();
        b.HasOne<FamilyGovernanceCase>().WithMany().HasForeignKey(r => r.CaseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.ActorUserId).OnDelete(DeleteBehavior.Restrict); b.HasIndex(r => new { r.ActorUserId, r.Operation, r.IdempotencyKey }).IsUnique();
    }
}
