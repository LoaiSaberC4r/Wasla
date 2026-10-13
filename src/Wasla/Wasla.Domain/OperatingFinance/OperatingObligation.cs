using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Results;

namespace Wasla.Domain.OperatingFinance;

public sealed class OperatingObligation : AggregateRoot<Guid>
{
    private readonly List<OperatingObligationRevision> _revisions = [];
    private OperatingObligation() { }
    public Guid DoctorId { get; private set; }
    public Guid? PlanId { get; private set; }
    public Guid? ReceptionId { get; private set; }
    public OperatingObligationKind Kind { get; private set; }
    public string? OccurrenceKey { get; private set; }
    public int? PeriodYear { get; private set; }
    public int? PeriodMonth { get; private set; }
    public DateOnly BusinessDate { get; private set; }
    public DateOnly DueBusinessDate { get; private set; }
    public long PlannedAmountMinorUnits { get; private set; }
    public long AmountMinorUnits { get; private set; }
    public string CurrencyCode { get; private set; } = OperatingFinancialPolicy.CurrencyCode;
    public ExpenseAmountPolicy AmountPolicy { get; private set; }
    public bool ActualAmountConfirmed { get; private set; }
    public ExpenseCategoryCode Category { get; private set; }
    public string DescriptionAr { get; private set; } = string.Empty;
    public string? DescriptionEn { get; private set; }
    public OperatingObligationStatus Status { get; private set; }
    public Guid? ActiveSettlementId { get; private set; }
    public int CurrentRevisionNumber { get; private set; }
    public Guid CreatedByApplicationUserId { get; private set; }
    public DateTime RecordedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyCollection<OperatingObligationRevision> Revisions => _revisions.AsReadOnly();

    public static Result<OperatingObligation> Create(Guid id, Guid doctorId, OperatingObligationKind kind,
        Guid? planId, Guid? receptionId, string? occurrenceKey, DateOnly businessDate, DateOnly dueDate,
        decimal amount, ExpenseAmountPolicy policy, ExpenseCategoryCode category, string descriptionAr,
        string? descriptionEn, IReadOnlyCollection<PracticeAllocationPercentage> allocations, Guid actorId, DateTime now)
    {
        var allocation = OperatingFinancialPolicy.Allocate(amount, allocations);
        if (allocation.IsFailure) return Result<OperatingObligation>.Fail(allocation.Errors);
        if (id == Guid.Empty || doctorId == Guid.Empty || actorId == Guid.Empty || !OperatingFinancialPolicy.ValidUtc(now) ||
            !Enum.IsDefined(kind) || !Enum.IsDefined(policy) || !Enum.IsDefined(category) ||
            businessDate == DateOnly.MinValue || dueDate == DateOnly.MinValue ||
            string.IsNullOrWhiteSpace(descriptionAr) || descriptionAr.Trim().Length > 1000 || descriptionEn?.Length > 1000 ||
            kind == OperatingObligationKind.Salary && (receptionId == null || policy != ExpenseAmountPolicy.Fixed) ||
            kind != OperatingObligationKind.OneTimeExpense && (planId == null || string.IsNullOrWhiteSpace(occurrenceKey)) ||
            kind == OperatingObligationKind.OneTimeExpense && (planId != null || occurrenceKey != null || receptionId != null))
            return Result<OperatingObligation>.Fail(OperatingFinanceErrors.Invalid());
        var result = new OperatingObligation { Id = id, DoctorId = doctorId, PlanId = planId, ReceptionId = receptionId,
            Kind = kind, OccurrenceKey = occurrenceKey, BusinessDate = businessDate, DueBusinessDate = dueDate,
            PlannedAmountMinorUnits = (long)(amount * 100), AmountMinorUnits = (long)(amount * 100),
            AmountPolicy = policy, ActualAmountConfirmed = policy == ExpenseAmountPolicy.Fixed, Category = category,
            DescriptionAr = descriptionAr.Trim(), DescriptionEn = descriptionEn?.Trim(), Status = OperatingObligationStatus.Unpaid,
            CurrentRevisionNumber = 1, CreatedByApplicationUserId = actorId, RecordedAtUtc = now,
            PeriodYear = planId != null ? businessDate.Year : null, PeriodMonth = planId != null ? businessDate.Month : null };
        result._revisions.Add(OperatingObligationRevision.Create(result, allocation.Value, "Created", actorId, now));
        return Result<OperatingObligation>.Ok(result);
    }

