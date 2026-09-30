using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Results;
using Wasla.Domain.Resources;

namespace Wasla.Domain.Payments;

public enum RefundReasonCode
{
    PatientRequestedCancellation = 1,
    DoctorUnavailable = 2,
    DuplicatePayment = 3,
    WrongPaymentMethod = 4,
    OperationalError = 5,
    Other = 6
}

public sealed class Refund : AggregateRoot<Guid>
{
    private readonly List<RefundCorrectionHistory> _correctionHistory = [];

    private Refund() { }

    private Refund(
        Guid id, Payment payment, FinancialTransactionNumber number,
        DateOnly businessDate, PaymentMethod refundMethod, RefundReasonCode refundReasonCode,
        string? reason, string? referenceNumber, string? notes,
        Guid refundedByApplicationUserId, DateTime refundedOnUtc) : base(id)
    {
        TransactionNumber = number.TransactionNumber.Trim();
        SequenceNumber = number.SequenceNumber;
        PaymentId = payment.Id;
        TicketId = payment.TicketId;
        PatientId = payment.PatientId;
        DoctorId = payment.DoctorId;
        DoctorPracticeId = payment.DoctorPracticeId;
        Amount = payment.Amount;
        CurrencyCode = payment.CurrencyCode;
        RefundMethod = refundMethod;
        RefundReasonCode = refundReasonCode;
        Reason = FinancialPolicy.Normalize(reason);
        ReferenceNumber = FinancialPolicy.Normalize(referenceNumber);
        Notes = FinancialPolicy.Normalize(notes);
        BusinessDate = businessDate;
        RefundedByApplicationUserId = refundedByApplicationUserId;
        RefundedOnUtc = FinancialPolicy.EnsureUtc(refundedOnUtc);
    }

    public string TransactionNumber { get; private set; } = string.Empty;
    public int SequenceNumber { get; private set; }
    public Guid PaymentId { get; private set; }
    public Guid TicketId { get; private set; }
    public Guid PatientId { get; private set; }
    public Guid DoctorId { get; private set; }
    public Guid DoctorPracticeId { get; private set; }
    public decimal Amount { get; private set; }
    public string CurrencyCode { get; private set; } = FinancialPolicy.CurrencyCode;
    public PaymentMethod RefundMethod { get; private set; }
    public RefundReasonCode RefundReasonCode { get; private set; }
    public string? Reason { get; private set; }
    public string? ReferenceNumber { get; private set; }
    public string? Notes { get; private set; }
    public DateOnly BusinessDate { get; private set; }
    public Guid RefundedByApplicationUserId { get; private set; }
    public DateTime RefundedOnUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyCollection<RefundCorrectionHistory> CorrectionHistory => _correctionHistory.AsReadOnly();

    public static Result<Refund> Record(
        Guid id, Payment payment, FinancialTransactionNumber number,
        DateOnly businessDate, PaymentMethod refundMethod, RefundReasonCode refundReasonCode,
        string? reason, string? referenceNumber, string? notes,
        Guid refundedByApplicationUserId, DateTime refundedOnUtc)
    {
        ArgumentNullException.ThrowIfNull(payment);
        ArgumentNullException.ThrowIfNull(number);
        var normalizedReason = FinancialPolicy.Normalize(reason);
        if (refundReasonCode == RefundReasonCode.Other && normalizedReason is null)
        {
            return Result<Refund>.Fail(RefundErrors.ReasonRequired);
        }

        if (id == Guid.Empty || payment.Refund is not null ||
            payment.Amount <= 0 || payment.CurrencyCode != FinancialPolicy.CurrencyCode ||
            payment.Status != PaymentStatus.Paid || businessDate == default ||
            number.SequenceNumber <= 0 ||
            string.IsNullOrWhiteSpace(number.TransactionNumber) ||
            number.TransactionNumber.Length > FinancialPolicy.TransactionNumberMaxLength ||
            !number.TransactionNumber.StartsWith("REF-", StringComparison.Ordinal) ||
            !FinancialPolicy.IsSelectableMethod(refundMethod) ||
            !Enum.IsDefined(refundReasonCode) ||
            normalizedReason?.Length > FinancialPolicy.ReasonMaxLength ||
            FinancialPolicy.Normalize(referenceNumber)?.Length > FinancialPolicy.ReferenceNumberMaxLength ||
            FinancialPolicy.Normalize(notes)?.Length > FinancialPolicy.NotesMaxLength ||
            refundedByApplicationUserId == Guid.Empty)
        {
            return Result<Refund>.Fail(payment.Refund is not null
                ? RefundErrors.AlreadyExists : RefundErrors.Invalid);
        }

        var refund = new Refund(id, payment, number, businessDate, refundMethod,
            refundReasonCode, normalizedReason, referenceNumber, notes,
            refundedByApplicationUserId, refundedOnUtc);
        if (!payment.TryAttachRefund(refund))
        {
            return Result<Refund>.Fail(RefundErrors.AlreadyExists);
        }

        return Result<Refund>.Ok(refund);
    }

