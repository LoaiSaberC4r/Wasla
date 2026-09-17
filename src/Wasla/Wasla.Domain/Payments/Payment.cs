using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Results;

namespace Wasla.Domain.Payments;

public enum PaymentStatus
{
    Paid = 1
}

public sealed class Payment : AggregateRoot<Guid>
{
    private Payment()
    {
    }

    private Payment(
        Guid id,
        Guid doctorId,
        Guid doctorPracticeId,
        Guid patientId,
        Guid? reservationId,
        Guid ticketId,
        decimal amount,
        Guid collectedByApplicationUserId,
        DateTime collectedOnUtc) : base(id)
    {
        DoctorId = doctorId;
        DoctorPracticeId = doctorPracticeId;
        PatientId = patientId;
        ReservationId = reservationId;
        TicketId = ticketId;
        Amount = amount;
        Status = PaymentStatus.Paid;
        CollectedByApplicationUserId = collectedByApplicationUserId;
        CollectedOnUtc = collectedOnUtc.Kind == DateTimeKind.Utc
            ? collectedOnUtc
            : collectedOnUtc.ToUniversalTime();
    }

    public Guid DoctorId { get; private set; }
    public Guid DoctorPracticeId { get; private set; }
    public Guid PatientId { get; private set; }
    public Guid? ReservationId { get; private set; }
    public Guid TicketId { get; private set; }
    public decimal Amount { get; private set; }
    public PaymentStatus Status { get; private set; }
    public Guid CollectedByApplicationUserId { get; private set; }
    public DateTime CollectedOnUtc { get; private set; }

    public static Result<Payment> RecordPaid(
        Guid id,
        Guid doctorId,
        Guid doctorPracticeId,
        Guid patientId,
        Guid? reservationId,
        Guid ticketId,
        decimal amount,
        decimal ticketPriceSnapshot,
        Guid collectedByApplicationUserId,
        DateTime collectedOnUtc)
        => id == Guid.Empty || doctorId == Guid.Empty || doctorPracticeId == Guid.Empty ||
           patientId == Guid.Empty || ticketId == Guid.Empty || amount <= 0 ||
           amount != ticketPriceSnapshot || collectedByApplicationUserId == Guid.Empty
            ? Result<Payment>.Fail(PaymentErrors.Invalid)
            : Result<Payment>.Ok(new Payment(
                id, doctorId, doctorPracticeId, patientId, reservationId,
                ticketId, amount, collectedByApplicationUserId, collectedOnUtc));
}

public static class PaymentErrors
{
    public static Error Invalid => Error.Validation(
        "Payment.Invalid", "The paid amount must equal the ticket price.");
}
