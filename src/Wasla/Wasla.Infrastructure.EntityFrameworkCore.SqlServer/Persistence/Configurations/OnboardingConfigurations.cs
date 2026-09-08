using BuildingBlock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.Domain.Doctors;
using Wasla.Domain.ReferenceData;
using Wasla.Domain.Security;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Configurations;

internal sealed class MedicalSpecializationConfiguration : IWriteEntityConfiguration<MedicalSpecialization>
{
    public void ConfigureAggregate(EntityTypeBuilder<MedicalSpecialization> builder)
    {
        builder.ToTable("MedicalSpecializations");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.NameAr).HasMaxLength(200).IsRequired();
        builder.Property(item => item.NameEn).HasMaxLength(200);
        builder.Property(item => item.DescriptionAr).HasMaxLength(1000);
        builder.Property(item => item.DescriptionEn).HasMaxLength(1000);
        builder.Property(item => item.IsActive).IsRequired();
        builder.Property(item => item.SortOrder).HasDefaultValue(0).IsRequired();
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(item => item.NameAr).IsUnique().HasDatabaseName("UX_MedicalSpecializations_NameAr");
        builder.HasIndex(item => item.NameEn).IsUnique().HasFilter("[NameEn] IS NOT NULL")
            .HasDatabaseName("UX_MedicalSpecializations_NameEn");
        builder.HasIndex(item => new { item.IsActive, item.SortOrder })
            .HasDatabaseName("IX_MedicalSpecializations_IsActive_SortOrder");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.CreatedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ModifiedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.DeletedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.RestoredByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DoctorSpecializationConfiguration : IWriteEntityConfiguration<DoctorSpecialization>
{
    public void ConfigureAggregate(EntityTypeBuilder<DoctorSpecialization> builder)
    {
        builder.ToTable("DoctorSpecializations");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.IsPrimary).IsRequired();
        builder.HasIndex(item => new { item.DoctorId, item.MedicalSpecializationId })
            .IsUnique().HasDatabaseName("UX_DoctorSpecializations_DoctorId_MedicalSpecializationId");
        builder.HasIndex(item => item.DoctorId).IsUnique().HasFilter("[IsPrimary] = 1")
            .HasDatabaseName("UX_DoctorSpecializations_OnePrimaryPerDoctor");
        builder.HasOne<Doctor>().WithMany().HasForeignKey(item => item.DoctorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MedicalSpecialization>().WithMany().HasForeignKey(item => item.MedicalSpecializationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.CreatedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DoctorSpecializationRequestConfiguration : IWriteEntityConfiguration<DoctorSpecializationRequest>
{
    public void ConfigureAggregate(EntityTypeBuilder<DoctorSpecializationRequest> builder)
    {
        builder.ToTable("DoctorSpecializationRequests");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Type).HasConversion<int>().IsRequired();
        builder.Property(item => item.Status).HasConversion<int>().IsRequired();
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(item => item.DoctorId).IsUnique().HasFilter("[Status] IN (1, 2)")
            .HasDatabaseName("UX_DoctorSpecializationRequests_OneOpenPerDoctor");
        builder.HasIndex(item => new { item.Status, item.Type, item.SubmittedOnUtc })
            .HasDatabaseName("IX_DoctorSpecializationRequests_ReviewQueue");
        builder.HasOne<Doctor>().WithMany().HasForeignKey(item => item.DoctorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.SubmittedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ReviewedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DoctorSpecializationRequestRevisionConfiguration : IWriteEntityConfiguration<DoctorSpecializationRequestRevision>
{
    public void ConfigureAggregate(EntityTypeBuilder<DoctorSpecializationRequestRevision> builder)
    {
        builder.ToTable("DoctorSpecializationRequestRevisions");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.DoctorSpecializationRequestId, item.RevisionNumber })
            .IsUnique().HasDatabaseName("UX_DoctorSpecializationRequestRevisions_RequestId_RevisionNumber");
        builder.HasOne<DoctorSpecializationRequest>().WithMany().HasForeignKey(item => item.DoctorSpecializationRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.SubmittedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DoctorSpecializationRequestItemConfiguration : IWriteEntityConfiguration<DoctorSpecializationRequestItem>
{
    public void ConfigureAggregate(EntityTypeBuilder<DoctorSpecializationRequestItem> builder)
    {
        builder.ToTable("DoctorSpecializationRequestItems");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.IsPrimary).IsRequired();
        builder.HasIndex(item => new { item.RevisionId, item.MedicalSpecializationId })
            .IsUnique().HasDatabaseName("UX_DoctorSpecializationRequestItems_RevisionId_MedicalSpecializationId");
        builder.HasIndex(item => item.RevisionId).IsUnique().HasFilter("[IsPrimary] = 1")
            .HasDatabaseName("UX_DoctorSpecializationRequestItems_OnePrimaryPerRevision");
        builder.HasOne<DoctorSpecializationRequestRevision>().WithMany().HasForeignKey(item => item.RevisionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MedicalSpecialization>().WithMany().HasForeignKey(item => item.MedicalSpecializationId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DoctorSpecializationRequestHistoryConfiguration : IWriteEntityConfiguration<DoctorSpecializationRequestHistory>
{
    public void ConfigureAggregate(EntityTypeBuilder<DoctorSpecializationRequestHistory> builder)
    {
        builder.ToTable("DoctorSpecializationRequestHistories");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Action).HasConversion<int>().IsRequired();
        builder.Property(item => item.Message).HasMaxLength(2000);
        builder.HasIndex(item => new { item.DoctorSpecializationRequestId, item.PerformedOnUtc })
            .HasDatabaseName("IX_DoctorSpecializationRequestHistories_RequestId_PerformedOnUtc");
        builder.HasOne<DoctorSpecializationRequest>().WithMany().HasForeignKey(item => item.DoctorSpecializationRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.PerformedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class GovernorateConfiguration : IWriteEntityConfiguration<Governorate>
{
    public void ConfigureAggregate(EntityTypeBuilder<Governorate> builder)
    {
        builder.ToTable("Governorates");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.NameAr).HasMaxLength(150).IsRequired();
        builder.Property(item => item.NameEn).HasMaxLength(150);
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(item => item.NameAr).IsUnique().HasDatabaseName("UX_Governorates_NameAr");
        builder.HasIndex(item => item.NameEn).IsUnique().HasFilter("[NameEn] IS NOT NULL").HasDatabaseName("UX_Governorates_NameEn");
        builder.HasIndex(item => new { item.IsActive, item.DisplayOrder }).HasDatabaseName("IX_Governorates_IsActive_DisplayOrder");
    }
}

internal sealed class CityConfiguration : IWriteEntityConfiguration<City>
{
    public void ConfigureAggregate(EntityTypeBuilder<City> builder)
    {
        builder.ToTable("Cities");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.NameAr).HasMaxLength(150).IsRequired();
        builder.Property(item => item.NameEn).HasMaxLength(150);
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasOne(item => item.Governorate).WithMany().HasForeignKey(item => item.GovernorateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => item.GovernorateId).HasDatabaseName("IX_Cities_GovernorateId");
        builder.HasIndex(item => new { item.GovernorateId, item.IsActive, item.DisplayOrder }).HasDatabaseName("IX_Cities_GovernorateId_IsActive_DisplayOrder");
        builder.HasIndex(item => new { item.GovernorateId, item.NameAr }).IsUnique().HasDatabaseName("UX_Cities_GovernorateId_NameAr");
        builder.HasIndex(item => new { item.GovernorateId, item.NameEn }).IsUnique().HasFilter("[NameEn] IS NOT NULL").HasDatabaseName("UX_Cities_GovernorateId_NameEn");
    }
}

internal sealed class AreaConfiguration : IWriteEntityConfiguration<Area>
{
    public void ConfigureAggregate(EntityTypeBuilder<Area> builder)
    {
        builder.ToTable("Areas");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.NameAr).HasMaxLength(150).IsRequired();
        builder.Property(item => item.NameEn).HasMaxLength(150);
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasOne(item => item.City).WithMany().HasForeignKey(item => item.CityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => item.CityId).HasDatabaseName("IX_Areas_CityId");
        builder.HasIndex(item => new { item.CityId, item.IsActive, item.DisplayOrder }).HasDatabaseName("IX_Areas_CityId_IsActive_DisplayOrder");
        builder.HasIndex(item => new { item.CityId, item.NameAr }).IsUnique().HasDatabaseName("UX_Areas_CityId_NameAr");
        builder.HasIndex(item => new { item.CityId, item.NameEn }).IsUnique().HasFilter("[NameEn] IS NOT NULL").HasDatabaseName("UX_Areas_CityId_NameEn");
    }
}

internal sealed class DoctorPracticeLocationConfiguration : IWriteEntityConfiguration<DoctorPracticeLocation>
{
    public void ConfigureAggregate(EntityTypeBuilder<DoctorPracticeLocation> builder)
    {
        builder.ToTable("DoctorPracticeLocations");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.DetailedAddress).HasMaxLength(500).IsRequired();
        builder.Property(item => item.Latitude).HasPrecision(9, 6).IsRequired();
        builder.Property(item => item.Longitude).HasPrecision(9, 6).IsRequired();
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(item => item.DoctorId).IsUnique().HasDatabaseName("UX_DoctorPracticeLocations_DoctorId");
        builder.HasOne<Doctor>().WithMany().HasForeignKey(item => item.DoctorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Governorate>().WithMany().HasForeignKey(item => item.GovernorateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<City>().WithMany().HasForeignKey(item => item.CityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Area>().WithMany().HasForeignKey(item => item.AreaId).OnDelete(DeleteBehavior.Restrict);
    }
}
