using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Results;
using Wasla.Domain.Resources;

namespace Wasla.Domain.Payments;

public enum PaymentStatus { Paid = 1 }

public enum PaymentMethod
{
    // Phase 11 did not capture a method. Never accept this for a new transaction.
    LegacyUnspecified = 0,
    Cash = 1,
    Card = 2,
    Wallet = 3
}

public enum FinancialTransactionType { Payment = 1, Refund = 2 }

public static class FinancialPolicy
{
    public const string CurrencyCode = "EGP";
    public const int TransactionNumberMaxLength = 80;
    public const int ReferenceNumberMaxLength = 200;
    public const int NotesMaxLength = 1000;
    public const int ReasonMaxLength = 1000;
    public const int CorrectionReasonMaxLength = 1000;

    public static bool IsSelectableMethod(PaymentMethod method)
        => method is PaymentMethod.Cash or PaymentMethod.Card or PaymentMethod.Wallet;

    public static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static DateTime EnsureUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}

public sealed record FinancialTransactionNumber(int SequenceNumber, string TransactionNumber);

public sealed record PaymentRecordSnapshot(
    Guid Id,
    Guid DoctorId,
    Guid DoctorPracticeId,
    Guid PatientId,
    Guid? ReservationId,
    Guid TicketId,
    decimal Amount,
    decimal TicketPriceSnapshot,
    string TransactionNumber,
    int SequenceNumber,
    DateOnly BusinessDate,
    PaymentMethod PaymentMethod,
    string? ReferenceNumber,
    string? Notes,
    Guid CollectedByApplicationUserId,
    DateTime CollectedOnUtc);

public sealed class Payment : AggregateRoot<Guid>
{
    private readonly List<PaymentCorrectionHistory> _correctionHistory = [];

    private Payment() { }

    private Payment(PaymentRecordSnapshot snapshot) : base(snapshot.Id)
    {
        TransactionNumber = snapshot.TransactionNumber.Trim();
        SequenceNumber = snapshot.SequenceNumber;
        DoctorId = snapshot.DoctorId;
        DoctorPracticeId = snapshot.DoctorPracticeId;
        PatientId = snapshot.PatientId;
        ReservationId = snapshot.ReservationId;
        TicketId = snapshot.TicketId;
        Amount = snapshot.Amount;
        CurrencyCode = FinancialPolicy.CurrencyCode;
        PaymentMethod = snapshot.PaymentMethod;
        ReferenceNumber = FinancialPolicy.Normalize(snapshot.ReferenceNumber);
        Notes = FinancialPolicy.Normalize(snapshot.Notes);
        Status = PaymentStatus.Paid;
        BusinessDate = snapshot.BusinessDate;
        CollectedByApplicationUserId = snapshot.CollectedByApplicationUserId;
        CollectedOnUtc = FinancialPolicy.EnsureUtc(snapshot.CollectedOnUtc);
    }

    public string TransactionNumber { get; private set; } = string.Empty;
    public int SequenceNumber { get; private set; }
    public Guid DoctorId { get; private set; }
    public Guid DoctorPracticeId { get; private set; }
    public Guid PatientId { get; private set; }
    public Guid? ReservationId { get; private set; }
    public Guid TicketId { get; private set; }
    public decimal Amount { get; private set; }
    public string CurrencyCode { get; private set; } = FinancialPolicy.CurrencyCode;
    public PaymentMethod PaymentMethod { get; private set; }
    public string? ReferenceNumber { get; private set; }
    public string? Notes { get; private set; }
    public PaymentStatus Status { get; private set; }
    public DateOnly BusinessDate { get; private set; }
    public Guid CollectedByApplicationUserId { get; private set; }
    public DateTime CollectedOnUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public Refund? Refund { get; private set; }
    public IReadOnlyCollection<PaymentCorrectionHistory> CorrectionHistory => _correctionHistory.AsReadOnly();

    public static Result<Payment> RecordPaid(PaymentRecordSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!FinancialPolicy.IsSelectableMethod(snapshot.PaymentMethod))
        {
            return Result<Payment>.Fail(PaymentErrors.UnsupportedMethod);
        }

        if (snapshot.Id == Guid.Empty || snapshot.DoctorId == Guid.Empty ||
            snapshot.DoctorPracticeId == Guid.Empty || snapshot.PatientId == Guid.Empty ||
            snapshot.TicketId == Guid.Empty || snapshot.Amount <= 0 ||
            snapshot.Amount != snapshot.TicketPriceSnapshot ||
            snapshot.CollectedByApplicationUserId == Guid.Empty ||
            snapshot.BusinessDate == default || snapshot.SequenceNumber <= 0 ||
            string.IsNullOrWhiteSpace(snapshot.TransactionNumber) ||
            snapshot.TransactionNumber.Length > FinancialPolicy.TransactionNumberMaxLength ||
            !snapshot.TransactionNumber.StartsWith("PAY-", StringComparison.Ordinal) ||
            FinancialPolicy.Normalize(snapshot.ReferenceNumber)?.Length > FinancialPolicy.ReferenceNumberMaxLength ||
            FinancialPolicy.Normalize(snapshot.Notes)?.Length > FinancialPolicy.NotesMaxLength)
        {
            return Result<Payment>.Fail(PaymentErrors.Invalid);
        }