    public Result Correct(decimal amount, DateOnly dueDate, ExpenseCategoryCode category, string descriptionAr,
        string? descriptionEn, IReadOnlyCollection<PracticeAllocationPercentage> allocations, string reason, Guid actorId, DateTime now)
    {
        if (Status != OperatingObligationStatus.Unpaid) return Result.Fail(OperatingFinanceErrors.State("OperatingFinance.PaidRecordImmutable"));
        if (!OperatingFinancialPolicy.HasReason(reason) || actorId == Guid.Empty || !OperatingFinancialPolicy.ValidUtc(now) ||
            dueDate == DateOnly.MinValue || !Enum.IsDefined(category) || string.IsNullOrWhiteSpace(descriptionAr) ||
            descriptionAr.Length > 1000 || descriptionEn?.Length > 1000)
            return Result.Fail(OperatingFinanceErrors.Invalid());
        var allocation = OperatingFinancialPolicy.Allocate(amount, allocations);
        if (allocation.IsFailure) return Result.Fail(allocation.Errors);
        AmountMinorUnits = (long)(amount * 100); DueBusinessDate = dueDate; Category = category;
        DescriptionAr = descriptionAr.Trim(); DescriptionEn = descriptionEn?.Trim();
        if (AmountPolicy == ExpenseAmountPolicy.Variable) ActualAmountConfirmed = false;
        ++CurrentRevisionNumber;
        _revisions.Add(OperatingObligationRevision.Create(this, allocation.Value, reason, actorId, now));
        return Result.Ok();
    }

    public Result ConfirmActual(decimal amount, string? reason, Guid actorId, DateTime now)
    {
        if (Status != OperatingObligationStatus.Unpaid || AmountPolicy != ExpenseAmountPolicy.Variable)
            return Result.Fail(OperatingFinanceErrors.State());
        var minor = OperatingFinancialPolicy.MinorUnits(amount);
        if (minor.IsFailure) return Result.Fail(minor.Errors);
        if (actorId == Guid.Empty || !OperatingFinancialPolicy.ValidUtc(now) ||
            (minor.Value != PlannedAmountMinorUnits || ActualAmountConfirmed) && !OperatingFinancialPolicy.HasReason(reason))
            return Result.Fail(OperatingFinanceErrors.Invalid("OperatingFinance.ReasonRequired"));
        var last = _revisions.OrderByDescending(r => r.RevisionNumber).First();
        var allocation = OperatingFinancialPolicy.Allocate(amount, last.Allocations
            .Select(a => new PracticeAllocationPercentage(a.PracticeId, a.Percentage)).ToArray());
        if (allocation.IsFailure) return Result.Fail(allocation.Errors);
        AmountMinorUnits = (long)(amount * 100); ActualAmountConfirmed = true; ++CurrentRevisionNumber;
        _revisions.Add(OperatingObligationRevision.Create(this, allocation.Value, reason ?? "ActualAmountConfirmed", actorId, now));
        return Result.Ok();
    }

