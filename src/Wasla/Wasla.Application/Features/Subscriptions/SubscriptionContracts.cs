using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Domain.Results;
using FluentValidation;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Subscriptions;

namespace Wasla.Application.Features.Subscriptions;

public sealed record TrialEligibility(string Status, bool CanActivate, int DurationDays, int PracticeLimit, int ReceptionLimit, DoctorSubscription? Trial);
public sealed record TrialEligibilityQuery : IQuery<TrialEligibility>;
public sealed record StartFreeTrialCommand(string IdempotencyKey) : ICommand<SubscriptionDetails>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record MySubscriptionContractsQuery(Guid? ContractId = null) : IQuery<IReadOnlyList<SubscriptionContractDetails>>;
public sealed record ContractSubscriptionCommand(Guid ContractId, SubscriptionOperation Operation, SubscriptionMutationRequest Request, string? IdempotencyKey)
    : ICommand<SubscriptionDetails>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record RenewalSelectionCommand(Guid ContractId, SubscriptionMutationRequest Request, string IdempotencyKey)
    : ICommand<SubscriptionDetails>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record RenewalPreviewQuery(Guid ContractId, SubscriptionSelection Selection) : IQuery<RenewalPreview>;
public sealed record RenewalPreview(SubscriptionTerms Terms, IReadOnlyList<PracticeSubscriptionAllocation> Allocations, IReadOnlyList<Guid> ActiveReceptionIds, string PeriodRowVersion);
internal sealed class RenewalSelectionCommandValidator : AbstractValidator<RenewalSelectionCommand>
{
    public RenewalSelectionCommandValidator()
    {
        RuleFor(x => x.ContractId).NotEmpty(); RuleFor(x => x.Request).NotNull();
        RuleFor(x => x.Request.RowVersion).Must(x => x is not null && Wasla.Application.Features.Doctors.RowVersionCodec.IsValid(x));
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(150); RuleFor(x => x.Request.RetainedAllocations).NotNull();
    }
}
internal sealed class RenewalSelectionCommandHandler(SubscriptionManager manager) : ICommandHandler<RenewalSelectionCommand, SubscriptionDetails>
{
    public Task<Result<SubscriptionDetails>> Handle(RenewalSelectionCommand request, CancellationToken ct) => manager.SaveRenewalSelectionAsync(request, ct);
}
internal sealed class RenewalPreviewQueryHandler(SubscriptionManager manager) : IQueryHandler<RenewalPreviewQuery, RenewalPreview>
{
    public Task<Result<RenewalPreview>> Handle(RenewalPreviewQuery request, CancellationToken ct) => manager.RenewalPreviewAsync(request, ct);
}
internal sealed class ContractSubscriptionCommandValidator : AbstractValidator<ContractSubscriptionCommand>
{
    public ContractSubscriptionCommandValidator()
    {
        RuleFor(x => x.ContractId).NotEmpty();
        RuleFor(x => x.Request).NotNull();
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Request.RowVersion).Must(x => x is not null && Wasla.Application.Features.Doctors.RowVersionCodec.IsValid(x));
        RuleFor(x => x.Request.RetainedAllocations).Must(x => x is null || x.Count <= 10000);
    }
}
internal sealed class ContractSubscriptionCommandHandler(SubscriptionManager manager) : ICommandHandler<ContractSubscriptionCommand, SubscriptionDetails>
{
    public Task<Result<SubscriptionDetails>> Handle(ContractSubscriptionCommand request, CancellationToken ct) => manager.ExecuteForContractAsync(request, ct);
}
public sealed record AllocatedPracticeSummary(Guid PracticeId, string NameAr, bool IsActive, Guid AllocationId, string RowVersion);
public sealed record SubscriptionContractDetails(Guid SubscriptionContractId, IReadOnlyList<SubscriptionDetails> Periods,
    IReadOnlyList<AllocatedPracticeSummary> Practices, SubscriptionUsage Usage, int AvailablePracticeSeats, int AvailableReceptionSeats,
    SubscriptionTerms? RenewalPreview, string UpcomingStatus);
