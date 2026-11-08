using BuildingBlock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.Domain.Families;
using Wasla.Domain.Patients;
using Wasla.Domain.Security;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Configurations;
internal sealed class ChildOnboardingConfiguration : IWriteEntityConfiguration<ChildOnboardingRequest>
{
    public void ConfigureAggregate(EntityTypeBuilder<ChildOnboardingRequest> b)
    {
        b.ToTable("ChildOnboardingRequests", t => {
            t.HasCheckConstraint("CK_ChildOnboarding_Status", "[Status] IN (1,2,3,4,5)");
            t.HasCheckConstraint("CK_ChildOnboarding_Role", "[ParentRole] IN (1,2)");
            t.HasCheckConstraint("CK_ChildOnboarding_Revision", "[Revision] >= 1");
        });
        b.HasKey(r => r.Id); b.Property(r => r.Id).ValueGeneratedNever(); b.Property(r => r.RowVersion).IsRowVersion();
        b.Property(r => r.NameAr).HasMaxLength(200).IsRequired(); b.Property(r => r.NameEn).HasMaxLength(200);
        b.Property(r => r.PhoneNumber).HasMaxLength(30); b.Property(r => r.ContactNameAr).HasMaxLength(200).IsRequired(); b.Property(r => r.ContactPhone).HasMaxLength(30).IsRequired();
        b.Property(r => r.IdempotencyKey).HasMaxLength(100).IsRequired(); b.Property(r => r.Fingerprint).HasMaxLength(64).IsRequired(); b.Property(r => r.IntentHash).HasMaxLength(64).IsRequired();
        b.Property(r => r.ReviewIdempotencyKey).HasMaxLength(100); b.Property(r => r.ResubmitIdempotencyKey).HasMaxLength(100);
        b.Property(r => r.ReviewFingerprint).HasMaxLength(64); b.Property(r => r.ResubmitFingerprint).HasMaxLength(64); b.Property(r => r.ReviewMessage).HasMaxLength(2000);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.ActorApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.ReviewedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Patient>().WithMany().HasForeignKey(r => r.ParentPatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Patient>().WithMany().HasForeignKey(r => r.ApprovedPatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<FamilyRelationshipRequest>().WithMany().HasForeignKey(r => r.FamilyRelationshipRequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<FamilyGovernanceCase>().WithMany().HasForeignKey(r => r.GovernanceCaseId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(r => new { r.ActorApplicationUserId, r.IdempotencyKey }).IsUnique();
        b.HasIndex(r => new { r.ParentPatientId, r.IntentHash }).IsUnique().HasFilter("[Status] IN (1,2,5)");
        b.HasIndex(r => new { r.Status, r.CreatedAtUtc, r.Id });
    }
}
internal sealed class ChildOnboardingEvidenceConfiguration : IWriteEntityConfiguration<ChildOnboardingEvidence>
{
    public void ConfigureAggregate(EntityTypeBuilder<ChildOnboardingEvidence> b)
    {
        b.ToTable("ChildOnboardingEvidence"); b.HasKey(r => r.Id); b.Property(r => r.Id).ValueGeneratedNever();
        b.Property(r => r.MediaKey).HasMaxLength(1000).IsRequired(); b.Property(r => r.FileName).HasMaxLength(500).IsRequired(); b.Property(r => r.ContentType).HasMaxLength(200).IsRequired();
        b.HasOne<ChildOnboardingRequest>().WithMany().HasForeignKey(r => r.RequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(r => new { r.RequestId, r.Revision, r.Id });
    }
}
internal sealed class ChildOnboardingHistoryConfiguration : IWriteEntityConfiguration<ChildOnboardingHistory>
{
    public void ConfigureAggregate(EntityTypeBuilder<ChildOnboardingHistory> b)
    {
        b.ToTable("ChildOnboardingHistories"); b.HasKey(r => r.Id); b.Property(r => r.Id).ValueGeneratedNever();
        b.Property(r => r.Action).HasMaxLength(50).IsRequired(); b.Property(r => r.Message).HasMaxLength(2000);
        b.HasOne<ChildOnboardingRequest>().WithMany().HasForeignKey(r => r.RequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.ActorApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(r => new { r.RequestId, r.Revision, r.OccurredAtUtc, r.Id });
    }
}