    public Result Settle(Guid settlementId, decimal amount)
    {
        if (Status != OperatingObligationStatus.Unpaid || ActiveSettlementId != null)
            return Result.Fail(OperatingFinanceErrors.State("OperatingFinance.AlreadySettled"));
        if (!ActualAmountConfirmed) return Result.Fail(OperatingFinanceErrors.State("OperatingFinance.ActualConfirmationRequired"));
        var minor = OperatingFinancialPolicy.MinorUnits(amount);
        if (settlementId == Guid.Empty || minor.IsFailure || minor.Value != AmountMinorUnits)
            return Result.Fail(OperatingFinanceErrors.Invalid("OperatingFinance.FullSettlementRequired"));
        ActiveSettlementId = settlementId; Status = OperatingObligationStatus.Paid;
        return Result.Ok();
    }
    public Result Reverse(Guid originalSettlementId)
    {
        if (Status != OperatingObligationStatus.Paid || ActiveSettlementId != originalSettlementId)
            return Result.Fail(OperatingFinanceErrors.State("OperatingFinance.AlreadyReversed"));
        ActiveSettlementId = null; Status = OperatingObligationStatus.Unpaid;
        return Result.Ok();
    }
    public Result Cancel(string reason, Guid actorId, DateTime now)
    {
        if (Status != OperatingObligationStatus.Unpaid) return Result.Fail(OperatingFinanceErrors.State());
        if (!OperatingFinancialPolicy.HasReason(reason) || actorId == Guid.Empty || !OperatingFinancialPolicy.ValidUtc(now))
            return Result.Fail(OperatingFinanceErrors.Invalid("OperatingFinance.ReasonRequired"));
        Status = OperatingObligationStatus.Cancelled; ++CurrentRevisionNumber;
        var shares = _revisions.OrderByDescending(r => r.RevisionNumber).First().Allocations
            .Select(a => new AllocatedOperatingCost(a.PracticeId, a.Percentage, a.AmountMinorUnits)).ToArray();
        _revisions.Add(OperatingObligationRevision.Create(this, shares, reason, actorId, now));
        return Result.Ok();
    }
}

public sealed class OperatingObligationRevision : Entity<Guid>
{
    private readonly List<OperatingCostAllocationSnapshot> _allocations = [];
    private OperatingObligationRevision() { }
    public Guid ObligationId { get; private set; }
    public Guid DoctorId { get; private set; }
    public int RevisionNumber { get; private set; }
    public long AmountMinorUnits { get; private set; }
    public DateOnly DueBusinessDate { get; private set; }
    public ExpenseCategoryCode Category { get; private set; }
    public string DescriptionAr { get; private set; } = string.Empty;
    public string? DescriptionEn { get; private set; }
    public bool ActualAmountConfirmed { get; private set; }
    public OperatingObligationStatus Status { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public Guid ActorApplicationUserId { get; private set; }
    public DateTime RecordedAtUtc { get; private set; }
    public IReadOnlyCollection<OperatingCostAllocationSnapshot> Allocations => _allocations.AsReadOnly();
    internal static OperatingObligationRevision Create(OperatingObligation obligation,
        IReadOnlyList<AllocatedOperatingCost> allocation, string reason, Guid actorId, DateTime now)
    {
        var result = new OperatingObligationRevision { Id = Guid.NewGuid(), ObligationId = obligation.Id,
            DoctorId = obligation.DoctorId, RevisionNumber = obligation.CurrentRevisionNumber,
            AmountMinorUnits = obligation.AmountMinorUnits, DueBusinessDate = obligation.DueBusinessDate,
            Category = obligation.Category, DescriptionAr = obligation.DescriptionAr, DescriptionEn = obligation.DescriptionEn,
            ActualAmountConfirmed = obligation.ActualAmountConfirmed, Status = obligation.Status,
            Reason = reason.Trim(), ActorApplicationUserId = actorId, RecordedAtUtc = now };
        result._allocations.AddRange(allocation.Select(a => new OperatingCostAllocationSnapshot(Guid.NewGuid(), result.Id,
            obligation.DoctorId, a.PracticeId, a.Percentage, a.AmountMinorUnits)));
        return result;
    }
}
public sealed class OperatingCostAllocationSnapshot : Entity<Guid>
{
    private OperatingCostAllocationSnapshot() { }
    internal OperatingCostAllocationSnapshot(Guid id, Guid revisionId, Guid doctorId, Guid practiceId,
        decimal percentage, long minor) : base(id)
    { RevisionId = revisionId; DoctorId = doctorId; PracticeId = practiceId; Percentage = percentage; AmountMinorUnits = minor; }
    public Guid RevisionId { get; private set; }
    public Guid DoctorId { get; private set; }
    public Guid PracticeId { get; private set; }
    public decimal Percentage { get; private set; }
    public long AmountMinorUnits { get; private set; }
}
