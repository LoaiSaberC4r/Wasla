using BuildingBlock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.Domain.Families;
using Wasla.Domain.Patients;
using Wasla.Domain.Security;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Configurations;

internal sealed class FamilyConfiguration : IWriteEntityConfiguration<Family>
{
    public void ConfigureAggregate(EntityTypeBuilder<Family> builder)
    {
        builder.ToTable("Families");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Status).HasConversion<int>().IsRequired();
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(item => item.Status).HasDatabaseName("IX_Families_Status");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.CreatedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(item => item.Members).WithOne().HasForeignKey(item => item.FamilyId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(item => item.Members).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class FamilyMemberConfiguration : IWriteEntityConfiguration<FamilyMember>
{
    public void ConfigureAggregate(EntityTypeBuilder<FamilyMember> builder)
    {
        builder.ToTable("FamilyMembers");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Role).HasConversion<int>().IsRequired();
        builder.Property(item => item.IsActive).IsRequired();
        builder.HasIndex(item => new { item.FamilyId, item.PatientId }).IsUnique().HasDatabaseName("UX_FamilyMembers_FamilyId_PatientId");
        builder.HasIndex(item => item.PatientId).IsUnique().HasFilter("[IsActive] = 1").HasDatabaseName("UX_FamilyMembers_OneActiveFamilyPerPatient");
        builder.HasIndex(item => new { item.FamilyId, item.Role }).IsUnique()
            .HasFilter("[IsActive] = 1 AND [Role] IN (1, 2)")
            .HasDatabaseName("UX_FamilyMembers_OneActiveParentRole");
        builder.HasOne<Patient>().WithMany().HasForeignKey(item => item.PatientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.VerifiedBySuperAdminUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FamilyRelationshipRequest>().WithMany().HasForeignKey(item => item.AddedThroughRequestId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class FamilyRelationshipRequestConfiguration : IWriteEntityConfiguration<FamilyRelationshipRequest>
{
    public void ConfigureAggregate(EntityTypeBuilder<FamilyRelationshipRequest> builder)
    {
        builder.ToTable("FamilyRelationshipRequests");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.RequestType).HasConversion<int>().IsRequired();
        builder.Property(item => item.RequesterClaimedRole).HasConversion<int>().IsRequired();
        builder.Property(item => item.TargetClaimedRole).HasConversion<int>().IsRequired();
        builder.Property(item => item.Status).HasConversion<int>().IsRequired();
        builder.Property(item => item.RejectionReason).HasMaxLength(2000);
        builder.Property(item => item.ModificationMessage).HasMaxLength(2000);
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(item => item.Status).HasDatabaseName("IX_FamilyRelationshipRequests_Status");
        builder.HasIndex(item => item.SubmittedOnUtc).HasDatabaseName("IX_FamilyRelationshipRequests_SubmittedOnUtc");
        builder.HasIndex(item => item.FamilyId).HasDatabaseName("IX_FamilyRelationshipRequests_FamilyId");
        builder.HasIndex(item => item.RequesterPatientId).HasDatabaseName("IX_FamilyRelationshipRequests_RequesterPatientId");
        builder.HasIndex(item => item.TargetPatientId).HasDatabaseName("IX_FamilyRelationshipRequests_TargetPatientId");
        builder.HasIndex(item => new { item.RequesterPatientId, item.TargetPatientId, item.RequestType, item.FamilyId })
            .IsUnique().HasFilter("[Status] IN (1, 2)").HasDatabaseName("UX_FamilyRelationshipRequests_OneOpenRelationship");
        builder.HasOne<Family>().WithMany().HasForeignKey(item => item.FamilyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Patient>().WithMany().HasForeignKey(item => item.RequesterPatientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Patient>().WithMany().HasForeignKey(item => item.TargetPatientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.SubmittedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ReviewedBySuperAdminApplicationUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class FamilyRelationshipDocumentConfiguration : IWriteEntityConfiguration<FamilyRelationshipDocument>
{
    public void ConfigureAggregate(EntityTypeBuilder<FamilyRelationshipDocument> builder)
    {
        builder.ToTable("FamilyRelationshipDocuments");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.DocumentType).HasConversion<int>().IsRequired();
        builder.Property(item => item.MediaKey).HasMaxLength(1000).IsRequired();
        builder.Property(item => item.OriginalFileName).HasMaxLength(500).IsRequired();
        builder.Property(item => item.ContentType).HasMaxLength(200).IsRequired();
        builder.HasIndex(item => new { item.FamilyRelationshipRequestId, item.RevisionNumber }).HasDatabaseName("IX_FamilyRelationshipDocuments_RequestId_RevisionNumber");
        builder.HasOne<FamilyRelationshipRequest>().WithMany().HasForeignKey(item => item.FamilyRelationshipRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UploadedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class FamilyRelationshipRequestHistoryConfiguration : IWriteEntityConfiguration<FamilyRelationshipRequestHistory>
{
    public void ConfigureAggregate(EntityTypeBuilder<FamilyRelationshipRequestHistory> builder)
    {
        builder.ToTable("FamilyRelationshipRequestHistories");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.OldStatus).HasConversion<int?>();
        builder.Property(item => item.NewStatus).HasConversion<int>().IsRequired();
        builder.Property(item => item.Action).HasConversion<int>().IsRequired();
        builder.Property(item => item.MessageOrReason).HasMaxLength(2000);
        builder.HasIndex(item => new { item.RequestId, item.PerformedOnUtc }).HasDatabaseName("IX_FamilyRelationshipRequestHistories_RequestId_PerformedOnUtc");
        builder.HasOne<FamilyRelationshipRequest>().WithMany().HasForeignKey(item => item.RequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.PerformedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
