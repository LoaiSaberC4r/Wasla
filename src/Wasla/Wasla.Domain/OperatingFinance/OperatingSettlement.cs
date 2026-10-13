using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Results;

namespace Wasla.Domain.OperatingFinance;

public sealed class OperatingSettlement : Entity<Guid>
{
    private OperatingSettlement() { }
    public Guid DoctorId { get; private set; }
    public Guid ObligationId { get; private set; }
    public Guid ObligationRevisionId { get; private set; }
    public string ReceiptNumber { get; private set; } = string.Empty;
    public long AmountMinorUnits { get; private set; }
    public string CurrencyCode { get; private set; } = OperatingFinancialPolicy.CurrencyCode;
    public OperatingPaymentMethod PaymentMethod { get; private set; }
    public string? Reference { get; private set; }
    public DateOnly BusinessDate { get; private set; }
    public string ReportTimeZoneId { get; private set; } = string.Empty;
    public string? BackdateReason { get; private set; }
    public Guid ActorApplicationUserId { get; private set; }
    public DateTime RecordedAtUtc { get; private set; }
    public static Result<OperatingSettlement> Create(Guid id, OperatingObligation obligation, decimal amount,
        DateOnly paidDate, DateOnly today, string timeZoneId, OperatingPaymentMethod method,
        string? reference, string? backdateReason, Guid actorId, DateTime now)
    {
        var date = OperatingFinancialPolicy.ValidateMovementDate(paidDate, today, backdateReason);
        if (date.IsFailure) return Result<OperatingSettlement>.Fail(date.Errors);
        if (!Enum.IsDefined(method) || reference?.Length > 200 || actorId == Guid.Empty || !OperatingFinancialPolicy.ValidUtc(now) ||
            string.IsNullOrWhiteSpace(timeZoneId)) return Result<OperatingSettlement>.Fail(OperatingFinanceErrors.Invalid());
        var settled = obligation.Settle(id, amount);
        if (settled.IsFailure) return Result<OperatingSettlement>.Fail(settled.Errors);
        return Result<OperatingSettlement>.Ok(new OperatingSettlement { Id = id, DoctorId = obligation.DoctorId,
            ObligationId = obligation.Id, ObligationRevisionId = obligation.Revisions.OrderByDescending(r => r.RevisionNumber).First().Id,
            ReceiptNumber = $"OPS-{id:N}".ToUpperInvariant(), AmountMinorUnits = obligation.AmountMinorUnits,
            PaymentMethod = method, Reference = reference?.Trim(), BusinessDate = paidDate, ReportTimeZoneId = timeZoneId,
            BackdateReason = backdateReason?.Trim(), ActorApplicationUserId = actorId, RecordedAtUtc = now });
    }
}

public sealed class OperatingSettlementReversal : Entity<Guid>
{
    private OperatingSettlementReversal() { }
    public Guid DoctorId { get; private set; }
    public Guid OriginalSettlementId { get; private set; }
    public Guid ObligationId { get; private set; }
    public long AmountMinorUnits { get; private set; }
    public DateOnly BusinessDate { get; private set; }
    public string ReportTimeZoneId { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public Guid ActorApplicationUserId { get; private set; }
    public DateTime RecordedAtUtc { get; private set; }
    public static Result<OperatingSettlementReversal> Create(Guid id, OperatingSettlement original,
        OperatingObligation obligation, DateOnly date, DateOnly today, string timeZoneId, string reason, Guid actorId, DateTime now)
    {
        if (id == Guid.Empty || actorId == Guid.Empty || !OperatingFinancialPolicy.ValidUtc(now) ||
            !OperatingFinancialPolicy.HasReason(reason) || string.IsNullOrWhiteSpace(timeZoneId) ||
            original.ObligationId != obligation.Id || original.DoctorId != obligation.DoctorId || date < original.BusinessDate)
            return Result<OperatingSettlementReversal>.Fail(OperatingFinanceErrors.Invalid("OperatingFinance.InvalidReversal"));
        var validDate = OperatingFinancialPolicy.ValidateMovementDate(date, today, reason);
        if (validDate.IsFailure) return Result<OperatingSettlementReversal>.Fail(validDate.Errors);
        var state = obligation.Reverse(original.Id);
        if (state.IsFailure) return Result<OperatingSettlementReversal>.Fail(state.Errors);
        return Result<OperatingSettlementReversal>.Ok(new OperatingSettlementReversal { Id = id, DoctorId = original.DoctorId,
            OriginalSettlementId = original.Id, ObligationId = obligation.Id, AmountMinorUnits = original.AmountMinorUnits,
            BusinessDate = date, ReportTimeZoneId = timeZoneId, Reason = reason.Trim(), ActorApplicationUserId = actorId, RecordedAtUtc = now });
    }
}

public sealed class OperatingFinanceHistory : Entity<Guid>
{
    private OperatingFinanceHistory() { }
    public OperatingFinanceHistory(Guid id, Guid doctorId, string resourceType, Guid resourceId,
        string operation, string? reason, Guid actorId, DateTime now) : base(id)
    { DoctorId = doctorId; ResourceType = resourceType; ResourceId = resourceId; Operation = operation;
        Reason = reason?.Trim(); ActorApplicationUserId = actorId; RecordedAtUtc = now; }
    public Guid DoctorId { get; private set; }
    public string ResourceType { get; private set; } = string.Empty;
    public Guid ResourceId { get; private set; }
    public string Operation { get; private set; } = string.Empty;
    public string? Reason { get; private set; }
    public Guid ActorApplicationUserId { get; private set; }
    public DateTime RecordedAtUtc { get; private set; }
}
