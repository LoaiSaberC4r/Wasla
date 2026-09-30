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

internal sealed class PaymentConfiguration : IWriteEntityConfiguration<Payment>
{
    public void ConfigureAggregate(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments", table =>
        {
            table.HasCheckConstraint("CK_Payments_Status", "[Status] = 1");
            table.HasCheckConstraint("CK_Payments_Amount", "[Amount] > 0");
            table.HasCheckConstraint("CK_Payments_SequenceNumber", "[SequenceNumber] > 0");
            table.HasCheckConstraint("CK_Payments_CurrencyCode", "[CurrencyCode] = 'EGP'");
            table.HasCheckConstraint("CK_Payments_Method", "[PaymentMethod] IN (0,1,2,3)");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.TransactionNumber).HasMaxLength(FinancialPolicy.TransactionNumberMaxLength).IsRequired();
        builder.Property(item => item.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(item => item.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(item => item.ReferenceNumber).HasMaxLength(FinancialPolicy.ReferenceNumberMaxLength);
        builder.Property(item => item.Notes).HasMaxLength(FinancialPolicy.NotesMaxLength);
        builder.Property(item => item.BusinessDate).HasColumnType("date").IsRequired();
        builder.Property(item => item.CollectedOnUtc).HasColumnType("datetime2(3)").IsRequired();
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasOne<Doctor>().WithMany().HasForeignKey(item => item.DoctorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DoctorPractice>().WithMany().HasForeignKey(item => item.DoctorPracticeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Patient>().WithMany().HasForeignKey(item => item.PatientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Reservation>().WithMany().HasForeignKey(item => item.ReservationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Ticket>().WithMany().HasForeignKey(item => item.TicketId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.CollectedByApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Refund).WithOne().HasForeignKey<Refund>(item => item.PaymentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(item => item.CorrectionHistory).WithOne()
            .HasForeignKey(item => item.PaymentId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(item => item.CorrectionHistory).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(item => item.TicketId).IsUnique().HasDatabaseName("UX_Payments_TicketId");
        builder.HasIndex(item => item.ReservationId).IsUnique().HasFilter("[ReservationId] IS NOT NULL")
            .HasDatabaseName("UX_Payments_ReservationId");
        builder.HasIndex(item => item.TransactionNumber).IsUnique().HasDatabaseName("UX_Payments_TransactionNumber");
        builder.HasIndex(item => new { item.DoctorPracticeId, item.BusinessDate });
        builder.HasIndex(item => new { item.DoctorId, item.BusinessDate });
        builder.HasIndex(item => new { item.PatientId, item.BusinessDate });
        builder.HasIndex(item => new { item.PaymentMethod, item.BusinessDate });
    }
}

internal sealed class RefundConfiguration : IWriteEntityConfiguration<Refund>
{
    public void ConfigureAggregate(EntityTypeBuilder<Refund> builder)
    {
        builder.ToTable("Refunds", table =>
        {
            table.HasCheckConstraint("CK_Refunds_Amount", "[Amount] > 0");
            table.HasCheckConstraint("CK_Refunds_SequenceNumber", "[SequenceNumber] > 0");
            table.HasCheckConstraint("CK_Refunds_CurrencyCode", "[CurrencyCode] = 'EGP'");
            table.HasCheckConstraint("CK_Refunds_Method", "[RefundMethod] IN (1,2,3)");
            table.HasCheckConstraint("CK_Refunds_ReasonCode", "[RefundReasonCode] IN (1,2,3,4,5,6)");
            table.HasCheckConstraint("CK_Refunds_OtherReason", "[RefundReasonCode] <> 6 OR NULLIF(LTRIM(RTRIM([Reason])), '') IS NOT NULL");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.TransactionNumber).HasMaxLength(FinancialPolicy.TransactionNumberMaxLength).IsRequired();
        builder.Property(item => item.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(item => item.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(item => item.Reason).HasMaxLength(FinancialPolicy.ReasonMaxLength);
        builder.Property(item => item.ReferenceNumber).HasMaxLength(FinancialPolicy.ReferenceNumberMaxLength);
        builder.Property(item => item.Notes).HasMaxLength(FinancialPolicy.NotesMaxLength);
        builder.Property(item => item.BusinessDate).HasColumnType("date").IsRequired();
        builder.Property(item => item.RefundedOnUtc).HasColumnType("datetime2(3)").IsRequired();
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasOne<Ticket>().WithMany().HasForeignKey(item => item.TicketId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Patient>().WithMany().HasForeignKey(item => item.PatientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Doctor>().WithMany().HasForeignKey(item => item.DoctorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DoctorPractice>().WithMany().HasForeignKey(item => item.DoctorPracticeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.RefundedByApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(item => item.CorrectionHistory).WithOne()
            .HasForeignKey(item => item.RefundId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(item => item.CorrectionHistory).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(item => item.PaymentId).IsUnique().HasDatabaseName("UX_Refunds_PaymentId");
        builder.HasIndex(item => item.TransactionNumber).IsUnique().HasDatabaseName("UX_Refunds_TransactionNumber");
        builder.HasIndex(item => new { item.DoctorPracticeId, item.BusinessDate });
        builder.HasIndex(item => new { item.DoctorId, item.BusinessDate });
        builder.HasIndex(item => new { item.PatientId, item.BusinessDate });
        builder.HasIndex(item => new { item.RefundMethod, item.BusinessDate });
    }
}

internal sealed class PaymentCorrectionHistoryConfiguration : IWriteEntityConfiguration<PaymentCorrectionHistory>
{
    public void ConfigureAggregate(EntityTypeBuilder<PaymentCorrectionHistory> builder)
    {
        builder.ToTable("PaymentCorrectionHistories");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.OldReferenceNumber).HasMaxLength(FinancialPolicy.ReferenceNumberMaxLength);
        builder.Property(item => item.NewReferenceNumber).HasMaxLength(FinancialPolicy.ReferenceNumberMaxLength);
        builder.Property(item => item.OldNotes).HasMaxLength(FinancialPolicy.NotesMaxLength);
        builder.Property(item => item.NewNotes).HasMaxLength(FinancialPolicy.NotesMaxLength);
        builder.Property(item => item.CorrectionReason).HasMaxLength(FinancialPolicy.CorrectionReasonMaxLength).IsRequired();
        builder.Property(item => item.CorrectedOnUtc).HasColumnType("datetime2(3)").IsRequired();
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.CorrectedByApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.PaymentId, item.CorrectedOnUtc });
    }
}

internal sealed class RefundCorrectionHistoryConfiguration : IWriteEntityConfiguration<RefundCorrectionHistory>
{
    public void ConfigureAggregate(EntityTypeBuilder<RefundCorrectionHistory> builder)
    {
        builder.ToTable("RefundCorrectionHistories");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();
        builder.Property(item => item.OldReason).HasMaxLength(FinancialPolicy.ReasonMaxLength);
        builder.Property(item => item.NewReason).HasMaxLength(FinancialPolicy.ReasonMaxLength);
        builder.Property(item => item.OldReferenceNumber).HasMaxLength(FinancialPolicy.ReferenceNumberMaxLength);
        builder.Property(item => item.NewReferenceNumber).HasMaxLength(FinancialPolicy.ReferenceNumberMaxLength);
        builder.Property(item => item.OldNotes).HasMaxLength(FinancialPolicy.NotesMaxLength);
        builder.Property(item => item.NewNotes).HasMaxLength(FinancialPolicy.NotesMaxLength);
        builder.Property(item => item.CorrectionReason).HasMaxLength(FinancialPolicy.CorrectionReasonMaxLength).IsRequired();
        builder.Property(item => item.CorrectedOnUtc).HasColumnType("datetime2(3)").IsRequired();
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.CorrectedByApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.RefundId, item.CorrectedOnUtc });
    }
}

internal sealed class FinancialDailyCounterConfiguration : IWriteEntityConfiguration<FinancialDailyCounter>
{
    public void ConfigureAggregate(EntityTypeBuilder<FinancialDailyCounter> builder)
    {
        builder.ToTable("FinancialDailyCounters", table =>
        {
            table.HasCheckConstraint("CK_FinancialDailyCounters_LastNumber", "[LastNumber] >= 0");
            table.HasCheckConstraint("CK_FinancialDailyCounters_Type", "[TransactionType] IN (1,2)");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.BusinessDate).HasColumnType("date").IsRequired();
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasOne<DoctorPractice>().WithMany().HasForeignKey(item => item.DoctorPracticeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.DoctorPracticeId, item.BusinessDate, item.TransactionType })
            .IsUnique().HasDatabaseName("UX_FinancialDailyCounters_PracticeDateType");
    }
}

internal sealed class FinancialIdempotencyRecordConfiguration : IWriteEntityConfiguration<FinancialIdempotencyRecord>
{
    public void ConfigureAggregate(EntityTypeBuilder<FinancialIdempotencyRecord> builder)
    {
        builder.ToTable("FinancialIdempotencyRecords");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Operation).HasMaxLength(100).IsRequired();
        builder.Property(item => item.IdempotencyKey).HasMaxLength(200).IsRequired();
        builder.Property(item => item.RequestFingerprint).HasMaxLength(64).IsRequired();
        builder.Property(item => item.CreatedOnUtc).HasColumnType("datetime2(3)").IsRequired();
        builder.Property(item => item.CompletedOnUtc).HasColumnType("datetime2(3)");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ActorApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(item => new { item.ActorApplicationUserId, item.Operation, item.IdempotencyKey })
            .IsUnique().HasDatabaseName("UX_FinancialIdempotency_ActorOperationKey");
    }
}
