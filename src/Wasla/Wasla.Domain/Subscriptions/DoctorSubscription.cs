using System.Text.Json;
using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Results;

namespace Wasla.Domain.Subscriptions;

public sealed class DoctorSubscription : AggregateRoot<Guid>
{
    private DoctorSubscription() { }
    public Guid DoctorId { get; private set; }
    public Guid SubscriptionContractId { get; private set; }
    public SubscriptionContract? Contract { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid PlanId { get; private set; }
    public SubscriptionKind SubscriptionKind { get; private set; }
    public SubscriptionStatus Status { get; private set; }
    public BillingCycle? BillingCycle { get; private set; }
    public int? SelectedPracticeCount { get; private set; }
    public decimal PriceAmount { get; private set; }
    public string Currency { get; private set; } = "";
    public int PracticeLimit { get; private set; }
    public int ReceptionLimit { get; private set; }
    public DateTime? StartsAtUtc { get; private set; }
    public DateTime? EndsAtUtc { get; private set; }
    public DateTime? TrialStartedAtUtc { get; private set; }
    public DateTime? TrialEndsAtUtc { get; private set; }
    public int TrialExtensionCount { get; private set; }
    public string CatalogVersion { get; private set; } = "";
    public string TermsSnapshotJson { get; private set; } = "";
    public bool WillNotRenew { get; private set; }
    public DateTime? CancellationRequestedOnUtc { get; private set; }
    public Guid? ActivatedByApplicationUserId { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }
    public DateTime? ModifiedOnUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public SubscriptionTerms Terms => JsonSerializer.Deserialize<SubscriptionTerms>(TermsSnapshotJson)!;

    public static Result<DoctorSubscription> Select(Guid doctorId, SubscriptionTerms terms, DateTime now, Guid? contractId = null)
    {
        if (doctorId == Guid.Empty || terms.ProductId == Guid.Empty || terms.PlanId == Guid.Empty || !Enum.IsDefined(terms.Kind) ||
            terms.PracticeLimit <= 0 || terms.ReceptionLimit < 0 || terms.PriceAmount < 0 ||
            string.IsNullOrWhiteSpace(terms.Currency) || string.IsNullOrWhiteSpace(terms.CatalogVersion) ||
            terms.Features.Count == 0 || terms.Features.Any(f => !Enum.IsDefined(f.AccessLevel)) ||
            terms.Kind == SubscriptionKind.Paid && (terms.BillingCycle is null || !Enum.IsDefined(terms.BillingCycle.Value) || terms.PriceAmount <= 0) ||
            terms.Kind != SubscriptionKind.Paid && (terms.BillingCycle is not null || terms.PriceAmount != 0 || terms.DurationDays is not > 0))
            return Result<DoctorSubscription>.Fail(SubscriptionErrors.Invalid("InvalidSelection"));
        var subscription = new DoctorSubscription { Id = Guid.NewGuid(), DoctorId = doctorId,
            Status = SubscriptionStatus.Pending, CreatedOnUtc = now };
        subscription.SubscriptionContractId = contractId ?? Guid.NewGuid();
        if (contractId is null) subscription.Contract = new SubscriptionContract(subscription.SubscriptionContractId, doctorId, terms.Kind, now);
        subscription.SetTerms(terms);
        return Result<DoctorSubscription>.Ok(subscription);
    }

    public Result StartUnusedTrial(SubscriptionTerms terms, DateTime start, Guid actor)
    {
        if (SubscriptionKind != SubscriptionKind.Trial || terms.Kind != SubscriptionKind.Trial || TrialStartedAtUtc is not null || StartsAtUtc is not null || Status is not (SubscriptionStatus.Pending or SubscriptionStatus.Cancelled))
            return Result.Fail(SubscriptionErrors.Conflict("TrialAlreadyUsed"));
        SetTerms(terms); Status = SubscriptionStatus.Pending;
        return Activate(start, actor);
    }

    public bool IsValidAt(DateTime now) => Status is SubscriptionStatus.Active or SubscriptionStatus.Scheduled && StartsAtUtc <= now && now < EndsAtUtc;

    public Result Activate(DateTime start, Guid actorId)
    {
        if (Status != SubscriptionStatus.Pending) return Result.Fail(SubscriptionErrors.Invalid());
        StartsAtUtc = start;
        EndsAtUtc = SubscriptionKind == SubscriptionKind.Paid
            ? BillingCycle == global::Wasla.Domain.Subscriptions.BillingCycle.Annual ? start.AddYears(1) : start.AddMonths(1)
            : start.AddDays(Terms.DurationDays!.Value);
        if (SubscriptionKind == SubscriptionKind.Trial) { TrialStartedAtUtc = start; TrialEndsAtUtc = EndsAtUtc; }
        Status = SubscriptionStatus.Active;
        ActivatedByApplicationUserId = actorId;
        ModifiedOnUtc = start;
        return Result.Ok();
    }

    public Result ExtendTrial(int days, string reason, DateTime now)
    {
        if (SubscriptionKind != SubscriptionKind.Trial || !IsValidAt(now) || days <= 0 || string.IsNullOrWhiteSpace(reason))
            return Result.Fail(SubscriptionErrors.Invalid("TrialExtensionInvalid"));
        DateTime extended;
        try { extended = EndsAtUtc!.Value.AddDays(days); }
        catch (ArgumentOutOfRangeException) { return Result.Fail(SubscriptionErrors.Invalid("TrialExtensionInvalid")); }
        EndsAtUtc = extended;
        TrialEndsAtUtc = EndsAtUtc;
        TrialExtensionCount++;
        ModifiedOnUtc = now;
        return Result.Ok();
    }

    public Result SchedulePaidPeriod(DateTime start, Guid actor, DateTime now)
    {
        if (SubscriptionKind != SubscriptionKind.Paid || start <= now) return Result.Fail(SubscriptionErrors.Invalid());
        var result = Activate(start, actor);
        if (result.IsSuccess) { Status = SubscriptionStatus.Scheduled; ModifiedOnUtc = now; }
        return result;
    }
    public void BeginScheduledPeriod(DateTime now)
    {
        if (Status == SubscriptionStatus.Scheduled && StartsAtUtc <= now) { Status = SubscriptionStatus.Active; ModifiedOnUtc = now; }
    }

    public Result Upgrade(SubscriptionTerms terms, DateTime now)
    {
        if (SubscriptionKind != SubscriptionKind.Paid || !IsValidAt(now) || terms.Kind != SubscriptionKind.Paid ||
            terms.ProductId != ProductId || terms.BillingCycle != BillingCycle || terms.PriceAmount < PriceAmount ||
            terms.PracticeLimit < PracticeLimit || terms.ReceptionLimit < ReceptionLimit ||
            terms.PlanId == PlanId && (SelectedPracticeCount is null || terms.SelectedPracticeCount <= SelectedPracticeCount))
            return Result.Fail(SubscriptionErrors.Invalid("ChangeRequestInvalid"));
        SetTerms(terms);
        ModifiedOnUtc = now;
        return Result.Ok();
    }

    public Result CancelAtPeriodEnd(DateTime now)
    {
        if (!IsValidAt(now)) return Result.Fail(SubscriptionErrors.Denied("NotActive"));
        WillNotRenew = true;
        CancellationRequestedOnUtc ??= now;
        ModifiedOnUtc = now;
        return Result.Ok();
    }

    public void Expire(DateTime now)
    {
        if (Status is SubscriptionStatus.Active or SubscriptionStatus.Scheduled && now >= EndsAtUtc) { Status = SubscriptionStatus.Expired; ModifiedOnUtc = now; }
    }
    public void Supersede(DateTime now) { Status = SubscriptionStatus.Superseded; ModifiedOnUtc = now; }
    public void Reject(DateTime now)
    {
        if (Status == SubscriptionStatus.Pending) { Status = SubscriptionStatus.Cancelled; ModifiedOnUtc = now; }
    }
    private void SetTerms(SubscriptionTerms terms)
    {
        ProductId = terms.ProductId; PlanId = terms.PlanId; SubscriptionKind = terms.Kind;
        BillingCycle = terms.BillingCycle; SelectedPracticeCount = terms.SelectedPracticeCount;
        PriceAmount = terms.PriceAmount; Currency = terms.Currency; PracticeLimit = terms.PracticeLimit;
        ReceptionLimit = terms.ReceptionLimit; CatalogVersion = terms.CatalogVersion;
        TermsSnapshotJson = JsonSerializer.Serialize(terms);
    }
}
