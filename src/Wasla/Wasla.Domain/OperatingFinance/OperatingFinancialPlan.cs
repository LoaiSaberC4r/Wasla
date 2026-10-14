using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Results;

namespace Wasla.Domain.OperatingFinance;

public sealed record OperatingPlanTerms(decimal Amount, int DueDay, DateOnly EffectiveFrom,
    DateOnly? EffectiveTo, ExpenseCadence Cadence, ExpenseAmountPolicy AmountPolicy,
    ExpenseCategoryCode Category, string DescriptionAr, string? DescriptionEn,
    IReadOnlyCollection<PracticeAllocationPercentage> Allocations);

public sealed class OperatingFinancialPlan : AggregateRoot<Guid>
{
    private readonly List<OperatingPlanRevision> _revisions = [];
    private OperatingFinancialPlan() { }
    private OperatingFinancialPlan(Guid id, Guid doctorId, Guid? receptionId, OperatingPlanKind kind,
        OperatingPlanTerms terms, Guid actorId, DateTime now) : base(id)
    {
        DoctorId = doctorId; ReceptionId = receptionId; Kind = kind; Status = OperatingPlanStatus.Active;
        EffectiveFrom = terms.EffectiveFrom; EffectiveTo = terms.EffectiveTo;
        FirstCoveredMonth = kind == OperatingPlanKind.Salary ? OperatingFinancialPolicy.FirstCoveredMonth(terms.EffectiveFrom)
            : new DateOnly(terms.EffectiveFrom.Year, terms.EffectiveFrom.Month, 1);
        CreatedByApplicationUserId = actorId; CreatedOnUtc = now; CurrentRevisionNumber = 1;
        _revisions.Add(OperatingPlanRevision.Create(this, terms, 1, Status, actorId, now, "Created"));
    }
    public Guid DoctorId { get; private set; }
    public Guid? ReceptionId { get; private set; }
    public OperatingPlanKind Kind { get; private set; }
    public OperatingPlanStatus Status { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    public DateOnly FirstCoveredMonth { get; private set; }
    public int CurrentRevisionNumber { get; private set; }
    public Guid CreatedByApplicationUserId { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }
    public Guid? ModifiedByApplicationUserId { get; private set; }
    public DateTime? ModifiedOnUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyCollection<OperatingPlanRevision> Revisions => _revisions.AsReadOnly();

    public static Result<OperatingFinancialPlan> Create(Guid id, Guid doctorId, Guid? receptionId,
        OperatingPlanKind kind, OperatingPlanTerms terms, Guid actorId, DateTime now)
    {
        var validation = ValidateTerms(kind, terms);
        return validation.IsFailure ? Result<OperatingFinancialPlan>.Fail(validation.Errors)
            : id == Guid.Empty || doctorId == Guid.Empty || actorId == Guid.Empty || !OperatingFinancialPolicy.ValidUtc(now) ||
                kind == OperatingPlanKind.Salary && (receptionId == null || receptionId == Guid.Empty) ||
                kind == OperatingPlanKind.RecurringExpense && receptionId != null
            ? Result<OperatingFinancialPlan>.Fail(OperatingFinanceErrors.Invalid())
            : Result<OperatingFinancialPlan>.Ok(new(id, doctorId, receptionId, kind, terms, actorId, now));
    }

    public Result Revise(OperatingPlanTerms terms, DateOnly today, Guid actorId, DateTime now, string reason)
    {
        if (Status == OperatingPlanStatus.Ended) return Result.Fail(OperatingFinanceErrors.State());
        var validation = ValidateTerms(Kind, terms);
        if (validation.IsFailure) return validation;
        if (terms.EffectiveFrom <= today || Kind == OperatingPlanKind.Salary && terms.EffectiveFrom.Day != 1 ||
            !OperatingFinancialPolicy.HasReason(reason) || actorId == Guid.Empty || !OperatingFinancialPolicy.ValidUtc(now) ||
            _revisions.Any(r => r.EffectiveFrom >= terms.EffectiveFrom))
            return Result.Fail(OperatingFinanceErrors.Invalid("OperatingFinance.RevisionMustBeFuture"));
        EffectiveTo = terms.EffectiveTo;
        _revisions.Add(OperatingPlanRevision.Create(this, terms, ++CurrentRevisionNumber, Status, actorId, now, reason));
        ModifiedByApplicationUserId = actorId; ModifiedOnUtc = now;
        return Result.Ok();
    }

    public Result ChangeLifecycle(OperatingPlanStatus target, DateOnly today, Guid actorId, DateTime now, string reason)
    {
        if (!Enum.IsDefined(target) || Status == OperatingPlanStatus.Ended || target == Status ||
            Kind == OperatingPlanKind.Salary && target != OperatingPlanStatus.Ended)
            return Result.Fail(OperatingFinanceErrors.State());
        if (!OperatingFinancialPolicy.HasReason(reason) || actorId == Guid.Empty || !OperatingFinancialPolicy.ValidUtc(now))
            return Result.Fail(OperatingFinanceErrors.Invalid("OperatingFinance.ReasonRequired"));
        var previous = _revisions.Where(r => !r.IsLifecycleChange && r.EffectiveFrom <= today)
            .OrderByDescending(r => r.EffectiveFrom).ThenByDescending(r => r.RevisionNumber).FirstOrDefault()
            ?? _revisions.First();
        var terms = previous.ToTerms() with { EffectiveFrom = today, EffectiveTo = target == OperatingPlanStatus.Ended ? today : EffectiveTo };
        Status = target;
        if (target == OperatingPlanStatus.Ended) EffectiveTo = today < EffectiveFrom ? EffectiveFrom : today;
        _revisions.Add(OperatingPlanRevision.Create(this, terms, ++CurrentRevisionNumber, target, actorId, now, reason, true));
        ModifiedByApplicationUserId = actorId; ModifiedOnUtc = now;
        return Result.Ok();
    }

    private static Result ValidateTerms(OperatingPlanKind kind, OperatingPlanTerms terms)
    {
        var allocation = OperatingFinancialPolicy.Allocate(terms.Amount, terms.Allocations);
        if (allocation.IsFailure) return Result.Fail(allocation.Errors);
        return !Enum.IsDefined(kind) || terms.DueDay is < 1 or > 31 || !Enum.IsDefined(terms.Cadence) ||
            !Enum.IsDefined(terms.AmountPolicy) || !Enum.IsDefined(terms.Category) ||
            terms.EffectiveFrom == DateOnly.MinValue || terms.EffectiveFrom == DateOnly.MaxValue || terms.EffectiveTo < terms.EffectiveFrom ||
            string.IsNullOrWhiteSpace(terms.DescriptionAr) || terms.DescriptionAr.Trim().Length > 1000 || terms.DescriptionEn?.Length > 1000 ||
            kind == OperatingPlanKind.Salary && (terms.Cadence != ExpenseCadence.Monthly || terms.AmountPolicy != ExpenseAmountPolicy.Fixed)
            || kind == OperatingPlanKind.Salary && terms.EffectiveFrom.Year == 9999 && terms.EffectiveFrom.Month == 12 && terms.EffectiveFrom.Day > 1
            ? Result.Fail(OperatingFinanceErrors.Invalid()) : Result.Ok();
    }
}

public sealed class OperatingPlanRevision : Entity<Guid>
{
    private readonly List<OperatingPlanAllocation> _allocations = [];
    private OperatingPlanRevision() { }
    public Guid PlanId { get; private set; }
    public Guid DoctorId { get; private set; }
    public int RevisionNumber { get; private set; }
    public decimal Amount { get; private set; }
    public string CurrencyCode { get; private set; } = OperatingFinancialPolicy.CurrencyCode;
    public int DueDay { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    public ExpenseCadence Cadence { get; private set; }
    public ExpenseAmountPolicy AmountPolicy { get; private set; }
    public ExpenseCategoryCode Category { get; private set; }
    public string DescriptionAr { get; private set; } = string.Empty;
    public string? DescriptionEn { get; private set; }
    public OperatingPlanStatus Status { get; private set; }
    public bool IsLifecycleChange { get; private set; }
    public Guid ActorApplicationUserId { get; private set; }
    public DateTime RecordedAtUtc { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public IReadOnlyCollection<OperatingPlanAllocation> Allocations => _allocations.AsReadOnly();

    internal static OperatingPlanRevision Create(OperatingFinancialPlan plan, OperatingPlanTerms terms,
        int revision, OperatingPlanStatus status, Guid actorId, DateTime now, string reason, bool lifecycle = false)
    {
        var result = new OperatingPlanRevision { Id = Guid.NewGuid(), PlanId = plan.Id, DoctorId = plan.DoctorId,
            RevisionNumber = revision, Amount = terms.Amount, DueDay = terms.DueDay, EffectiveFrom = terms.EffectiveFrom,
            EffectiveTo = terms.EffectiveTo, Cadence = terms.Cadence, AmountPolicy = terms.AmountPolicy,
            Category = terms.Category, DescriptionAr = terms.DescriptionAr.Trim(), DescriptionEn = terms.DescriptionEn?.Trim(),
            Status = status, IsLifecycleChange = lifecycle, ActorApplicationUserId = actorId, RecordedAtUtc = now, Reason = reason.Trim() };
        result._allocations.AddRange(terms.Allocations.Select(a => new OperatingPlanAllocation(Guid.NewGuid(), result.Id,
            plan.DoctorId, a.PracticeId, a.Percentage)));
        return result;
    }
    public OperatingPlanTerms ToTerms() => new(Amount, DueDay, EffectiveFrom, EffectiveTo, Cadence, AmountPolicy,
        Category, DescriptionAr, DescriptionEn, _allocations.Select(a => new PracticeAllocationPercentage(a.PracticeId, a.Percentage)).ToArray());
}

public sealed class OperatingPlanAllocation : Entity<Guid>
{
    private OperatingPlanAllocation() { }
    internal OperatingPlanAllocation(Guid id, Guid revisionId, Guid doctorId, Guid practiceId, decimal percentage) : base(id)
    { RevisionId = revisionId; DoctorId = doctorId; PracticeId = practiceId; Percentage = percentage; }
    public Guid RevisionId { get; private set; }
    public Guid DoctorId { get; private set; }
    public Guid PracticeId { get; private set; }
    public decimal Percentage { get; private set; }
}
