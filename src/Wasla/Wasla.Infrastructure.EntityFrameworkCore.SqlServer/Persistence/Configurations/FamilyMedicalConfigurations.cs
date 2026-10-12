using BuildingBlock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.Domain.Families;
using Wasla.Domain.Patients;
using Wasla.Domain.Security;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Configurations;

internal static class FamilyMedicalMapping
{
    public static void Scope<T>(EntityTypeBuilder<T> b, string adult) where T : class
    {
        b.HasKey("Id"); b.Property<Guid>("Id").ValueGeneratedNever(); b.Property<byte[]>("RowVersion").IsRowVersion();
        b.HasOne<Family>().WithMany().HasForeignKey("FamilyId").OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Patient>().WithMany().HasForeignKey("ChildPatientId").OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Patient>().WithMany().HasForeignKey(adult).OnDelete(DeleteBehavior.Restrict);
    }
    public static void History<T, TParent>(EntityTypeBuilder<T> b, string table) where T : FamilyMedicalHistory where TParent : class
    {
        b.HasBaseType((Type?)null); b.ToTable(table); b.HasKey(h => h.Id); b.Property(h => h.Id).ValueGeneratedNever();
        b.Property(h => h.Action).HasMaxLength(50).IsRequired(); b.Property(h => h.ReasonOrMessage).HasMaxLength(2000);
        b.HasOne<TParent>().WithMany().HasForeignKey(h => h.ParentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(h => h.ActorApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(h => new { h.ParentId, h.OccurredAtUtc, h.Id });
    }
}
internal sealed class FamilyMedicalAccessRequestConfiguration : IWriteEntityConfiguration<FamilyMedicalAccessRequest>
{
    public void ConfigureAggregate(EntityTypeBuilder<FamilyMedicalAccessRequest> b)
    {
        b.ToTable("FamilyMedicalAccessRequests", t => {
            t.HasCheckConstraint("CK_FamilyMedicalRequests_Status", "[Status] IN (1,2,3,4,5)");
            t.HasCheckConstraint("CK_FamilyMedicalRequests_Type", "[RequestType] IN (1,2)");
            t.HasCheckConstraint("CK_FamilyMedicalRequests_Revision", "[CurrentRevisionNumber] >= 1");
            t.HasCheckConstraint("CK_FamilyMedicalRequests_Actors", "[ChildPatientId] <> [AffectedAdultPatientId] AND (([RequestType] = 1 AND [SubmittedByPatientId] <> [AffectedAdultPatientId]) OR ([RequestType] = 2 AND [SubmittedByPatientId] = [AffectedAdultPatientId]))");
        });
        FamilyMedicalMapping.Scope(b, nameof(FamilyMedicalAccessRequest.AffectedAdultPatientId)); b.Ignore(r => r.IsOpen);
        b.Property(r => r.Reason).HasMaxLength(2000).IsRequired(); b.Property(r => r.ReviewMessage).HasMaxLength(2000);
        b.HasOne<Patient>().WithMany().HasForeignKey(r => r.SubmittedByPatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.SubmittedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.ReviewedBySuperAdminApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.WithdrawnByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<FamilyMedicalAccessRestriction>().WithMany().HasForeignKey(r => r.AffectedRestrictionId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(r => new { r.FamilyId, r.ChildPatientId, r.AffectedAdultPatientId, r.RequestType }).IsUnique()
            .HasFilter("[Status] IN (1,2)").HasDatabaseName("UX_FamilyMedicalRequests_Open");
        b.HasIndex(r => new { r.SubmittedByApplicationUserId, r.SubmittedAtUtc, r.Id }).IsDescending(false, true, false);
        b.HasIndex(r => new { r.Status, r.SubmittedAtUtc, r.Id }).IsDescending(false, true, false);
    }
}
internal sealed class FamilyMedicalAccessRestrictionConfiguration : IWriteEntityConfiguration<FamilyMedicalAccessRestriction>
{
    public void ConfigureAggregate(EntityTypeBuilder<FamilyMedicalAccessRestriction> b)
    {
        b.ToTable("FamilyMedicalAccessRestrictions", t => t.HasCheckConstraint("CK_FamilyMedicalRestrictions_Status", "[Status] IN (1,2)"));
        FamilyMedicalMapping.Scope(b, nameof(FamilyMedicalAccessRestriction.AffectedAdultPatientId));
        b.HasIndex(r => new { r.FamilyId, r.ChildPatientId, r.AffectedAdultPatientId }).IsUnique()
            .HasFilter("[Status] = 1").HasDatabaseName("UX_FamilyMedicalRestrictions_Active");
        b.HasOne<FamilyMedicalAccessRequest>().WithMany().HasForeignKey(r => r.RestrictionApprovedByRequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<FamilyMedicalAccessRequest>().WithMany().HasForeignKey(r => r.LiftedByRequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.RestrictedBySuperAdminApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.LiftedBySuperAdminApplicationUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
internal sealed class FamilyMedicalEligibilityReviewConfiguration : IWriteEntityConfiguration<FamilyMedicalEligibilityReview>
{
    public void ConfigureAggregate(EntityTypeBuilder<FamilyMedicalEligibilityReview> b)
    {
        b.ToTable("FamilyMedicalEligibilityReviews", t => t.HasCheckConstraint("CK_FamilyMedicalEligibility_Status", "[Status] IN (1,2,3,4)"));
        FamilyMedicalMapping.Scope(b, nameof(FamilyMedicalEligibilityReview.AdultPatientId));
        b.Property(r => r.PolicyVersion).HasMaxLength(100); b.Property(r => r.BasisReference).HasMaxLength(500); b.Property(r => r.ReviewReason).HasMaxLength(2000);
        b.HasOne<FamilyMember>().WithMany().HasForeignKey(r => r.AdultFamilyMemberId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<FamilyMember>().WithMany().HasForeignKey(r => r.ChildFamilyMemberId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.ReviewedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(r => new { r.FamilyId, r.AdultPatientId, r.ChildPatientId }).IsUnique().HasDatabaseName("UX_FamilyMedicalEligibility_Pair");
        b.HasIndex(r => new { r.Status, r.CreatedAtUtc });
    }
}
internal sealed class FamilyMedicalAccessRequestDocumentConfiguration : IWriteEntityConfiguration<FamilyMedicalAccessRequestDocument>
{
    public void ConfigureAggregate(EntityTypeBuilder<FamilyMedicalAccessRequestDocument> b)
    {
        b.ToTable("FamilyMedicalAccessRequestDocuments"); b.HasKey(d => d.Id); b.Property(d => d.Id).ValueGeneratedNever();
        b.Property(d => d.MediaKey).HasMaxLength(1000).IsRequired(); b.Property(d => d.FileName).HasMaxLength(500).IsRequired();
        b.Property(d => d.ContentType).HasMaxLength(200).IsRequired(); b.Property(d => d.Sha256).HasMaxLength(64).IsRequired();
        b.HasOne<FamilyMedicalAccessRequest>().WithMany().HasForeignKey(d => d.RequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(d => d.UploadedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(d => new { d.RequestId, d.RevisionNumber });
    }
}
internal sealed class FamilyMedicalAccessRequestHistoryConfiguration : IWriteEntityConfiguration<FamilyMedicalAccessRequestHistory>
{ public void ConfigureAggregate(EntityTypeBuilder<FamilyMedicalAccessRequestHistory> b) => FamilyMedicalMapping.History<FamilyMedicalAccessRequestHistory, FamilyMedicalAccessRequest>(b, "FamilyMedicalAccessRequestHistories"); }
internal sealed class FamilyMedicalAccessRestrictionHistoryConfiguration : IWriteEntityConfiguration<FamilyMedicalAccessRestrictionHistory>
{ public void ConfigureAggregate(EntityTypeBuilder<FamilyMedicalAccessRestrictionHistory> b) => FamilyMedicalMapping.History<FamilyMedicalAccessRestrictionHistory, FamilyMedicalAccessRestriction>(b, "FamilyMedicalAccessRestrictionHistories"); }
internal sealed class FamilyMedicalEligibilityReviewHistoryConfiguration : IWriteEntityConfiguration<FamilyMedicalEligibilityReviewHistory>
{ public void ConfigureAggregate(EntityTypeBuilder<FamilyMedicalEligibilityReviewHistory> b) => FamilyMedicalMapping.History<FamilyMedicalEligibilityReviewHistory, FamilyMedicalEligibilityReview>(b, "FamilyMedicalEligibilityReviewHistories"); }