internal sealed class StartFreeTrialCommandValidator : AbstractValidator<StartFreeTrialCommand>
{
    public StartFreeTrialCommandValidator() { RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(150); }
}
internal sealed class StartFreeTrialCommandHandler(SubscriptionManager manager) : ICommandHandler<StartFreeTrialCommand, SubscriptionDetails>
{
    public Task<Result<SubscriptionDetails>> Handle(StartFreeTrialCommand request, CancellationToken ct) => manager.StartTrialAsync(request.IdempotencyKey, ct);
}
internal sealed class TrialEligibilityQueryHandler(SubscriptionManager manager) : IQueryHandler<TrialEligibilityQuery, TrialEligibility>
{
    public Task<Result<TrialEligibility>> Handle(TrialEligibilityQuery request, CancellationToken ct) => manager.TrialEligibilityAsync(ct);
}
internal sealed class MySubscriptionContractsQueryHandler(SubscriptionManager manager) : IQueryHandler<MySubscriptionContractsQuery, IReadOnlyList<SubscriptionContractDetails>>
{
    public Task<Result<IReadOnlyList<SubscriptionContractDetails>>> Handle(MySubscriptionContractsQuery request, CancellationToken ct) => manager.ContractsAsync(request.ContractId, ct);
}

public sealed partial class SubscriptionManager
{
    public async Task<Result<RenewalPreview>> RenewalPreviewAsync(RenewalPreviewQuery request, CancellationToken ct)
    {
        var doctorId = await OwnDoctorAsync(SubscriptionPermissionNames.ViewOwn, ct);
        if (doctorId is null) return Result<RenewalPreview>.Fail(SubscriptionErrors.Denied());
        var period = (await store.PeriodsAsync(doctorId.Value, ct)).FirstOrDefault(p => p.SubscriptionContractId == request.ContractId);
        if (period is null) return Result<RenewalPreview>.Fail(SubscriptionErrors.NotFound);
        var terms = await resolver.ResolveAsync(request.Selection, ct);
        return terms.IsFailure ? Result<RenewalPreview>.Fail(terms.Errors) : Result<RenewalPreview>.Ok(new(terms.Value,
            await store.AllocationsAsync(request.ContractId, ct), await store.ActiveReceptionIdsAsync(request.ContractId, null, ct), Convert.ToBase64String(period.RowVersion)));
    }
    public async Task<Result<SubscriptionDetails>> SaveRenewalSelectionAsync(RenewalSelectionCommand command, CancellationToken ct)
    {
        var doctorId = await OwnDoctorAsync(SubscriptionPermissionNames.RequestChangeOwn, ct);
        if (doctorId is null) return Result<SubscriptionDetails>.Fail(SubscriptionErrors.Denied());
        await store.AcquireSubscriptionCapacityLockAsync(doctorId.Value, ct);
        var doctor = await doctors.FindDoctorByIdAsync(doctorId.Value, ct);
        if (doctor?.ApprovalStatus != DoctorApprovalStatus.Approved) return Result<SubscriptionDetails>.Fail(SubscriptionErrors.Denied("DoctorNotApproved"));
        var period = (await store.PeriodsAsync(doctorId.Value, ct)).FirstOrDefault(p => p.SubscriptionContractId == command.ContractId && p.Status is SubscriptionStatus.Active or SubscriptionStatus.Expired);
        if (period is null || period.SubscriptionKind != SubscriptionKind.Paid) return Result<SubscriptionDetails>.Fail(SubscriptionErrors.NotFound);
        var actor = currentUser.UserId!.Value;
        var fingerprint = MutationFingerprint(new { Operation = "RenewalSelection", Actor = actor, command.ContractId, command.Request });
        var receipt = await store.ReceiptAsync(command.IdempotencyKey.Trim(), ct);
        if (receipt is not null) return receipt.ActorId == actor && receipt.Fingerprint == fingerprint
            ? Result<SubscriptionDetails>.Ok(await DetailsAsync(period, false, ct)) : Result<SubscriptionDetails>.Fail(SubscriptionErrors.Conflict("IdempotencyConflict"));
        var row = Wasla.Application.Features.Doctors.RowVersionCodec.Decode(command.Request.RowVersion ?? "");
        if (row is null || !period.RowVersion.AsSpan().SequenceEqual(row)) return Result<SubscriptionDetails>.Fail(SubscriptionErrors.Conflict("StaleRowVersion"));
        if ((await store.ChangesAsync(period.Id, ct)).Any(c => c.Status is SubscriptionChangeStatus.Pending or SubscriptionChangeStatus.Scheduled)) return Result<SubscriptionDetails>.Fail(SubscriptionErrors.Conflict("ChangeRequestAlreadyPending"));
        var terms = await resolver.ResolveAsync(command.Request.Selection ?? TargetSelection(period, command.Request), ct);
        if (terms.IsFailure) return Result<SubscriptionDetails>.Fail(terms.Errors);
        if (terms.Value.Kind != SubscriptionKind.Paid || terms.Value.ProductId != period.ProductId) return Result<SubscriptionDetails>.Fail(SubscriptionErrors.Invalid());
        var fit = await ValidateRetainedAsync(period, terms.Value, command.Request.RetainedAllocations, ct);
        if (fit.IsFailure) return Result<SubscriptionDetails>.Fail(fit.Errors);
        var change = new SubscriptionChangeRequest(period.Id, actor, SubscriptionChangeType.Renewal, terms.Value, 0, clock.UtcNow);
        change.SetRetainedAllocations(command.Request.RetainedAllocations); store.Add(change);
        store.Add(new SubscriptionMutationReceipt(command.IdempotencyKey.Trim(), actor, fingerprint, change.Id, clock.UtcNow));
        lifecycle.History(period, "RenewalSelectionRequested", period.Status, actor, null, "renewal-selection:" + change.Id);
        await store.SaveChangesAsync(ct);
        return Result<SubscriptionDetails>.Ok(await DetailsAsync(period, false, ct));
    }
    public async Task<Result<SubscriptionDetails>> ExecuteForContractAsync(ContractSubscriptionCommand command, CancellationToken ct)
    {
        var permission = command.Operation == SubscriptionOperation.Cancel ? SubscriptionPermissionNames.CancelAtPeriodEndOwn : SubscriptionPermissionNames.RequestChangeOwn;
        var doctorId = await OwnDoctorAsync(permission, ct);
        if (doctorId is null || command.Operation is not (SubscriptionOperation.RequestChange or SubscriptionOperation.Cancel)) return Result<SubscriptionDetails>.Fail(SubscriptionErrors.Denied());
        await store.AcquireSubscriptionCapacityLockAsync(doctorId.Value, ct);
        var periods = (await store.PeriodsAsync(doctorId.Value, ct)).Where(p => p.SubscriptionContractId == command.ContractId).ToArray();
        var period = periods.SingleOrDefault(p => p.IsValidAt(clock.UtcNow)) ?? periods.FirstOrDefault();
        if (period is null) return Result<SubscriptionDetails>.Fail(SubscriptionErrors.NotFound);
        var fingerprint = MutationFingerprint(new { currentUser.UserId, command.ContractId, command.Operation, command.Request });
        var receipt = await store.ReceiptAsync(command.IdempotencyKey!.Trim(), ct);
        if (receipt is not null) return receipt.ActorId == currentUser.UserId && receipt.Fingerprint == fingerprint
            ? Result<SubscriptionDetails>.Ok(await DetailsAsync((await store.FindAsync(receipt.ResultId, ct))!, false, ct)) : Result<SubscriptionDetails>.Fail(SubscriptionErrors.Conflict("IdempotencyConflict"));
        var result = await ExecuteAsync(new(command.Operation, period.Id, command.Request, command.IdempotencyKey), ct);
        if (result.IsSuccess)
        {
            store.Add(new SubscriptionMutationReceipt(command.IdempotencyKey.Trim(), currentUser.UserId!.Value, fingerprint, period.Id, clock.UtcNow));
            await store.SaveChangesAsync(ct);
        }
        return result;
    }
    private async Task<Result> ValidateRetainedAsync(DoctorSubscription source, SubscriptionTerms target,
        IReadOnlyList<RetainedPracticeAllocation>? selected, CancellationToken ct)
    {
        var allocations = await store.AllocationsAsync(source.SubscriptionContractId, ct);
        if (selected is null)
        {
            var usage = await store.ContractUsageAsync(source.SubscriptionContractId, ct);
            return usage.PracticeCount > target.PracticeLimit || usage.ReceptionCount > target.ReceptionLimit || allocations.Any(a => a.LegacyGrandfathered)
                ? Result.Fail(SubscriptionErrors.Conflict("MigrationRequiresAllocationSelection")) : Result.Ok();
        }
        if (selected.Any(p => p is null || p.ReceptionIds is null || p.PracticeId == Guid.Empty || p.AllocationId == Guid.Empty))
            return Result.Fail(SubscriptionErrors.Invalid("AllocationSelectionInvalid"));
        if (selected.Count > target.PracticeLimit || selected.Select(p => p.PracticeId).Distinct().Count() != selected.Count)
            return Result.Fail(SubscriptionErrors.Conflict("ContractCapacityReached"));
        var receptionIds = new HashSet<Guid>();
        foreach (var retained in selected)
        {
            if (!allocations.Any(a => a.Id == retained.AllocationId && a.DoctorPracticeId == retained.PracticeId)) return Result.Fail(SubscriptionErrors.Conflict("AllocationConflict"));
            var assignments = await store.AssignmentsAsync(retained.PracticeId, ct);
            foreach (var receptionId in retained.ReceptionIds)
            {
                var reception = await doctors.FindDoctorReceptionAsync(receptionId, ct);
                if (reception?.Reception.OwnerDoctorId != source.DoctorId || !reception.User.IsActive ||
                    !assignments.Any(a => a.ReceptionId == receptionId && a.IsActive)) return Result.Fail(SubscriptionErrors.Invalid("ReceptionSelectionInvalid"));
                receptionIds.Add(receptionId);
            }
        }
        return receptionIds.Count > target.ReceptionLimit ? Result.Fail(SubscriptionErrors.Conflict("ReceptionCapacityReached")) : Result.Ok();
    }
    public async Task<Result<TrialEligibility>> TrialEligibilityAsync(CancellationToken ct)
    {
        var doctorId = await OwnDoctorAsync(SubscriptionPermissionNames.ViewOwn, ct);
        if (doctorId is null) return Result<TrialEligibility>.Fail(SubscriptionErrors.Denied());
        var doctor = (await doctors.FindDoctorByIdAsync(doctorId.Value, ct))!;
        var trial = (await store.PeriodsAsync(doctorId.Value, ct)).SingleOrDefault(p => p.SubscriptionKind == SubscriptionKind.Trial && (p.TrialStartedAtUtc != null || p.StartsAtUtc != null));
        var offer = options.CurrentValue.FullPlatform.Trial;
        var canStart = offer.Enabled && doctor.ApprovalStatus == DoctorApprovalStatus.Approved && trial is null;
        return Result<TrialEligibility>.Ok(new(trial is null ? canStart ? "TrialAvailable" : "Unavailable" : trial.IsValidAt(clock.UtcNow) ? "TrialActive" : "TrialExpired",
            canStart, offer.DurationDays, offer.PracticeLimit, offer.ReceptionLimit, trial));
    }
    public async Task<Result<SubscriptionDetails>> StartTrialAsync(string key, CancellationToken ct)
    {
        var doctorId = await OwnDoctorAsync(SubscriptionPermissionNames.ActivateTrialOwn, ct);
        if (doctorId is null || string.IsNullOrWhiteSpace(key) || key.Trim().Length > 150) return Result<SubscriptionDetails>.Fail(SubscriptionErrors.Denied());
        await store.AcquireSubscriptionCapacityLockAsync(doctorId.Value, ct);
        var doctor = (await doctors.FindDoctorByIdAsync(doctorId.Value, ct))!;
        var user = await doctors.FindUserByIdAsync(doctor.ApplicationUserId, ct);
        if (doctor.ApprovalStatus != DoctorApprovalStatus.Approved || user is not { IsActive: true, IsFirstLogin: false })
            return Result<SubscriptionDetails>.Fail(SubscriptionErrors.Denied("DoctorNotApproved"));
        var actor = currentUser.UserId!.Value;
        var fingerprint = MutationFingerprint(new { Operation = "StartTrial", DoctorId = doctorId.Value, Actor = actor });
        var receipt = await store.ReceiptAsync(key.Trim(), ct);
        if (receipt is not null)
            return receipt.ActorId == actor && receipt.Fingerprint == fingerprint
                ? Result<SubscriptionDetails>.Ok(await DetailsAsync((await store.FindAsync(receipt.ResultId, ct))!, false, ct))
                : Result<SubscriptionDetails>.Fail(SubscriptionErrors.Conflict("IdempotencyConflict"));
        var existing = (await store.PeriodsAsync(doctorId.Value, ct)).SingleOrDefault(p => p.SubscriptionKind == SubscriptionKind.Trial);
        if (existing is not null && (existing.TrialStartedAtUtc is not null || existing.StartsAtUtc is not null))
            return Result<SubscriptionDetails>.Fail(SubscriptionErrors.Conflict("TrialAlreadyUsed"));
        var offer = options.CurrentValue.FullPlatform.Trial;
        var terms = await resolver.ResolveAsync(new("FULL_PLATFORM", "STARTER", null, offer.OfferCode), ct);
        if (terms.IsFailure) return Result<SubscriptionDetails>.Fail(terms.Errors);
        var selected = existing is null ? lifecycle.Select(doctorId.Value, terms.Value) : Result<DoctorSubscription>.Ok(existing);
        if (selected.IsFailure) return Result<SubscriptionDetails>.Fail(selected.Errors);
        // A pending old trial is eligibility, never evidence of previous use. Use the configured offer at actual acceptance.
        if (existing is not null)
        {
            store.RemoveFeatures(existing.Id);
            await store.SaveChangesAsync(ct);
        }
        var activation = selected.Value.StartUnusedTrial(terms.Value, clock.UtcNow, actor);
        if (existing is not null && activation.IsSuccess) lifecycle.AddFeatures(existing);
        if (activation.IsFailure) return Result<SubscriptionDetails>.Fail(activation.Errors);
        lifecycle.History(selected.Value, "TrialStarted", SubscriptionStatus.Pending, actor, null, $"trial-started:{selected.Value.Id}");
        await lifecycle.ScheduleRemindersAsync(selected.Value, ct);
        await lifecycle.NotifyAsync(selected.Value, "TrialStarted", $"subscription-trial-started:{selected.Value.Id}", ct);
        store.Add(new SubscriptionMutationReceipt(key.Trim(), actor, fingerprint, selected.Value.Id, clock.UtcNow));
        await store.SaveChangesAsync(ct);
        return Result<SubscriptionDetails>.Ok(await DetailsAsync(selected.Value, false, ct));
    }
    public async Task<Result<IReadOnlyList<SubscriptionContractDetails>>> ContractsAsync(Guid? contractId, CancellationToken ct)
    {
        var doctorId = await OwnDoctorAsync(SubscriptionPermissionNames.ViewOwn, ct);
        if (doctorId is null) return Result<IReadOnlyList<SubscriptionContractDetails>>.Fail(SubscriptionErrors.Denied());
        var periods = await store.PeriodsAsync(doctorId.Value, ct);
        if (contractId.HasValue && periods.All(p => p.SubscriptionContractId != contractId)) return Result<IReadOnlyList<SubscriptionContractDetails>>.Fail(SubscriptionErrors.NotFound);
        var result = new List<SubscriptionContractDetails>();
        foreach (var group in periods.Where(p => !contractId.HasValue || p.SubscriptionContractId == contractId).GroupBy(p => p.SubscriptionContractId))
        {
            var allocations = await store.AllocationsAsync(group.Key, ct);
            var practices = new List<AllocatedPracticeSummary>();
            foreach (var a in allocations)
            {
                var p = (await doctors.FindDoctorPracticeAsync(a.DoctorPracticeId, ct))!;
                practices.Add(new(p.Id, p.NameAr, p.IsActive, a.Id, Convert.ToBase64String(a.RowVersion)));
            }
            var details = new List<SubscriptionDetails>();
            foreach (var period in group) details.Add(await DetailsAsync(period, false, ct));
            var usage = await store.ContractUsageAsync(group.Key, ct);
            var effective = group.SingleOrDefault(p => p.IsValidAt(clock.UtcNow));
            var latest = effective ?? group.OrderByDescending(p => p.StartsAtUtc).ThenByDescending(p => p.CreatedOnUtc).First();
            SubscriptionTerms? renewalPreview = null;
            if (latest.SubscriptionKind == SubscriptionKind.Paid)
            {
                var confirmedNext = group.FirstOrDefault(p => p.Status == SubscriptionStatus.Scheduled && p.StartsAtUtc > clock.UtcNow);
                var acceptedIntent = (await store.ChangesAsync(latest.Id, ct)).FirstOrDefault(c => c.Status == SubscriptionChangeStatus.Scheduled ||
                    c.Status == SubscriptionChangeStatus.Pending && c.ChangeType == SubscriptionChangeType.Renewal);
                renewalPreview = confirmedNext?.Terms ?? acceptedIntent?.TargetTerms;
                if (renewalPreview is null)
                {
                    var quote = await resolver.ResolveAsync(new(latest.Terms.ProductCode, latest.Terms.PlanCode, latest.BillingCycle, SelectedPracticeCount: latest.SelectedPracticeCount), ct);
                    if (quote.IsSuccess) renewalPreview = quote.Value;
                }
            }
            var upcoming = group.Any(p => p.Status == SubscriptionStatus.Scheduled && p.StartsAtUtc > clock.UtcNow) ? "RenewalConfirmed"
                : effective is null ? latest.Status == SubscriptionStatus.Pending ? "PendingPayment" : "Expired"
                : effective.EndsAtUtc <= clock.UtcNow.AddDays(effective.SubscriptionKind == SubscriptionKind.Trial ? 5 : 7) ? "ExpiringSoon" : "Active";
            result.Add(new(group.Key, details, practices, usage, Math.Max(0, (effective?.PracticeLimit ?? 0) - usage.PracticeCount), Math.Max(0, (effective?.ReceptionLimit ?? 0) - usage.ReceptionCount), renewalPreview, upcoming));
        }
        return Result<IReadOnlyList<SubscriptionContractDetails>>.Ok(result);
    }
    internal static string MutationFingerprint(object value) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
        System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(value))));
}