        return Result<Payment>.Ok(new Payment(snapshot));
    }

    // Compatibility for Phase 11 domain callers. Production admission uses the snapshot overload.
    public static Result<Payment> RecordPaid(
        Guid id, Guid doctorId, Guid doctorPracticeId, Guid patientId,
        Guid? reservationId, Guid ticketId, decimal amount, decimal ticketPriceSnapshot,
        Guid collectedByApplicationUserId, DateTime collectedOnUtc)
    {
        if (id == Guid.Empty || doctorId == Guid.Empty || doctorPracticeId == Guid.Empty ||
            patientId == Guid.Empty || ticketId == Guid.Empty || amount <= 0 ||
            amount != ticketPriceSnapshot || collectedByApplicationUserId == Guid.Empty)
        {
            return Result<Payment>.Fail(PaymentErrors.Invalid);
        }

        var legacy = new Payment(new PaymentRecordSnapshot(
            id, doctorId, doctorPracticeId, patientId, reservationId, ticketId,
            amount, ticketPriceSnapshot, $"PAY-LEGACY-{id:N}", 1,
            DateOnly.FromDateTime(FinancialPolicy.EnsureUtc(collectedOnUtc)),
            PaymentMethod.LegacyUnspecified, null, null,
            collectedByApplicationUserId, collectedOnUtc));
        return Result<Payment>.Ok(legacy);
    }

    public Result<PaymentCorrectionHistory> CorrectMetadata(
        PaymentMethod method, string? referenceNumber, string? notes,
        string correctionReason, Guid correctedByApplicationUserId, DateTime correctedOnUtc)
    {
        if (Refund is not null)
        {
            return Result<PaymentCorrectionHistory>.Fail(PaymentErrors.AlreadyRefunded);
        }

        var reference = FinancialPolicy.Normalize(referenceNumber);
        var normalizedNotes = FinancialPolicy.Normalize(notes);
        var reason = FinancialPolicy.Normalize(correctionReason);
        if (!FinancialPolicy.IsSelectableMethod(method) ||
            reference?.Length > FinancialPolicy.ReferenceNumberMaxLength ||
            normalizedNotes?.Length > FinancialPolicy.NotesMaxLength ||
            reason is null || reason.Length > FinancialPolicy.CorrectionReasonMaxLength ||
            correctedByApplicationUserId == Guid.Empty)
        {
            return Result<PaymentCorrectionHistory>.Fail(PaymentErrors.InvalidCorrection);
        }

        if (PaymentMethod == method && ReferenceNumber == reference && Notes == normalizedNotes)
        {
            return Result<PaymentCorrectionHistory>.Fail(PaymentErrors.CorrectionNoChanges);
        }

        var history = PaymentCorrectionHistory.Create(
            Guid.NewGuid(), Id, PaymentMethod, method, ReferenceNumber, reference,
            Notes, normalizedNotes, reason, correctedByApplicationUserId,
            FinancialPolicy.EnsureUtc(correctedOnUtc));
        PaymentMethod = method;
        ReferenceNumber = reference;
        Notes = normalizedNotes;
        _correctionHistory.Add(history);
        return Result<PaymentCorrectionHistory>.Ok(history);
    }

    internal bool TryAttachRefund(Refund refund)
    {
        if (Refund is not null || refund.PaymentId != Id)
        {
            return false;
        }

        Refund = refund;
        return true;
    }
}

public sealed class PaymentCorrectionHistory : Entity<Guid>
{
    private PaymentCorrectionHistory() { }

    private PaymentCorrectionHistory(
        Guid id, Guid paymentId, PaymentMethod oldMethod, PaymentMethod newMethod,
        string? oldReference, string? newReference, string? oldNotes, string? newNotes,
        string correctionReason, Guid actorId, DateTime whenUtc) : base(id)
    {
        PaymentId = paymentId;
        OldPaymentMethod = oldMethod;
        NewPaymentMethod = newMethod;
        OldReferenceNumber = oldReference;
        NewReferenceNumber = newReference;
        OldNotes = oldNotes;
        NewNotes = newNotes;
        CorrectionReason = correctionReason;
        CorrectedByApplicationUserId = actorId;
        CorrectedOnUtc = whenUtc;
    }

    public Guid PaymentId { get; private set; }
    public PaymentMethod OldPaymentMethod { get; private set; }
    public PaymentMethod NewPaymentMethod { get; private set; }
    public string? OldReferenceNumber { get; private set; }
    public string? NewReferenceNumber { get; private set; }
    public string? OldNotes { get; private set; }
    public string? NewNotes { get; private set; }
    public string CorrectionReason { get; private set; } = string.Empty;
    public Guid CorrectedByApplicationUserId { get; private set; }
    public DateTime CorrectedOnUtc { get; private set; }

    internal static PaymentCorrectionHistory Create(
        Guid id, Guid paymentId, PaymentMethod oldMethod, PaymentMethod newMethod,
        string? oldReference, string? newReference, string? oldNotes, string? newNotes,
        string correctionReason, Guid actorId, DateTime whenUtc)
        => new(id, paymentId, oldMethod, newMethod, oldReference, newReference,
            oldNotes, newNotes, correctionReason, actorId, whenUtc);
}

public static class PaymentErrors
{
    public static Error Invalid => Error.Validation(
        "Payment.Invalid", ErrorMessage.GetString("PaymentInvalid"));
    public static Error UnsupportedMethod => Error.Validation(
        "Payment.UnsupportedMethod", ErrorMessage.GetString("PaymentUnsupportedMethod"));
    public static Error AlreadyRefunded => Error.Conflict(
        "Payment.AlreadyRefunded", ErrorMessage.GetString("PaymentAlreadyRefunded"));
    public static Error InvalidCorrection => Error.Validation(
        "Payment.InvalidCorrection", ErrorMessage.GetString("PaymentInvalidCorrection"));
    public static Error CorrectionNoChanges => Error.Validation(
        "Payment.CorrectionNoChanges", ErrorMessage.GetString("PaymentCorrectionNoChanges"));
}
