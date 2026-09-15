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
        builder.Property(doctor => doctor.NormalizedNameAr).HasMaxLength(200).IsRequired();
        builder.Property(doctor => doctor.NormalizedNameEn).HasMaxLength(200);
        builder.Property(doctor => doctor.Bio).HasMaxLength(2000);
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
        builder.HasIndex(doctor => doctor.NormalizedNameAr).HasDatabaseName("IX_Doctors_NormalizedNameAr");
        builder.HasIndex(doctor => doctor.NormalizedNameEn).HasDatabaseName("IX_Doctors_NormalizedNameEn");
        builder.HasIndex(doctor => doctor.CreatedOnUtc).HasDatabaseName("IX_Doctors_CreatedOnUtc");
        builder.HasOne(doctor => doctor.ApplicationUser)
            .WithOne()
            .HasForeignKey<Doctor>(doctor => doctor.ApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(doctor => doctor.ApprovedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(doctor => doctor.RejectedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(doctor => doctor.SuspendedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(doctor => doctor.ReactivatedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(doctor => doctor.ModifiedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DoctorQualificationConfiguration : IWriteEntityConfiguration<DoctorQualification>
{
    public void ConfigureAggregate(EntityTypeBuilder<DoctorQualification> builder)
    {
        builder.ToTable("DoctorQualifications");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.NameAr).HasMaxLength(300).IsRequired();
        builder.Property(item => item.NameEn).HasMaxLength(300);
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(item => new { item.DoctorId, item.DisplayOrder })
            .HasDatabaseName("IX_DoctorQualifications_DoctorId_DisplayOrder");
        builder.HasOne<Doctor>().WithMany().HasForeignKey(item => item.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.CreatedByApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ModifiedByApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);
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
        builder.Property(patient => patient.PhoneNumber).HasMaxLength(30);
        builder.Property(patient => patient.Email).HasMaxLength(200);
        builder.Property(patient => patient.ProfileImageMediaKey).HasMaxLength(1000);
        builder.Property(patient => patient.PersonalIdFrontMediaKey).HasMaxLength(1000);
        builder.Property(patient => patient.PersonalIdBackMediaKey).HasMaxLength(1000);
        builder.Property(patient => patient.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(patient => patient.PhoneNumber).HasDatabaseName("IX_Patients_PhoneNumber");
        builder.HasIndex(patient => patient.DateOfBirth).HasDatabaseName("IX_Patients_DateOfBirth");
        builder.HasIndex(patient => patient.NameAr).HasDatabaseName("IX_Patients_NameAr");
    }
}

internal sealed class PatientAccountLinkConfiguration : IWriteEntityConfiguration<PatientAccountLink>
{
    public void ConfigureAggregate(EntityTypeBuilder<PatientAccountLink> builder)
    {
        builder.ToTable("PatientAccountLinks");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Source).HasConversion<int>().IsRequired();
        builder.HasIndex(item => item.ApplicationUserId).IsUnique().HasDatabaseName("UX_PatientAccountLinks_ApplicationUserId");
        builder.HasIndex(item => item.PatientId).IsUnique().HasDatabaseName("UX_PatientAccountLinks_PatientId");
        builder.HasOne<ApplicationUser>().WithOne().HasForeignKey<PatientAccountLink>(item => item.ApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Patient>().WithOne().HasForeignKey<PatientAccountLink>(item => item.PatientId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PatientContactConfiguration : IWriteEntityConfiguration<PatientContact>
{
    public void ConfigureAggregate(EntityTypeBuilder<PatientContact> builder)
    {
        builder.ToTable("PatientContacts");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.NameAr).HasMaxLength(200).IsRequired();
        builder.Property(item => item.NameEn).HasMaxLength(200);
        builder.Property(item => item.PhoneNumber).HasMaxLength(30).IsRequired();
        builder.Property(item => item.RelationshipType).HasConversion<int>().IsRequired();
        builder.Property(item => item.IsPrimary).IsRequired();
        builder.HasIndex(item => item.PatientId).HasDatabaseName("IX_PatientContacts_PatientId");
        builder.HasIndex(item => item.PhoneNumber).HasDatabaseName("IX_PatientContacts_PhoneNumber");
        builder.HasIndex(item => item.LinkedPatientId).HasDatabaseName("IX_PatientContacts_LinkedPatientId");
        builder.HasOne<Patient>().WithMany().HasForeignKey(item => item.PatientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Patient>().WithMany().HasForeignKey(item => item.LinkedPatientId).OnDelete(DeleteBehavior.Restrict);
    }
}
