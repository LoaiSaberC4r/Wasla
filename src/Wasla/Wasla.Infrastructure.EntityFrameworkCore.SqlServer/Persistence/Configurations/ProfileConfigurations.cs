using BuildingBlock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.Domain.Doctors;
using Wasla.Domain.Patients;
using Wasla.Domain.Security;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Configurations;

internal sealed class DoctorConfiguration : IWriteEntityConfiguration<Doctor>
{
    public void ConfigureAggregate(EntityTypeBuilder<Doctor> builder)
    {
        builder.ToTable("Doctors");
        builder.HasKey(doctor => doctor.Id);
        builder.Property(doctor => doctor.NameAr).HasMaxLength(200).IsRequired();
        builder.Property(doctor => doctor.NameEn).HasMaxLength(200);
        builder.Property(doctor => doctor.Gender).HasConversion<int>().IsRequired();
        builder.Property(doctor => doctor.ApprovalStatus).HasConversion<int>().IsRequired();
        builder.Property(doctor => doctor.ProfileImageMediaKey).HasMaxLength(1000);
        builder.Property(doctor => doctor.PersonalIdFrontMediaKey).HasMaxLength(1000).IsRequired();
        builder.Property(doctor => doctor.PersonalIdBackMediaKey).HasMaxLength(1000).IsRequired();
        builder.Property(doctor => doctor.SyndicateCardFrontMediaKey).HasMaxLength(1000).IsRequired();
        builder.Property(doctor => doctor.SyndicateCardBackMediaKey).HasMaxLength(1000);
        builder.Property(doctor => doctor.NationalId).HasMaxLength(100);
        builder.Property(doctor => doctor.RejectionReason).HasMaxLength(1000);
        builder.Property(doctor => doctor.SuspensionReason).HasMaxLength(1000);
        builder.Property(doctor => doctor.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(doctor => doctor.ApplicationUserId).IsUnique().HasDatabaseName("UX_Doctors_ApplicationUserId");
        builder.HasIndex(doctor => doctor.NationalId)
            .IsUnique()
            .HasFilter("[NationalId] IS NOT NULL")
            .HasDatabaseName("UX_Doctors_NationalId");
        builder.HasIndex(doctor => doctor.ApprovalStatus).HasDatabaseName("IX_Doctors_ApprovalStatus");
        builder.HasIndex(doctor => doctor.CreatedOnUtc).HasDatabaseName("IX_Doctors_CreatedOnUtc");
        builder.HasOne(doctor => doctor.ApplicationUser)
            .WithOne()
            .HasForeignKey<Doctor>(doctor => doctor.ApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(doctor => doctor.ApprovedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(doctor => doctor.RejectedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(doctor => doctor.SuspendedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(doctor => doctor.ReactivatedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DoctorStatusHistoryConfiguration : IWriteEntityConfiguration<DoctorStatusHistory>
{
    public void ConfigureAggregate(EntityTypeBuilder<DoctorStatusHistory> builder)
    {
        builder.ToTable("DoctorStatusHistories");
        builder.HasKey(history => history.Id);
        builder.Property(history => history.FromStatus).HasConversion<int?>();
        builder.Property(history => history.ToStatus).HasConversion<int>().IsRequired();
        builder.Property(history => history.Reason).HasMaxLength(1000);
        builder.HasIndex(history => new { history.DoctorId, history.OccurredOnUtc })
            .HasDatabaseName("IX_DoctorStatusHistories_DoctorId_OccurredOnUtc");
        builder.HasOne<Doctor>().WithMany().HasForeignKey(history => history.DoctorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(history => history.PerformedByApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PatientConfiguration : IWriteEntityConfiguration<Patient>
{
    public void ConfigureAggregate(EntityTypeBuilder<Patient> builder)
    {
        builder.ToTable("Patients");
        builder.HasKey(patient => patient.Id);
        builder.Property(patient => patient.NameAr).HasMaxLength(200).IsRequired();
        builder.Property(patient => patient.NameEn).HasMaxLength(200);
        builder.Property(patient => patient.Gender).HasConversion<int>().IsRequired();
        builder.Property(patient => patient.ProfileImageMediaKey).HasMaxLength(1000);
        builder.Property(patient => patient.PersonalIdFrontMediaKey).HasMaxLength(1000);
        builder.Property(patient => patient.PersonalIdBackMediaKey).HasMaxLength(1000);
        builder.HasIndex(patient => patient.ApplicationUserId).IsUnique().HasDatabaseName("UX_Patients_ApplicationUserId");
        builder.HasOne(patient => patient.ApplicationUser)
            .WithOne()
            .HasForeignKey<Patient>(patient => patient.ApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
