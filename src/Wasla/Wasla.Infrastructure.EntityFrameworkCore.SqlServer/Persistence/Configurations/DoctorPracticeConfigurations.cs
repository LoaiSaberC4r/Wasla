using BuildingBlock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.Domain.Practices;
using Wasla.Domain.Security;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Configurations;

internal sealed class DoctorPracticeOperationalConfiguration
    : IWriteEntityConfiguration<Wasla.Domain.Practices.DoctorPracticeConfiguration>
{
    public void ConfigureAggregate(EntityTypeBuilder<Wasla.Domain.Practices.DoctorPracticeConfiguration> builder)
    {
        builder.ToTable("DoctorPracticeConfigurations");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.TimeZoneId).HasMaxLength(100).IsRequired();
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(item => item.DoctorPracticeId).IsUnique()
            .HasDatabaseName("UX_DoctorPracticeConfigurations_DoctorPracticeId");
        builder.HasOne<DoctorPractice>().WithMany().HasForeignKey(item => item.DoctorPracticeId)
            .OnDelete(DeleteBehavior.Restrict);
        ConfigureActors(builder);
    }

    private static void ConfigureActors(EntityTypeBuilder<Wasla.Domain.Practices.DoctorPracticeConfiguration> builder)
    {
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.CreatedByApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ModifiedByApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DoctorPracticeBrandingConfiguration : IWriteEntityConfiguration<DoctorPracticeBranding>
{
    public void ConfigureAggregate(EntityTypeBuilder<DoctorPracticeBranding> builder)
    {
        builder.ToTable("DoctorPracticeBrandings");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.LogoMediaKey).HasMaxLength(1000);
        builder.Property(item => item.PrimaryColor).HasMaxLength(7);
        builder.Property(item => item.SecondaryColor).HasMaxLength(7);
        builder.Property(item => item.BackgroundColor).HasMaxLength(7);
        builder.Property(item => item.TextColor).HasMaxLength(7);
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(item => item.DoctorPracticeId).IsUnique()
            .HasDatabaseName("UX_DoctorPracticeBrandings_DoctorPracticeId");
        builder.HasOne<DoctorPractice>().WithMany().HasForeignKey(item => item.DoctorPracticeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.CreatedByApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ModifiedByApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DoctorPracticeSchedulePeriodConfiguration
    : IWriteEntityConfiguration<DoctorPracticeSchedulePeriod>
{
    public void ConfigureAggregate(EntityTypeBuilder<DoctorPracticeSchedulePeriod> builder)
    {
        builder.ToTable("DoctorPracticeSchedulePeriods");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.DayOfWeek).HasConversion<int>().IsRequired();
        builder.Property(item => item.StartTime).HasColumnType("time").IsRequired();
        builder.Property(item => item.EndTime).HasColumnType("time").IsRequired();
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(item => new { item.DoctorPracticeId, item.DayOfWeek })
            .HasDatabaseName("IX_DoctorPracticeSchedulePeriods_Practice_Day");
        builder.HasOne<DoctorPractice>().WithMany().HasForeignKey(item => item.DoctorPracticeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.CreatedByApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ModifiedByApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DoctorPracticeScheduleExceptionConfiguration
    : IWriteEntityConfiguration<DoctorPracticeScheduleException>
{
    public void ConfigureAggregate(EntityTypeBuilder<DoctorPracticeScheduleException> builder)
    {
        builder.ToTable("DoctorPracticeScheduleExceptions");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Type).HasConversion<int>().IsRequired();
        builder.Property(item => item.StartTime).HasColumnType("time");
        builder.Property(item => item.EndTime).HasColumnType("time");
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(item => new { item.DoctorPracticeId, item.Date })
            .HasDatabaseName("IX_DoctorPracticeScheduleExceptions_Practice_Date");
        builder.HasOne<DoctorPractice>().WithMany().HasForeignKey(item => item.DoctorPracticeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.CreatedByApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ModifiedByApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DoctorPracticeSegmentConfiguration : IWriteEntityConfiguration<DoctorPracticeSegment>
{
    public void ConfigureAggregate(EntityTypeBuilder<DoctorPracticeSegment> builder)
    {
        builder.ToTable("DoctorPracticeSegments");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.NameAr).HasMaxLength(200).IsRequired();
        builder.Property(item => item.NameEn).HasMaxLength(200);
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex([nameof(DoctorPracticeSegment.DoctorPracticeId)], "IX_DoctorPracticeSegments_PracticeId");
        builder.HasIndex(
                [nameof(DoctorPracticeSegment.DoctorPracticeId)],
                "UX_DoctorPracticeSegments_OneDefaultPerPractice")
            .IsUnique().HasFilter("[IsDefault] = 1");
        builder.HasAlternateKey(item => new { item.Id, item.DoctorPracticeId })
            .HasName("AK_DoctorPracticeSegments_Id_PracticeId");
        builder.HasOne<DoctorPractice>().WithMany().HasForeignKey(item => item.DoctorPracticeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.CreatedByApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ModifiedByApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DoctorPracticeVisitTypeConfiguration : IWriteEntityConfiguration<DoctorPracticeVisitType>
{
    public void ConfigureAggregate(EntityTypeBuilder<DoctorPracticeVisitType> builder)
    {
        builder.ToTable("DoctorPracticeVisitTypes");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Type).HasConversion<int>().IsRequired();
        builder.Property(item => item.NameAr).HasMaxLength(200).IsRequired();
        builder.Property(item => item.NameEn).HasMaxLength(200);
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(item => new { item.DoctorPracticeId, item.Type }).IsUnique()
            .HasDatabaseName("UX_DoctorPracticeVisitTypes_Practice_Type");
        builder.HasAlternateKey(item => new { item.Id, item.DoctorPracticeId })
            .HasName("AK_DoctorPracticeVisitTypes_Id_PracticeId");
        builder.HasOne<DoctorPractice>().WithMany().HasForeignKey(item => item.DoctorPracticeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.CreatedByApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ModifiedByApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DoctorPracticePriceConfiguration
    : IWriteEntityConfiguration<DoctorPracticeSegmentVisitTypePrice>
{
    public void ConfigureAggregate(EntityTypeBuilder<DoctorPracticeSegmentVisitTypePrice> builder)
    {
        builder.ToTable("DoctorPracticeSegmentVisitTypePrices");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Price).HasPrecision(18, 2).IsRequired();
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(item => new { item.DoctorPracticeId, item.SegmentId, item.VisitTypeId }).IsUnique()
            .HasDatabaseName("UX_DoctorPracticePrices_Practice_Segment_VisitType");
        builder.HasOne<DoctorPractice>().WithMany().HasForeignKey(item => item.DoctorPracticeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DoctorPracticeSegment>().WithMany()
            .HasForeignKey(item => new { item.SegmentId, item.DoctorPracticeId })
            .HasPrincipalKey(item => new { item.Id, item.DoctorPracticeId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DoctorPracticeVisitType>().WithMany()
            .HasForeignKey(item => new { item.VisitTypeId, item.DoctorPracticeId })
            .HasPrincipalKey(item => new { item.Id, item.DoctorPracticeId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.CreatedByApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ModifiedByApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PublicPracticeAvailabilitySlotConfiguration
    : IWriteEntityConfiguration<PublicPracticeAvailabilitySlot>
{
    public void ConfigureAggregate(EntityTypeBuilder<PublicPracticeAvailabilitySlot> builder)
    {
        builder.ToTable("PublicPracticeAvailabilitySlots");
        builder.HasKey(item => new { item.DoctorPracticeId, item.LocalDate, item.LocalTime });
        builder.Property(item => item.LocalTime).HasColumnType("time").IsRequired();
        builder.Property(item => item.SlotStartUtc).HasColumnType("datetime2").IsRequired();
        builder.Property(item => item.VisibleFromUtc).HasColumnType("datetime2").IsRequired();
        builder.Property(item => item.RefreshedOnUtc).HasColumnType("datetime2").IsRequired();
        builder.HasIndex(item => new
            {
                item.DoctorId,
                item.IsAvailable,
                item.VisibleFromUtc,
                item.SlotStartUtc
            })
            .HasDatabaseName("IX_PublicAvailabilitySlots_Doctor_Availability");
        builder.HasIndex(item => new { item.DoctorPracticeId, item.SlotStartUtc })
            .HasDatabaseName("IX_PublicAvailabilitySlots_Practice_Start");
    }
}

internal sealed class PublicDoctorSearchRankConfiguration
    : IWriteEntityConfiguration<PublicDoctorSearchRank>
{
    public void ConfigureAggregate(EntityTypeBuilder<PublicDoctorSearchRank> builder)
    {
        builder.ToTable("PublicDoctorSearchRanks");
        builder.HasKey(item => item.DoctorId);
        builder.Property(item => item.RefreshedOnUtc).HasColumnType("datetime2").IsRequired();
        builder.HasIndex(item => new { item.PopularityScore, item.DoctorId })
            .IsDescending(true, false)
            .HasDatabaseName("IX_PublicDoctorSearchRanks_Popularity_Doctor");
    }
}

internal sealed class ReceptionConfiguration : IWriteEntityConfiguration<Reception>
{
    public void ConfigureAggregate(EntityTypeBuilder<Reception> builder)
    {
        builder.ToTable("Receptions");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.NameAr).HasMaxLength(200).IsRequired();
        builder.Property(item => item.NameEn).HasMaxLength(200);
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(item => item.ApplicationUserId).IsUnique().HasDatabaseName("UX_Receptions_ApplicationUserId");
        builder.HasIndex(item => item.OwnerDoctorId).HasDatabaseName("IX_Receptions_OwnerDoctorId");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Wasla.Domain.Doctors.Doctor>().WithMany().HasForeignKey(item => item.OwnerDoctorId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.CreatedByApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ReceptionPracticeAssignmentConfiguration
    : IWriteEntityConfiguration<ReceptionPracticeAssignment>
{
    public void ConfigureAggregate(EntityTypeBuilder<ReceptionPracticeAssignment> builder)
    {
        builder.ToTable("ReceptionPracticeAssignments");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(item => new { item.ReceptionId, item.DoctorPracticeId }).IsUnique()
            .HasDatabaseName("UX_ReceptionPracticeAssignments_Reception_Practice");
        builder.HasIndex(item => item.DoctorPracticeId)
            .HasDatabaseName("IX_ReceptionPracticeAssignments_PracticeId");
        builder.HasOne<Reception>().WithMany().HasForeignKey(item => item.ReceptionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DoctorPractice>().WithMany().HasForeignKey(item => item.DoctorPracticeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.CreatedByApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ModifiedByApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ReceptionPracticeAssignmentPermissionConfiguration
    : IWriteEntityConfiguration<ReceptionPracticeAssignmentPermission>
{
    public void ConfigureAggregate(EntityTypeBuilder<ReceptionPracticeAssignmentPermission> builder)
    {
        builder.ToTable("ReceptionPracticeAssignmentPermissions");
        builder.HasKey(item => item.Id);
        builder.HasIndex(item => new { item.AssignmentId, item.PermissionId }).IsUnique()
            .HasDatabaseName("UX_ReceptionAssignmentPermissions_Assignment_Permission");
        builder.HasOne<ReceptionPracticeAssignment>().WithMany().HasForeignKey(item => item.AssignmentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Permission>().WithMany().HasForeignKey(item => item.PermissionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.CreatedByApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
