using BuildingBlock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.Domain.Clinical;
using Wasla.Domain.Doctors;
using Wasla.Domain.Patients;
using Wasla.Domain.Practices;
using Wasla.Domain.Reservations;
using Wasla.Domain.Security;
using Wasla.Domain.Tickets;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Configurations;

internal sealed class MedicalEncounterConfiguration : IWriteEntityConfiguration<MedicalEncounter>
{
    public void ConfigureAggregate(EntityTypeBuilder<MedicalEncounter> builder)
    {
        builder.ToTable("MedicalEncounters", table =>
        {
            table.HasCheckConstraint("CK_MedicalEncounters_Status", "[Status] IN (1,2)");
            table.HasCheckConstraint("CK_MedicalEncounters_Completion", "([Status] = 1 AND [CompletedAtUtc] IS NULL) OR ([Status] = 2 AND [CompletedAtUtc] IS NOT NULL AND LEN(LTRIM(RTRIM([ClinicalNotes]))) > 0 AND [ClinicalNotes] IS NOT NULL)");
        });
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ClinicalNotes).HasMaxLength(8000);
        builder.Property(e => e.StartedAtUtc).HasColumnType("datetime2(3)");
        builder.Property(e => e.CompletedAtUtc).HasColumnType("datetime2(3)");
        builder.Property(e => e.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasOne<Ticket>().WithMany().HasForeignKey(e => e.TicketId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Doctor>().WithMany().HasForeignKey(e => e.DoctorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DoctorPractice>().WithMany().HasForeignKey(e => e.DoctorPracticeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Patient>().WithMany().HasForeignKey(e => e.PatientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(e => e.CreatedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(e => e.ModifiedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => e.TicketId).IsUnique().HasDatabaseName("UX_MedicalEncounters_TicketId");
        builder.HasIndex(e => new { e.DoctorId, e.DoctorPracticeId, e.Status, e.StartedAtUtc });
        builder.HasIndex(e => new { e.PatientId, e.Status, e.CompletedAtUtc });
        builder.HasMany(e => e.Diagnoses).WithOne().HasForeignKey(d => d.MedicalEncounterId).OnDelete(DeleteBehavior.ClientCascade);
        builder.HasMany(e => e.Amendments).WithOne().HasForeignKey(a => a.MedicalEncounterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.AuditEvents).WithOne().HasForeignKey(a => a.MedicalEncounterId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(e => e.Diagnoses).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(e => e.Amendments).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(e => e.AuditEvents).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class DiagnosisConfiguration : IWriteEntityConfiguration<Diagnosis>
{
    public void ConfigureAggregate(EntityTypeBuilder<Diagnosis> builder)
    {
        builder.ToTable("Diagnoses", table => table.HasCheckConstraint("CK_Diagnoses_Type", "[Type] IN (1,2)"));
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.Property(d => d.DisplayText).HasMaxLength(500).IsRequired();
        // Unicode uppercasing may expand characters. This remains below SQL Server's 1700-byte index limit.
        builder.Property(d => d.NormalizedDisplayText).HasMaxLength(800).IsRequired();
        builder.Property(d => d.Notes).HasMaxLength(2000);
        builder.HasIndex(d => d.MedicalEncounterId).IsUnique().HasFilter("[Type] = 1 AND [IsVoided] = 0")
            .HasDatabaseName("UX_Diagnoses_ActivePrimary");
        builder.HasIndex(d => new { d.MedicalEncounterId, d.NormalizedDisplayText }).IsUnique()
            .HasFilter("[IsVoided] = 0").HasDatabaseName("UX_Diagnoses_ActiveText");
    }
}

internal sealed class EncounterAmendmentConfiguration : IWriteEntityConfiguration<EncounterAmendment>
{
    public void ConfigureAggregate(EntityTypeBuilder<EncounterAmendment> builder)
    {
        builder.ToTable("EncounterAmendments");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(a => a.CreatedAtUtc).HasColumnType("datetime2(3)");
        builder.HasIndex(a => new { a.MedicalEncounterId, a.SequenceNumber }).IsUnique()
            .HasDatabaseName("UX_EncounterAmendments_Sequence");
        builder.HasOne<Doctor>().WithMany().HasForeignKey(a => a.CreatedByDoctorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(a => a.CreatedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(a => a.Changes).WithOne().HasForeignKey(c => c.EncounterAmendmentId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(a => a.Changes).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class EncounterAmendmentChangeConfiguration : IWriteEntityConfiguration<EncounterAmendmentChange>
{
    public void ConfigureAggregate(EntityTypeBuilder<EncounterAmendmentChange> builder)
    {
        builder.ToTable("EncounterAmendmentChanges");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.BeforeSnapshot);
        builder.Property(c => c.AfterSnapshot);
    }
}

internal sealed class EncounterAuditEventConfiguration : IWriteEntityConfiguration<EncounterAuditEvent>
{
    public void ConfigureAggregate(EntityTypeBuilder<EncounterAuditEvent> builder)
    {
        builder.ToTable("EncounterAuditEvents");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Action).HasMaxLength(100).IsRequired();
        builder.Property(e => e.OccurredAtUtc).HasColumnType("datetime2(3)");
        builder.HasIndex(e => new { e.MedicalEncounterId, e.OccurredAtUtc });
    }
}

internal sealed class FollowUpEligibilityConfiguration : IWriteEntityConfiguration<FollowUpEligibility>
{
    public void ConfigureAggregate(EntityTypeBuilder<FollowUpEligibility> builder)
    {
        builder.ToTable("FollowUpEligibilities", table =>
        {
            table.HasCheckConstraint("CK_FollowUpEligibilities_Status", "[Status] IN (1,2,3,4)");
            table.HasCheckConstraint("CK_FollowUpEligibilities_Claim", "([Status] = 2 AND (([ReservedReservationId] IS NOT NULL AND [ReservedTicketId] IS NULL) OR ([ReservedReservationId] IS NULL AND [ReservedTicketId] IS NOT NULL)) AND [ConsumedEncounterId] IS NULL) OR ([Status] IN (1,4) AND [ReservedReservationId] IS NULL AND [ReservedTicketId] IS NULL AND [ConsumedEncounterId] IS NULL) OR ([Status] = 3 AND [ReservedReservationId] IS NULL AND [ReservedTicketId] IS NULL AND [ConsumedEncounterId] IS NOT NULL)");
        });
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ValidUntil).HasColumnType("date");
        builder.Property(e => e.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasOne<MedicalEncounter>().WithMany().HasForeignKey(e => e.SourceMedicalEncounterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MedicalEncounter>().WithMany().HasForeignKey(e => e.ConsumedEncounterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DoctorPractice>().WithMany().HasForeignKey(e => e.DoctorPracticeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Doctor>().WithMany().HasForeignKey(e => e.DoctorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Patient>().WithMany().HasForeignKey(e => e.PatientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Reservation>().WithMany().HasForeignKey(e => e.ReservedReservationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Ticket>().WithMany().HasForeignKey(e => e.ReservedTicketId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => e.SourceMedicalEncounterId).IsUnique().HasDatabaseName("UX_FollowUpEligibilities_Source");
        builder.HasIndex(e => e.ReservedReservationId).IsUnique().HasFilter("[ReservedReservationId] IS NOT NULL");
        builder.HasIndex(e => e.ReservedTicketId).IsUnique().HasFilter("[ReservedTicketId] IS NOT NULL");
        builder.HasIndex(e => e.ConsumedEncounterId).IsUnique().HasFilter("[ConsumedEncounterId] IS NOT NULL");
        builder.HasIndex(e => new { e.PatientId, e.Status, e.ValidUntil });
        builder.HasIndex(e => new { e.DoctorId, e.DoctorPracticeId, e.Status, e.ValidUntil });
        builder.HasMany(e => e.History).WithOne().HasForeignKey(h => h.FollowUpEligibilityId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(e => e.History).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class FollowUpEligibilityHistoryConfiguration : IWriteEntityConfiguration<FollowUpEligibilityHistory>
{
    public void ConfigureAggregate(EntityTypeBuilder<FollowUpEligibilityHistory> builder)
    {
        builder.ToTable("FollowUpEligibilityHistories");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).ValueGeneratedNever();
        builder.Property(h => h.Action).HasMaxLength(100).IsRequired();
        builder.Property(h => h.OccurredAtUtc).HasColumnType("datetime2(3)");
        builder.HasIndex(h => new { h.FollowUpEligibilityId, h.OccurredAtUtc });
    }
}
