using BuildingBlock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wasla.Domain.Doctors;
using Wasla.Domain.Patients;
using Wasla.Domain.Payments;
using Wasla.Domain.Practices;
using Wasla.Domain.Reservations;
using Wasla.Domain.Security;
using Wasla.Domain.Tickets;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.Configurations;

internal sealed class TicketConfiguration : IWriteEntityConfiguration<Ticket>
{
    public void ConfigureAggregate(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("Tickets", table =>
        {
            table.HasCheckConstraint("CK_Tickets_Status", "[Status] IN (1,2,3,4,5,6)");
            table.HasCheckConstraint("CK_Tickets_Source", "[Source] IN (1,2)");
            table.HasCheckConstraint("CK_Tickets_Number", "[TicketNumber] > 0");
            table.HasCheckConstraint("CK_Tickets_Price", "[PriceSnapshot] > 0");
            table.HasCheckConstraint(
                "CK_Tickets_ReservationSource",
                "([Source] = 1 AND [ReservationId] IS NOT NULL) OR ([Source] = 2 AND [ReservationId] IS NULL)");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.BusinessDate).HasColumnType("date").IsRequired();
        builder.Property(item => item.SegmentNameArSnapshot).HasMaxLength(200).IsRequired();
        builder.Property(item => item.SegmentNameEnSnapshot).HasMaxLength(200);
        builder.Property(item => item.VisitTypeCodeSnapshot).HasMaxLength(100).IsRequired();
        builder.Property(item => item.VisitTypeNameArSnapshot).HasMaxLength(200).IsRequired();
        builder.Property(item => item.VisitTypeNameEnSnapshot).HasMaxLength(200);
        builder.Property(item => item.PriceSnapshot).HasPrecision(18, 2).IsRequired();
        builder.Property(item => item.PracticeTimeZoneIdSnapshot).HasMaxLength(100).IsRequired();
        builder.Property(item => item.CheckInTimeUtc).HasColumnType("datetime2(3)").IsRequired();
        builder.Property(item => item.QueueOrderTimeUtc).HasColumnType("datetime2(3)").IsRequired();
        builder.Property(item => item.FastTrackGrantedOnUtc).HasColumnType("datetime2(3)");
        builder.Property(item => item.CalledOnUtc).HasColumnType("datetime2(3)");
        builder.Property(item => item.NoShowOnUtc).HasColumnType("datetime2(3)");
        builder.Property(item => item.InProgressOnUtc).HasColumnType("datetime2(3)");
        builder.Property(item => item.CompletedOnUtc).HasColumnType("datetime2(3)");
        builder.Property(item => item.CancelledOnUtc).HasColumnType("datetime2(3)");
        builder.Property(item => item.LastUpdatedOnUtc).HasColumnType("datetime2(3)").IsRequired();
        builder.Property(item => item.CancellationReasonCode).HasMaxLength(TicketPolicy.ReasonCodeMaxLength);
        builder.Property(item => item.CancellationReason).HasMaxLength(TicketPolicy.ReasonMaxLength);
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.Ignore(item => item.IsOpen);

        builder.HasOne<Doctor>().WithMany().HasForeignKey(item => item.DoctorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DoctorPractice>().WithMany().HasForeignKey(item => item.DoctorPracticeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Patient>().WithMany().HasForeignKey(item => item.PatientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Reservation>().WithMany().HasForeignKey(item => item.ReservationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DoctorPracticeSegment>().WithMany().HasForeignKey(item => item.SegmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DoctorPracticeVisitType>().WithMany().HasForeignKey(item => item.VisitTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.CreatedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ModifiedByApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(item => item.History).WithOne().HasForeignKey(item => item.TicketId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(item => item.CallAttempts).WithOne().HasForeignKey(item => item.TicketId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(item => item.History).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(item => item.CallAttempts).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(item => item.ReservationId).IsUnique()
            .HasFilter("[ReservationId] IS NOT NULL")
            .HasDatabaseName("UX_Tickets_ReservationId");
        builder.HasIndex(item => new { item.DoctorPracticeId, item.BusinessDate, item.TicketNumber }).IsUnique()
            .HasDatabaseName("UX_Tickets_PracticeDateNumber");
        builder.HasIndex(item => new { item.PatientId, item.DoctorPracticeId }).IsUnique()
            .HasFilter("[Status] IN (1,2,3,4)")
            .HasDatabaseName("UX_Tickets_OpenPatientPractice");
        builder.HasIndex(item => item.DoctorPracticeId).IsUnique()
            .HasFilter("[Status] IN (2,3)")
            .HasDatabaseName("UX_Tickets_OneCalledOrInProgressPerPractice");
        builder.HasIndex(item => new { item.DoctorPracticeId, item.BusinessDate, item.Status });
        builder.HasIndex(item => new
        {
            item.DoctorPracticeId,
            item.BusinessDate,
            item.Status,
            item.IsFastTrack,
            item.SegmentPrioritySnapshot,
            item.QueueOrderTimeUtc,
            item.TicketNumber
        }).HasDatabaseName("IX_Tickets_AuthoritativeQueueOrder");
        builder.HasIndex(item => new { item.PatientId, item.Status, item.LastUpdatedOnUtc });
    }
}

internal sealed class TicketHistoryConfiguration : IWriteEntityConfiguration<TicketHistory>
{
    public void ConfigureAggregate(EntityTypeBuilder<TicketHistory> builder)
    {
        builder.ToTable("TicketHistories");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.OccurredOnUtc).HasColumnType("datetime2(3)").IsRequired();
        builder.Property(item => item.ReasonCode).HasMaxLength(TicketPolicy.ReasonCodeMaxLength);
        builder.Property(item => item.Reason).HasMaxLength(TicketPolicy.ReasonMaxLength);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ActorApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.TicketId, item.OccurredOnUtc });
        builder.HasIndex(item => new { item.TicketId, item.EventType, item.OccurredOnUtc });
    }
}

internal sealed class TicketCallAttemptConfiguration : IWriteEntityConfiguration<TicketCallAttempt>
{
    public void ConfigureAggregate(EntityTypeBuilder<TicketCallAttempt> builder)
    {
        builder.ToTable("TicketCallAttempts", table =>
        {
            table.HasCheckConstraint("CK_TicketCallAttempts_Cycle", "[CallCycle] > 0");
            table.HasCheckConstraint("CK_TicketCallAttempts_Number", "[AttemptNumber] > 0");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.CalledOnUtc).HasColumnType("datetime2(3)").IsRequired();
        builder.Property(item => item.OutcomeRecordedOnUtc).HasColumnType("datetime2(3)");
        builder.HasIndex(item => new { item.TicketId, item.CallCycle, item.AttemptNumber }).IsUnique()
            .HasDatabaseName("UX_TicketCallAttempts_TicketCycleNumber");
    }
}

internal sealed class TicketDailyCounterConfiguration : IWriteEntityConfiguration<TicketDailyCounter>
{
    public void ConfigureAggregate(EntityTypeBuilder<TicketDailyCounter> builder)
    {
        builder.ToTable("TicketDailyCounters", table =>
            table.HasCheckConstraint("CK_TicketDailyCounters_LastNumber", "[LastNumber] >= 0"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.BusinessDate).HasColumnType("date").IsRequired();
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(item => new { item.DoctorPracticeId, item.BusinessDate }).IsUnique()
            .HasDatabaseName("UX_TicketDailyCounters_PracticeDate");
        builder.HasOne<DoctorPractice>().WithMany().HasForeignKey(item => item.DoctorPracticeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class TicketIdempotencyRecordConfiguration
    : IWriteEntityConfiguration<TicketIdempotencyRecord>
{
    public void ConfigureAggregate(EntityTypeBuilder<TicketIdempotencyRecord> builder)
    {
        builder.ToTable("TicketIdempotencyRecords");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Operation).HasMaxLength(100).IsRequired();
        builder.Property(item => item.IdempotencyKey).HasMaxLength(200).IsRequired();
        builder.Property(item => item.RequestFingerprint).HasMaxLength(64).IsRequired();
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ActorApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Ticket>().WithMany().HasForeignKey(item => item.TicketId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.ActorApplicationUserId, item.Operation, item.IdempotencyKey })
            .IsUnique().HasDatabaseName("UX_TicketIdempotency_ActorOperationKey");
    }
}

internal sealed class PaymentConfiguration : IWriteEntityConfiguration<Payment>
{
    public void ConfigureAggregate(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments", table =>
        {
            table.HasCheckConstraint("CK_Payments_Status", "[Status] = 1");
            table.HasCheckConstraint("CK_Payments_Amount", "[Amount] > 0");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(item => item.CollectedOnUtc).HasColumnType("datetime2(3)").IsRequired();
        builder.HasOne<Doctor>().WithMany().HasForeignKey(item => item.DoctorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DoctorPractice>().WithMany().HasForeignKey(item => item.DoctorPracticeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Patient>().WithMany().HasForeignKey(item => item.PatientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Reservation>().WithMany().HasForeignKey(item => item.ReservationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Ticket>().WithMany().HasForeignKey(item => item.TicketId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.CollectedByApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => item.TicketId).IsUnique().HasDatabaseName("UX_Payments_TicketId");
        builder.HasIndex(item => item.ReservationId).IsUnique()
            .HasFilter("[ReservationId] IS NOT NULL")
            .HasDatabaseName("UX_Payments_ReservationId");
    }
}