    public Result<RefundCorrectionHistory> CorrectMetadata(
        PaymentMethod refundMethod, RefundReasonCode refundReasonCode,
        string? reason, string? referenceNumber, string? notes,
        string correctionReason, Guid correctedByApplicationUserId, DateTime correctedOnUtc)
    {
        var normalizedReason = FinancialPolicy.Normalize(reason);
        var reference = FinancialPolicy.Normalize(referenceNumber);
        var normalizedNotes = FinancialPolicy.Normalize(notes);
        var correction = FinancialPolicy.Normalize(correctionReason);
        if (refundReasonCode == RefundReasonCode.Other && normalizedReason is null)
        {
            return Result<RefundCorrectionHistory>.Fail(RefundErrors.ReasonRequired);
        }

        if (!FinancialPolicy.IsSelectableMethod(refundMethod) ||
            !Enum.IsDefined(refundReasonCode) ||
            normalizedReason?.Length > FinancialPolicy.ReasonMaxLength ||
            reference?.Length > FinancialPolicy.ReferenceNumberMaxLength ||
            normalizedNotes?.Length > FinancialPolicy.NotesMaxLength ||
            correction is null || correction.Length > FinancialPolicy.CorrectionReasonMaxLength ||
            correctedByApplicationUserId == Guid.Empty)
        {
            return Result<RefundCorrectionHistory>.Fail(RefundErrors.InvalidCorrection);
        }

        if (RefundMethod == refundMethod && RefundReasonCode == refundReasonCode &&
            Reason == normalizedReason && ReferenceNumber == reference && Notes == normalizedNotes)
        {
            return Result<RefundCorrectionHistory>.Fail(RefundErrors.CorrectionNoChanges);
        }

        var history = RefundCorrectionHistory.Create(
            Guid.NewGuid(), Id, RefundMethod, refundMethod, RefundReasonCode, refundReasonCode,
            Reason, normalizedReason, ReferenceNumber, reference, Notes, normalizedNotes,
            correction, correctedByApplicationUserId, FinancialPolicy.EnsureUtc(correctedOnUtc));
        RefundMethod = refundMethod;
        RefundReasonCode = refundReasonCode;
        Reason = normalizedReason;
        ReferenceNumber = reference;
        Notes = normalizedNotes;
        _correctionHistory.Add(history);
        return Result<RefundCorrectionHistory>.Ok(history);
    }
}

public sealed class RefundCorrectionHistory : Entity<Guid>
{
    private RefundCorrectionHistory() { }

    private RefundCorrectionHistory(
        Guid id, Guid refundId, PaymentMethod oldMethod, PaymentMethod newMethod,
        RefundReasonCode oldReasonCode, RefundReasonCode newReasonCode,
        string? oldReason, string? newReason, string? oldReference, string? newReference,
        string? oldNotes, string? newNotes, string correctionReason,
        Guid actorId, DateTime whenUtc) : base(id)
    {
        RefundId = refundId;
        OldRefundMethod = oldMethod;
        NewRefundMethod = newMethod;
        OldRefundReasonCode = oldReasonCode;
        NewRefundReasonCode = newReasonCode;
        OldReason = oldReason;
        NewReason = newReason;
        OldReferenceNumber = oldReference;
        NewReferenceNumber = newReference;
        OldNotes = oldNotes;
        NewNotes = newNotes;
        CorrectionReason = correctionReason;
        CorrectedByApplicationUserId = actorId;
        CorrectedOnUtc = whenUtc;
    }

    public Guid RefundId { get; private set; }
    public PaymentMethod OldRefundMethod { get; private set; }
    public PaymentMethod NewRefundMethod { get; private set; }
    public RefundReasonCode OldRefundReasonCode { get; private set; }
    public RefundReasonCode NewRefundReasonCode { get; private set; }
    public string? OldReason { get; private set; }
    public string? NewReason { get; private set; }
    public string? OldReferenceNumber { get; private set; }
    public string? NewReferenceNumber { get; private set; }
    public string? OldNotes { get; private set; }
    public string? NewNotes { get; private set; }
    public string CorrectionReason { get; private set; } = string.Empty;
    public Guid CorrectedByApplicationUserId { get; private set; }
    public DateTime CorrectedOnUtc { get; private set; }

    internal static RefundCorrectionHistory Create(
        Guid id, Guid refundId, PaymentMethod oldMethod, PaymentMethod newMethod,
        RefundReasonCode oldReasonCode, RefundReasonCode newReasonCode,
        string? oldReason, string? newReason, string? oldReference, string? newReference,
        string? oldNotes, string? newNotes, string correctionReason,
        Guid actorId, DateTime whenUtc)
        => new(id, refundId, oldMethod, newMethod, oldReasonCode, newReasonCode,
            oldReason, newReason, oldReference, newReference, oldNotes, newNotes,
            correctionReason, actorId, whenUtc);
}

public static class RefundErrors
{
    public static Error Invalid => Error.Validation(
        "Refund.Invalid", ErrorMessage.GetString("RefundInvalid"));
    public static Error AlreadyExists => Error.Conflict(
        "Refund.AlreadyExists", ErrorMessage.GetString("RefundAlreadyExists"));
    public static Error ReasonRequired => Error.Validation(
        "Refund.ReasonRequired", ErrorMessage.GetString("RefundReasonRequired"));
    public static Error InvalidCorrection => Error.Validation(
        "Refund.InvalidCorrection", ErrorMessage.GetString("RefundInvalidCorrection"));
    public static Error CorrectionNoChanges => Error.Validation(
        "Refund.CorrectionNoChanges", ErrorMessage.GetString("RefundCorrectionNoChanges"));
}
