using BuildingBlock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.Domain.Doctors;
using Wasla.Domain.Patients;
using Wasla.Domain.Practices;
using Wasla.Domain.Reservations;
using Wasla.Domain.Security;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Configurations;

internal sealed class ReservationConfiguration : IWriteEntityConfiguration<Reservation>
{
    public void ConfigureAggregate(EntityTypeBuilder<Reservation> builder)
    {
        builder.ToTable("Reservations", table =>
        {
            table.HasCheckConstraint("CK_Reservations_Status", "[Status] IN (1,2,3,4,5)");
            table.HasCheckConstraint("CK_Reservations_Duration", "[SlotDurationMinutesSnapshot] > 0");
            table.HasCheckConstraint("CK_Reservations_Price", "[PriceSnapshot] > 0");
            table.HasCheckConstraint("CK_Reservations_RescheduleCount", "[PatientInitiatedRescheduleCount] >= 0 AND [PatientInitiatedRescheduleCount] <= 2");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.ReservationReference).HasMaxLength(32).IsRequired();
        builder.Property(item => item.BookedOnBehalfRelationshipSnapshot).HasMaxLength(100);
        builder.Property(item => item.ScheduledStartUtc).HasColumnType("datetime2(3)").IsRequired();
        builder.Property(item => item.ScheduledLocalDateTime).HasColumnType("datetime2(0)").IsRequired();
        builder.Property(item => item.BusinessDate).HasColumnType("date").IsRequired();
        builder.Property(item => item.TimeZoneIdSnapshot).HasMaxLength(100).IsRequired();
        builder.Property(item => item.SegmentNameArSnapshot).HasMaxLength(200).IsRequired();
        builder.Property(item => item.SegmentNameEnSnapshot).HasMaxLength(200);
        builder.Property(item => item.VisitTypeCodeSnapshot).HasMaxLength(100).IsRequired();
        builder.Property(item => item.VisitTypeNameArSnapshot).HasMaxLength(200).IsRequired();
        builder.Property(item => item.VisitTypeNameEnSnapshot).HasMaxLength(200);
        builder.Property(item => item.PriceSnapshot).HasPrecision(18, 2);
        builder.Property(item => item.BookingNote).HasMaxLength(ReservationPolicy.BookingNoteMaxLength);
        builder.Property(item => item.CancellationReasonCode).HasMaxLength(100);
        builder.Property(item => item.CancellationComment).HasMaxLength(ReservationPolicy.ReasonMaxLength);
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.Ignore(item => item.ScheduledEndUtc);
        builder.Ignore(item => item.ConsumesCapacity);

        builder.HasOne<Doctor>().WithMany().HasForeignKey(item => item.DoctorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DoctorPractice>().WithMany().HasForeignKey(item => item.DoctorPracticeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Patient>().WithMany().HasForeignKey(item => item.PatientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DoctorPracticeSegment>().WithMany().HasForeignKey(item => item.SegmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DoctorPracticeVisitType>().WithMany().HasForeignKey(item => item.VisitTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.CreatedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ModifiedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.CancelledByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(item => item.History).WithOne().HasForeignKey(item => item.ReservationId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(item => item.History).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(item => item.ReservationReference).IsUnique()
            .HasDatabaseName("UX_Reservations_Reference");
        builder.HasIndex(item => new { item.DoctorPracticeId, item.ScheduledStartUtc }).IsUnique()
            .HasFilter("[Status] IN (1,5)")
            .HasDatabaseName("UX_Reservations_ConsumingPracticeSlot");
        builder.HasIndex(item => new { item.PatientId, item.DoctorPracticeId, item.BusinessDate }).IsUnique()
            .HasFilter("[Status] = 1")
            .HasDatabaseName("UX_Reservations_ActivePatientPracticeDate");
        builder.HasIndex(item => new { item.DoctorPracticeId, item.BusinessDate, item.Status });
        builder.HasIndex(item => new { item.DoctorPracticeId, item.BusinessDate, item.SegmentId, item.Status });
        builder.HasIndex(item => new { item.PatientId, item.Status, item.ScheduledStartUtc });
        builder.HasIndex(item => new { item.DoctorId, item.Status, item.ScheduledStartUtc });
        builder.HasIndex(item => new { item.DoctorId, item.ConvertedToTicketOnUtc });
        builder.HasIndex(item => new { item.Status, item.BusinessDate });
    }
}

internal sealed class ReservationHistoryConfiguration : IWriteEntityConfiguration<ReservationHistory>
{
    public void ConfigureAggregate(EntityTypeBuilder<ReservationHistory> builder)
    {
        builder.ToTable("ReservationHistories");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.OccurredOnUtc).HasColumnType("datetime2(3)").IsRequired();
        builder.Property(item => item.ReasonCode).HasMaxLength(100);
        builder.Property(item => item.Reason).HasMaxLength(ReservationPolicy.ReasonMaxLength);
        builder.Property(item => item.OldScheduledLocalDateTime).HasColumnType("datetime2(0)");
        builder.Property(item => item.OldScheduledStartUtc).HasColumnType("datetime2(3)");
        builder.Property(item => item.NewScheduledLocalDateTime).HasColumnType("datetime2(0)");
        builder.Property(item => item.NewScheduledStartUtc).HasColumnType("datetime2(3)");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.PerformedByApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.ReservationId, item.OccurredOnUtc });
    }
}

internal sealed class ReservationIdempotencyRecordConfiguration
    : IWriteEntityConfiguration<ReservationIdempotencyRecord>
{
    public void ConfigureAggregate(EntityTypeBuilder<ReservationIdempotencyRecord> builder)
    {
        builder.ToTable("ReservationIdempotencyRecords");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Operation).HasMaxLength(100).IsRequired();
        builder.Property(item => item.IdempotencyKey).HasMaxLength(200).IsRequired();
        builder.Property(item => item.RequestFingerprint).HasMaxLength(64).IsRequired();
        builder.Property(item => item.ResultReference).HasMaxLength(32);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ActorApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Reservation>().WithMany().HasForeignKey(item => item.ReservationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.ActorApplicationUserId, item.Operation, item.IdempotencyKey })
            .IsUnique()
            .HasDatabaseName("UX_ReservationIdempotency_ActorOperationKey");
    }
}
