using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using FluentValidation;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.Doctors;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Subscriptions;

namespace Wasla.Application.Features.Subscriptions;

public enum SubscriptionOperation { Activate = 1, ExtendTrial = 2, RequestChange = 3, ApproveChange = 4, RejectChange = 5, Renew = 6, Cancel = 7, SelectPaid = 8 }
public sealed record SubscriptionMutationRequest(
    string? RowVersion = null, SubscriptionPaymentMethod PaymentMethod = SubscriptionPaymentMethod.Cash,
    decimal? AmountReceived = null, DateTime? PaidAtUtc = null, string? Reference = null, string? Notes = null,
    int? AdditionalDays = null, string? Reason = null, SubscriptionSelection? Selection = null,
    [property: System.Text.Json.Serialization.JsonConverter(typeof(SubscriptionChangeTypeInputConverter))]
    SubscriptionChangeType? ChangeType = null, string? ChangeRowVersion = null,
    string? TargetPlanCode = null, int? TargetPracticeCount = null, BillingCycle? TargetBillingCycle = null,
    IReadOnlyList<RetainedPracticeAllocation>? RetainedAllocations = null);
public sealed record SubscriptionCommand(SubscriptionOperation Operation, Guid? SubscriptionId,
    SubscriptionMutationRequest Request, string? IdempotencyKey = null, Guid? RequestId = null)
    : ICommand<SubscriptionDetails>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record SubscriptionQuery(Guid? SubscriptionId = null) : IQuery<SubscriptionDetails>;
public sealed record SubscriptionListQuery(SubscriptionListFilter Filter) : IQuery<PagedResponse<DoctorSubscription>>;
public sealed record CommercialCatalogQuery : IQuery<CommercialCatalogResponse>;
public sealed record CommercialCatalogResponse(string CatalogVersion, string Currency, IReadOnlyList<CommercialProductResponse> Products,
    TrialOfferOptions TrialOffer, BookingOnlyOptions BookingOnly, SubscriptionPaymentOptions PaymentMethods, SubscriptionPolicyOptions Policies);
public sealed record CommercialProductResponse(string Code, string NameAr, string? NameEn, bool Enabled, IReadOnlyList<CommercialPlanResponse> Plans);
public sealed record CommercialPlanResponse(string Code, string NameAr, string? NameEn, SubscriptionPlanOptions? Pricing,
    IReadOnlyList<BillingCycle> BillingCycles, IReadOnlyList<CommercialFeatureResponse> Features);
public sealed record CommercialFeatureResponse(string Code, string NameAr, string? NameEn, string GroupCode,
    FeatureImplementationStatus ImplementationStatus, FeatureReleaseReadiness ReleaseReadiness, FeatureAccessLevel AccessLevel, bool IsReleaseReady);
public sealed record SubscriptionDetails(DoctorSubscription Subscription, SubscriptionUsage Usage,
    IReadOnlyList<SubscriptionFeatureSnapshot> Entitlements, IReadOnlyList<SubscriptionChangeRequest> ChangeRequests,
    IReadOnlyList<SubscriptionPayment>? Payments, IReadOnlyList<SubscriptionHistory>? History,
    string RowVersion, DoctorSubscription? PendingSubscription, DoctorSubscription? NextPeriod, SubscriptionDoctorInfo Doctor);
public sealed record SubscriptionDoctorInfo(Guid DoctorId, string NameAr, string? NameEn, string Email, DoctorApprovalStatus ApprovalStatus);

internal sealed class SubscriptionCommandValidator : AbstractValidator<SubscriptionCommand>
{
    public SubscriptionCommandValidator()
    {
        RuleFor(x => x.Operation).IsInEnum();
        RuleFor(x => x.Request).NotNull();
        RuleFor(x => x.Request.RowVersion).Must(value => value is not null && RowVersionCodec.IsValid(value)).When(x => x.Operation != SubscriptionOperation.SelectPaid && (x.Operation != SubscriptionOperation.RequestChange || x.Request.RowVersion is not null));
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(150).When(x => x.Operation is SubscriptionOperation.Activate or SubscriptionOperation.Renew or SubscriptionOperation.ApproveChange or SubscriptionOperation.SelectPaid);
        RuleFor(x => x.Request.Reason).NotEmpty().MaximumLength(2000).When(x => x.Operation is SubscriptionOperation.ExtendTrial or SubscriptionOperation.RejectChange);
        RuleFor(x => x.Request.AdditionalDays).GreaterThan(0).When(x => x.Operation == SubscriptionOperation.ExtendTrial);
        RuleFor(x => x.Request.Reference).MaximumLength(200);
        RuleFor(x => x.Request.Notes).MaximumLength(2000);
        RuleFor(x => x.Request.PaymentMethod).IsInEnum();
        RuleFor(x => x.Request.Selection).NotNull().When(x => x.Operation == SubscriptionOperation.SelectPaid);
        RuleFor(x => x.Request.TargetPlanCode).MaximumLength(200);
        RuleFor(x => x.Request.TargetPracticeCount).GreaterThan(0).When(x => x.Request.TargetPracticeCount.HasValue);
        RuleFor(x => x.Request.TargetBillingCycle).IsInEnum().When(x => x.Request.TargetBillingCycle.HasValue);
        RuleFor(x => x.Request.Reason).MaximumLength(2000);
        RuleFor(x => x.Request.ChangeType).NotNull().When(x => x.Operation == SubscriptionOperation.RequestChange);
        RuleFor(x => x.Request.ChangeType).IsInEnum().When(x => x.Request.ChangeType.HasValue);
        RuleFor(x => x.Request.ChangeRowVersion).Must(value => value is not null && RowVersionCodec.IsValid(value)).When(x => x.Operation is SubscriptionOperation.ApproveChange or SubscriptionOperation.RejectChange);
    }
}
internal sealed class SubscriptionListQueryValidator : AbstractValidator<SubscriptionListQuery>
{
    public SubscriptionListQueryValidator()
    { RuleFor(x => x.Filter.PageNumber).InclusiveBetween(1, 1000000); RuleFor(x => x.Filter.PageSize).InclusiveBetween(1, 100); RuleFor(x => x.Filter.Search).MaximumLength(200); RuleFor(x => x.Filter.Status).IsInEnum().When(x => x.Filter.Status.HasValue); }
}
internal sealed class SubscriptionCommandHandler(SubscriptionManager manager) : ICommandHandler<SubscriptionCommand, SubscriptionDetails>
{
    public Task<Result<SubscriptionDetails>> Handle(SubscriptionCommand request, CancellationToken cancellationToken) => manager.ExecuteAsync(request, cancellationToken);
}
internal sealed class SubscriptionQueryHandler(SubscriptionManager manager) : IQueryHandler<SubscriptionQuery, SubscriptionDetails>
{
    public Task<Result<SubscriptionDetails>> Handle(SubscriptionQuery request, CancellationToken cancellationToken) => manager.ReadAsync(request.SubscriptionId, cancellationToken);
}
internal sealed class SubscriptionListQueryHandler(SubscriptionManager manager, ISubscriptionDataStore store) : IQueryHandler<SubscriptionListQuery, PagedResponse<DoctorSubscription>>
{
    public async Task<Result<PagedResponse<DoctorSubscription>>> Handle(SubscriptionListQuery request, CancellationToken cancellationToken)
    {
        if (await manager.AdminAsync(SubscriptionPermissionNames.ViewAll, cancellationToken) is null) return Result<PagedResponse<DoctorSubscription>>.Fail(SubscriptionErrors.Denied());
        var (items, total) = await store.ListAsync(request.Filter, cancellationToken);
        return Result<PagedResponse<DoctorSubscription>>.Ok(new(items, total, request.Filter.PageNumber, request.Filter.PageSize));
    }
}
internal sealed class CommercialCatalogQueryHandler(ISubscriptionDataStore store, IOptionsMonitor<SubscriptionCatalogOptions> options) : IQueryHandler<CommercialCatalogQuery, CommercialCatalogResponse>
{
    public async Task<Result<CommercialCatalogResponse>> Handle(CommercialCatalogQuery request, CancellationToken cancellationToken)
    {
        var data = await store.CatalogAsync(cancellationToken); var o = options.CurrentValue;
        var products = data.Products.Where(p => p.IsActive && p.IsPubliclySelectable).Select(p => new CommercialProductResponse(
            p.Code, p.NameAr, p.NameEn, p.Code == "FULL_PLATFORM" || o.BookingOnly.Enabled,
            data.Plans.Where(plan => plan.ProductId == p.Id && plan.IsActive && plan.IsPubliclySelectable).Select(plan => new CommercialPlanResponse(
                plan.Code, plan.NameAr, plan.NameEn, o.FullPlatform.Plans.FirstOrDefault(x => x.Code == plan.Code),
                p.Code == "FULL_PLATFORM" ? [BillingCycle.Monthly, BillingCycle.Annual] : [BillingCycle.Monthly],
                (from e in data.Entitlements join f in data.Features on e.FeatureId equals f.Id
                 where e.PlanId == plan.Id && f.IsActive && f.IsPubliclyVisible
                 select new CommercialFeatureResponse(f.Code, f.NameAr, f.NameEn, f.GroupCode, f.ImplementationStatus, f.ReleaseReadiness, e.AccessLevel, f.ReleaseReadiness == FeatureReleaseReadiness.Ready)).ToArray())).ToArray())).ToArray();
        return Result<CommercialCatalogResponse>.Ok(new(o.CatalogVersion, o.Currency, products, o.FullPlatform.Trial, o.BookingOnly, o.PaymentMethods, o.Policies));
    }
}

public sealed partial class SubscriptionManager(ISubscriptionDataStore store, IWaslaDataStore doctors, ICurrentUser currentUser,
    IDateTimeProvider clock, IOptionsMonitor<SubscriptionCatalogOptions> options, ISubscriptionCatalogResolver resolver, SubscriptionLifecycle lifecycle)
{
    public async Task<Guid?> AdminAsync(string permission, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } id) return null;
        var access = await doctors.GetAccessSnapshotAsync(id, ct);
        return access is { User.IsActive: true, User.IsFirstLogin: false, User.UserType: UserType.SuperAdmin, SuperAdminId: not null } &&
            access.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase) ? id : null;
    }
    private async Task<Guid?> OwnDoctorAsync(string permission, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } id) return null;
        var access = await doctors.GetAccessSnapshotAsync(id, ct);
        return access is { User.IsActive: true, User.IsFirstLogin: false, User.UserType: UserType.Doctor, DoctorId: not null } &&
            access.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase) ? access.DoctorId : null;
    }
    public async Task<Result<SubscriptionDetails>> ReadAsync(Guid? id, CancellationToken ct)
    {
        DoctorSubscription? s;
        if (id.HasValue)
        {
            if (await AdminAsync(SubscriptionPermissionNames.ViewDetails, ct) is null) return Result<SubscriptionDetails>.Fail(SubscriptionErrors.Denied());
            s = await store.FindAsync(id.Value, ct);
        }
        else
        {
            var doctor = await OwnDoctorAsync(SubscriptionPermissionNames.ViewOwn, ct);
            if (doctor is null) return Result<SubscriptionDetails>.Fail(SubscriptionErrors.Denied());
            var periods = await store.PeriodsAsync(doctor.Value, ct);
            if (periods.Select(p => p.SubscriptionContractId).Distinct().Skip(1).Any())
                return Result<SubscriptionDetails>.Fail(SubscriptionErrors.Conflict("MultipleContractsRequireSelection"));
            s = periods.Where(p => p.IsValidAt(clock.UtcNow)).SingleOrDefault() ?? (periods.Count == 0 ? null : periods[0]);
        }
        return s is null ? Result<SubscriptionDetails>.Fail(SubscriptionErrors.NotFound) : Result<SubscriptionDetails>.Ok(await DetailsAsync(s, id.HasValue, ct));
    }
    private async Task<SubscriptionDetails> DetailsAsync(DoctorSubscription s, bool admin, CancellationToken ct)
    {
        var doctor = (await doctors.FindDoctorByIdAsync(s.DoctorId, ct))!;
        var user = (await doctors.FindUserByIdAsync(doctor.ApplicationUserId, ct))!;
        return new(s,
        await store.ContractUsageAsync(s.SubscriptionContractId, ct), await store.FeaturesAsync(s.Id, ct), await store.ChangesAsync(s.Id, ct),
        admin ? await store.PaymentsAsync(s.Id, ct) : null, admin ? await store.HistoryAsync(s.Id, ct) : null,
        Convert.ToBase64String(s.RowVersion), (await store.PeriodsAsync(s.DoctorId, ct)).SingleOrDefault(p => p.SubscriptionContractId == s.SubscriptionContractId && p.Status == SubscriptionStatus.Pending),
        (await store.PeriodsAsync(s.DoctorId, ct)).SingleOrDefault(p => p.SubscriptionContractId == s.SubscriptionContractId && p.Status == SubscriptionStatus.Scheduled), new(doctor.Id, doctor.NameAr, doctor.NameEn, user.Email, doctor.ApprovalStatus));
    }

    public async Task<Result<SubscriptionDetails>> ExecuteAsync(SubscriptionCommand command, CancellationToken ct)
    {
        var own = command.Operation is SubscriptionOperation.RequestChange or SubscriptionOperation.Cancel or SubscriptionOperation.SelectPaid;
        var permission = command.Operation switch
        {
            SubscriptionOperation.Activate => SubscriptionPermissionNames.Activate,
            SubscriptionOperation.ExtendTrial => SubscriptionPermissionNames.ExtendTrial,
            SubscriptionOperation.Renew => SubscriptionPermissionNames.Renew,
            SubscriptionOperation.ApproveChange or SubscriptionOperation.RejectChange => SubscriptionPermissionNames.ReviewChangeRequests,
            SubscriptionOperation.Cancel => SubscriptionPermissionNames.CancelAtPeriodEndOwn,
            _ => SubscriptionPermissionNames.RequestChangeOwn
        };
        Guid? owner = own ? await OwnDoctorAsync(permission, ct) : null;
        Guid? actor = own ? currentUser.UserId : await AdminAsync(permission, ct);
        if (actor is null || own && owner is null) return Result<SubscriptionDetails>.Fail(SubscriptionErrors.Denied());
        if (command.Operation == SubscriptionOperation.SelectPaid) return await SelectPaidAsync(owner!.Value, command, ct);
        var doctorId = command.SubscriptionId.HasValue ? await store.DoctorIdAsync(command.SubscriptionId.Value, ct) : owner;
        if (doctorId is null || own && doctorId != owner) return Result<SubscriptionDetails>.Fail(SubscriptionErrors.NotFound);
        await store.AcquireSubscriptionCapacityLockAsync(doctorId.Value, ct);
        if (!command.SubscriptionId.HasValue && (await store.PeriodsAsync(doctorId.Value, ct)).Select(p => p.SubscriptionContractId).Distinct().Skip(1).Any())
            return Result<SubscriptionDetails>.Fail(SubscriptionErrors.Conflict("MultipleContractsRequireSelection"));
        var s = command.SubscriptionId.HasValue ? await store.FindAsync(command.SubscriptionId.Value, ct) : await store.CurrentAsync(doctorId.Value, ct) ?? await store.LatestEndedAsync(doctorId.Value, ct);
        if (own && !command.SubscriptionId.HasValue && (await store.ScheduledAsync(doctorId.Value, ct)) is { } effectiveNext && effectiveNext.IsValidAt(clock.UtcNow)) s = effectiveNext;
        if (s is null) return Result<SubscriptionDetails>.Fail(SubscriptionErrors.NotFound);
        var r = command.Request; var now = clock.UtcNow;
        var paymentType = command.Operation == SubscriptionOperation.Renew ? SubscriptionPaymentType.Renewal : command.Operation == SubscriptionOperation.ApproveChange ? SubscriptionPaymentType.UpgradeDifference : SubscriptionPaymentType.Initial;
        if (command.IdempotencyKey is { Length: > 0 } key)
        {
            var prior = await store.PaymentByKeyAsync(key.Trim(), ct);
            if (prior is not null)
            {
                var ledger = (await store.HistoryAsync(s.Id, ct)).SingleOrDefault(h => h.EventIdempotencyKey == "payment-request:" + key.Trim());
                var fingerprint = PaymentFingerprint(command, actor.Value, s.Id);
                var matches = prior.PaymentType == paymentType && prior.Amount == r.AmountReceived && prior.PaymentMethod == r.PaymentMethod &&
                    prior.RecordedByApplicationUserId == actor && ledger?.MetadataJson is { } metadata &&
                    JsonSerializer.Deserialize<PaymentRequestMetadata>(metadata)?.Fingerprint == fingerprint &&
                    (prior.SubscriptionId == s.Id || paymentType == SubscriptionPaymentType.Renewal &&
                    (await store.HistoryAsync(s.Id, ct)).Any(h => h.EventIdempotencyKey == "renewal:" + key.Trim()));
                return matches ? Result<SubscriptionDetails>.Ok(await DetailsAsync((await store.FindAsync(prior.SubscriptionId, ct))!, !own, ct)) : Result<SubscriptionDetails>.Fail(SubscriptionErrors.Conflict("PaymentAlreadyRecorded"));
            }
        }
        var row = RowVersionCodec.Decode(r.RowVersion ?? "");
        if ((command.Operation != SubscriptionOperation.RequestChange || r.RowVersion is not null) &&
            (row is null || !s.RowVersion.AsSpan().SequenceEqual(row))) return Result<SubscriptionDetails>.Fail(SubscriptionErrors.Conflict());
        Result result;
        switch (command.Operation)
        {
            case SubscriptionOperation.Activate:
                if (s.SubscriptionKind != SubscriptionKind.Paid || s.Status != SubscriptionStatus.Pending) return Result<SubscriptionDetails>.Fail(SubscriptionErrors.Invalid());
                result = await ConfirmPaymentAsync(s, r, command.IdempotencyKey, actor.Value, SubscriptionPaymentType.Initial, s.PriceAmount, ct);
                if (result.IsFailure) break;
                result = s.Activate(now, actor.Value);
                if (result.IsSuccess)
                {
                    lifecycle.History(s, "Activated", SubscriptionStatus.Pending, actor, null, "activated:" + s.Id);
                    await lifecycle.ScheduleRemindersAsync(s, ct);
                    await lifecycle.NotifyAsync(s, "Activated", "subscription-activated:" + s.Id, ct);
                }
                break;
            case SubscriptionOperation.ExtendTrial:
                if (!options.CurrentValue.FullPlatform.Trial.ManualExtensionEnabled) return Result<SubscriptionDetails>.Fail(SubscriptionErrors.Invalid("TrialExtensionInvalid"));
                var oldEnd = s.EndsAtUtc;
                result = s.ExtendTrial(r.AdditionalDays ?? 0, r.Reason ?? "", now);
                if (result.IsSuccess)
                {
                    lifecycle.History(s, "TrialExtended", s.Status, actor, r.Reason, $"trial-extended:{s.Id}:{s.TrialExtensionCount}", metadata: new { OldEndsAtUtc = oldEnd, NewEndsAtUtc = s.EndsAtUtc });
                    await lifecycle.ScheduleRemindersAsync(s, ct);
                    await lifecycle.NotifyAsync(s, "TrialExtended", $"subscription-trial-extended:{s.Id}:{s.TrialExtensionCount}", ct);
                }
                break;
            case SubscriptionOperation.RequestChange:
                result = await RequestChangeAsync(s, actor.Value, r, ct); break;
            case SubscriptionOperation.ApproveChange:
            case SubscriptionOperation.RejectChange:
                result = await ReviewAsync(s, actor.Value, command, ct); break;
            case SubscriptionOperation.Renew:
                return await RenewAsync(s, actor.Value, command, ct);
            case SubscriptionOperation.Cancel:
                result = s.CancelAtPeriodEnd(now);
                if (result.IsSuccess) lifecycle.History(s, "CancellationAtPeriodEnd", s.Status, actor, r.Reason, "cancel-at-end:" + s.Id);
                break;
            default: result = Result.Fail(SubscriptionErrors.Invalid()); break;
        }
        if (result.IsFailure) return Result<SubscriptionDetails>.Fail(result.Errors);
        if (command.IdempotencyKey is { Length: > 0 } completedKey && await store.PaymentByKeyAsync(completedKey.Trim(), ct) is not null)
            lifecycle.History(s, "PaymentRequest", s.Status, actor, null, "payment-request:" + completedKey.Trim(), metadata: new PaymentRequestMetadata(PaymentFingerprint(command, actor.Value, s.Id)));
        await store.SaveChangesAsync(ct);
        return Result<SubscriptionDetails>.Ok(await DetailsAsync(s, !own, ct));
    }

    private async Task<Result> ConfirmPaymentAsync(DoctorSubscription s, SubscriptionMutationRequest r, string? key,
        Guid actor, SubscriptionPaymentType type, decimal expected, CancellationToken ct)
    {
        var doctor = await doctors.FindDoctorByIdAsync(s.DoctorId, ct);
        if (doctor?.ApprovalStatus != DoctorApprovalStatus.Approved) return Result.Fail(SubscriptionErrors.Invalid("DoctorNotApproved"));
        var enabled = options.CurrentValue.PaymentMethods;
        if (r.PaymentMethod != SubscriptionPaymentMethod.Cash || !enabled.Cash)
            return Result.Fail(SubscriptionErrors.Invalid("PaymentMethodDisabled"));
        if (expected < 0 || expected == 0 && type != SubscriptionPaymentType.UpgradeDifference || r.AmountReceived != expected) return Result.Fail(SubscriptionErrors.Invalid("PaymentAmountMismatch"));
        if (string.IsNullOrWhiteSpace(key) || key.Trim().Length > 150) return Result.Fail(SubscriptionErrors.Invalid("PaymentRequired"));
        if (r.PaidAtUtc is { } paidAt && (paidAt.Kind != DateTimeKind.Utc || paidAt > clock.UtcNow)) return Result.Fail(SubscriptionErrors.Invalid("InvalidSelection"));
        store.Add(new SubscriptionPayment(Guid.NewGuid(), s.Id, type, r.PaymentMethod, expected, s.Currency,
            r.PaidAtUtc ?? clock.UtcNow, actor, r.Reference, r.Notes, key.Trim(), clock.UtcNow));
        return Result.Ok();
    }
    private async Task<Result<SubscriptionDetails>> SelectPaidAsync(Guid doctorId, SubscriptionCommand command, CancellationToken ct)
    {
        await store.AcquireSubscriptionCapacityLockAsync(doctorId, ct);
        var request = command.Request;
        var actor = currentUser.UserId!.Value;
        if (string.IsNullOrWhiteSpace(command.IdempotencyKey)) return Result<SubscriptionDetails>.Fail(SubscriptionErrors.Invalid("IdempotencyKeyRequired"));
        var key = command.IdempotencyKey.Trim();
        var fingerprint = MutationFingerprint(new { Operation = "SelectPaid", DoctorId = doctorId, Actor = actor, request.Selection });
        var receipt = await store.ReceiptAsync(key, ct);
        if (receipt is not null)
            return receipt.ActorId == actor && receipt.Fingerprint == fingerprint
                ? Result<SubscriptionDetails>.Ok(await DetailsAsync((await store.FindAsync(receipt.ResultId, ct))!, false, ct))
                : Result<SubscriptionDetails>.Fail(SubscriptionErrors.Conflict("IdempotencyConflict"));
        var terms = await resolver.ResolveAsync(request.Selection, ct);
        if (terms.IsFailure) return Result<SubscriptionDetails>.Fail(terms.Errors);
        if (terms.Value.Kind != SubscriptionKind.Paid) return Result<SubscriptionDetails>.Fail(SubscriptionErrors.Invalid("TrialRequiresSelfActivation"));
        var selected = lifecycle.Select(doctorId, terms.Value);
        if (selected.IsFailure) return Result<SubscriptionDetails>.Fail(selected.Errors);
        store.Add(new SubscriptionMutationReceipt(key, actor, fingerprint, selected.Value.Id, clock.UtcNow));
        await store.SaveChangesAsync(ct);
        return Result<SubscriptionDetails>.Ok(await DetailsAsync(selected.Value, false, ct));
    }
    private async Task<Result> RequestChangeAsync(DoctorSubscription s, Guid actor, SubscriptionMutationRequest r, CancellationToken ct)
    {
        if (!s.IsValidAt(clock.UtcNow) || s.SubscriptionKind != SubscriptionKind.Paid || r.ChangeType is null) return Result.Fail(SubscriptionErrors.Invalid("ChangeRequestInvalid"));
        var changes = await store.ChangesAsync(s.Id, ct);
        if (changes.Any(c => c.Status is SubscriptionChangeStatus.Pending or SubscriptionChangeStatus.Scheduled)) return Result.Fail(SubscriptionErrors.Conflict("ChangeRequestAlreadyPending"));
        var target = await resolver.ResolveAsync(r.Selection ?? TargetSelection(s, r), ct);
        if (target.IsFailure) return Result.Fail(target.Errors);
        var t = target.Value;
        if (t.Kind != SubscriptionKind.Paid || t.ProductId != s.ProductId) return Result.Fail(SubscriptionErrors.Invalid("ChangeRequestInvalid"));
        if (r.ChangeType == SubscriptionChangeType.Upgrade)
        {
            var plans = (await store.CatalogAsync(ct)).Plans;
            var higherPlan = plans.Single(p => p.Id == t.PlanId).DisplayOrder > plans.Single(p => p.Id == s.PlanId).DisplayOrder;
            var higherBookingCount = t.PlanId == s.PlanId && t.SelectedPracticeCount.HasValue && s.SelectedPracticeCount.HasValue && t.SelectedPracticeCount > s.SelectedPracticeCount;
            if (t.BillingCycle != s.BillingCycle || t.PriceAmount < s.PriceAmount || t.PracticeLimit < s.PracticeLimit || t.ReceptionLimit < s.ReceptionLimit ||
                !higherPlan && !higherBookingCount)
                return Result.Fail(SubscriptionErrors.Invalid("ChangeRequestInvalid"));
        }
        else
        {
            if (r.ChangeType == SubscriptionChangeType.Downgrade && (t.PracticeLimit > s.PracticeLimit || t.ReceptionLimit > s.ReceptionLimit || t.PlanId == s.PlanId && t.SelectedPracticeCount == s.SelectedPracticeCount) ||
                r.ChangeType == SubscriptionChangeType.BillingCycle && (t.PlanId != s.PlanId || t.BillingCycle == s.BillingCycle)) return Result.Fail(SubscriptionErrors.Invalid("ChangeRequestInvalid"));
            var fit = await ValidateRetainedAsync(s, t, r.RetainedAllocations, ct); if (fit.IsFailure) return fit;
        }
        var change = new SubscriptionChangeRequest(s.Id, actor, r.ChangeType.Value, t, Math.Max(0, t.PriceAmount - s.PriceAmount), clock.UtcNow);
        change.SetRetainedAllocations(r.RetainedAllocations ?? (r.ChangeType == SubscriptionChangeType.Upgrade ? null : await SnapshotAllocationsAsync(s.SubscriptionContractId, ct)));
        store.Add(change);
        lifecycle.History(s, "ChangeRequested", s.Status, actor, r.Reason, "change-requested:" + change.Id, metadata: new { change.Id, change.ChangeType });
        return Result.Ok();
    }
    private async Task<Result> ReviewAsync(DoctorSubscription s, Guid actor, SubscriptionCommand command, CancellationToken ct)
    {
        var change = (await store.ChangesAsync(s.Id, ct)).SingleOrDefault(c => c.Id == command.RequestId);
        if (change is null) return Result.Fail(SubscriptionErrors.Invalid("ChangeRequestNotFound"));
        var row = RowVersionCodec.Decode(command.Request.ChangeRowVersion ?? "");
        if (row is null || !change.RowVersion.AsSpan().SequenceEqual(row)) return Result.Fail(SubscriptionErrors.Conflict());
        if (change.Status != SubscriptionChangeStatus.Pending) return Result.Fail(SubscriptionErrors.Invalid("ChangeRequestInvalid"));
        var approve = command.Operation == SubscriptionOperation.ApproveChange;
        if (approve && !s.IsValidAt(clock.UtcNow)) return Result.Fail(SubscriptionErrors.Denied("NotActive"));
        var oldTerms = s.TermsSnapshotJson;
        if (approve && change.ChangeType == SubscriptionChangeType.Upgrade)
        {
            var payment = await ConfirmPaymentAsync(s, command.Request, command.IdempotencyKey, actor, SubscriptionPaymentType.UpgradeDifference, change.DifferenceAmount, ct);
            if (payment.IsFailure) return payment;
            var upgraded = s.Upgrade(change.TargetTerms, clock.UtcNow); if (upgraded.IsFailure) return upgraded;
            store.RemoveFeatures(s.Id);
            await store.SaveChangesAsync(ct);
            lifecycle.AddFeatures(s);
        }
        else if (approve)
        {
            var fit = await ValidateRetainedAsync(s, change.TargetTerms, change.RetainedAllocations, ct); if (fit.IsFailure) return fit;
        }
        var result = change.Review(approve, actor, clock.UtcNow, change.ChangeType == SubscriptionChangeType.Upgrade ? clock.UtcNow : s.EndsAtUtc!.Value, command.Request.Reason);
        if (result.IsSuccess) lifecycle.History(s, approve ? "ChangeApproved" : "ChangeRejected", s.Status, actor, command.Request.Reason,
            "change-reviewed:" + change.Id, oldTerms, new { change.Id, change.EffectiveAtUtc });
        if (result.IsSuccess && approve && change.ChangeType == SubscriptionChangeType.Upgrade)
            await lifecycle.NotifyAsync(s, "Activated", $"subscription-upgrade:{s.Id}:{change.Id}", ct);
        return result;
    }
    private async Task<Result<SubscriptionDetails>> RenewAsync(DoctorSubscription old, Guid actor, SubscriptionCommand command, CancellationToken ct)
    {
        await store.AcquireReminderLockAsync(old.SubscriptionContractId, ct);
        var periods = (await store.PeriodsAsync(old.DoctorId, ct)).Where(p => p.SubscriptionContractId == old.SubscriptionContractId).ToArray();
        if (old.SubscriptionKind != SubscriptionKind.Paid || old.Status is not (SubscriptionStatus.Active or SubscriptionStatus.Expired) ||
            periods.Any(p => p.Status is SubscriptionStatus.Scheduled or SubscriptionStatus.Pending || p.StartsAtUtc > old.StartsAtUtc))
            return Result<SubscriptionDetails>.Fail(SubscriptionErrors.Invalid());
        var scheduled = (await store.ChangesAsync(old.Id, ct)).SingleOrDefault(c => c.Status == SubscriptionChangeStatus.Scheduled || c.Status == SubscriptionChangeStatus.Pending && c.ChangeType == SubscriptionChangeType.Renewal);
        var selection = scheduled is not null
            ? new SubscriptionSelection(old.Terms.ProductCode, scheduled.TargetPlanCode!, scheduled.TargetBillingCycle, null, scheduled.TargetPracticeCount)
            : command.Request.Selection ?? TargetSelection(old, command.Request);
        var terms = scheduled is null ? await resolver.ResolveAsync(selection, ct) : Result<SubscriptionTerms>.Ok(scheduled.TargetTerms);
        if (terms.IsFailure) return Result<SubscriptionDetails>.Fail(terms.Errors);
        if (terms.Value.Kind != SubscriptionKind.Paid || terms.Value.ProductId != old.ProductId) return Result<SubscriptionDetails>.Fail(SubscriptionErrors.Invalid("ChangeRequestInvalid"));
        var retained = command.Request.RetainedAllocations ?? scheduled?.RetainedAllocations;
        if (command.Request.RetainedAllocations is not null && (scheduled?.RetainedAllocations is null ||
            MutationFingerprint(command.Request.RetainedAllocations) != MutationFingerprint(scheduled.RetainedAllocations)))
            return Result<SubscriptionDetails>.Fail(SubscriptionErrors.Conflict("AllocationConflict"));
        if (scheduled is null && (terms.Value.PracticeLimit < old.PracticeLimit || terms.Value.ReceptionLimit < old.ReceptionLimit || terms.Value.BillingCycle != old.BillingCycle))
            return Result<SubscriptionDetails>.Fail(SubscriptionErrors.Conflict("RenewalSelectionRequired"));
        var fit = await ValidateRetainedAsync(old, terms.Value, retained, ct);
        if (fit.IsFailure) return Result<SubscriptionDetails>.Fail(fit.Errors);
        var next = lifecycle.Select(old.DoctorId, terms.Value, old.SubscriptionContractId);
        if (next.IsFailure) return Result<SubscriptionDetails>.Fail(next.Errors);
        var payment = await ConfirmPaymentAsync(next.Value, command.Request, command.IdempotencyKey, actor, SubscriptionPaymentType.Renewal, terms.Value.PriceAmount, ct);
        if (payment.IsFailure) return Result<SubscriptionDetails>.Fail(payment.Errors);
        var start = old.EndsAtUtc > clock.UtcNow ? old.EndsAtUtc.Value : clock.UtcNow;
        var end = terms.Value.BillingCycle == BillingCycle.Annual ? start.AddYears(1) : start.AddMonths(1);
        if (!SubscriptionContract.PeriodFits(old.SubscriptionContractId, start, end, periods)) return Result<SubscriptionDetails>.Fail(SubscriptionErrors.Conflict("PeriodOverlap"));
        if (start > clock.UtcNow) next.Value.SchedulePaidPeriod(start, actor, clock.UtcNow);
        else
        {
            old.Expire(clock.UtcNow);
            await store.SaveChangesAsync(ct);
            next.Value.Activate(start, actor);
        }
        var selections = retained ?? await SnapshotAllocationsAsync(old.SubscriptionContractId, ct);
        store.Add(new SubscriptionAllocationSelection(next.Value.Id, selections, actor, clock.UtcNow));
        await lifecycle.ScheduleRemindersAsync(next.Value, ct);
        await lifecycle.SuppressPaidRemindersAsync(old, ct);
        if (scheduled?.Status == SubscriptionChangeStatus.Pending) scheduled.Review(true, actor, clock.UtcNow, start, null);
        scheduled?.Apply();
        lifecycle.History(old, "RenewalRecorded", old.Status, actor, null, "renewal:" + command.IdempotencyKey!.Trim(), metadata: new { NextSubscriptionId = next.Value.Id });
        lifecycle.History(old, "PaymentRequest", old.Status, actor, null, "payment-request:" + command.IdempotencyKey.Trim(), metadata: new PaymentRequestMetadata(PaymentFingerprint(command, actor, old.Id)));
        lifecycle.History(next.Value, "RenewalPaid", SubscriptionStatus.Pending, actor, null, "renewal-paid:" + next.Value.Id);
        await lifecycle.NotifyAsync(next.Value, "Activated", "subscription-activated:" + next.Value.Id, ct);
        await store.SaveChangesAsync(ct);
        return Result<SubscriptionDetails>.Ok(await DetailsAsync(next.Value, true, ct));
    }
    private sealed record PaymentRequestMetadata(string Fingerprint);
    private async Task<IReadOnlyList<RetainedPracticeAllocation>> SnapshotAllocationsAsync(Guid contractId, CancellationToken ct)
    {
        var result = new List<RetainedPracticeAllocation>();
        var active = (await store.ActiveReceptionIdsAsync(contractId, null, ct)).ToHashSet();
        foreach (var allocation in await store.AllocationsAsync(contractId, ct))
            result.Add(new(allocation.DoctorPracticeId, allocation.Id, (await store.AssignmentsAsync(allocation.DoctorPracticeId, ct))
                .Where(a => a.IsActive && active.Contains(a.ReceptionId)).Select(a => a.ReceptionId).ToArray()));
        return result;
    }
    private static string PaymentFingerprint(SubscriptionCommand command, Guid actor, Guid subscriptionId)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            ActorId = actor, SubscriptionId = subscriptionId, command.Operation, command.RequestId,
            command.Request.PaymentMethod, command.Request.AmountReceived, command.Request.PaidAtUtc,
            command.Request.Reference, command.Request.Notes, command.Request.Selection,
            command.Request.TargetPlanCode, command.Request.TargetPracticeCount, command.Request.TargetBillingCycle, command.Request.RetainedAllocations
        }))));
    private static SubscriptionSelection TargetSelection(DoctorSubscription current, SubscriptionMutationRequest request) =>
        new(current.Terms.ProductCode, request.TargetPlanCode ?? current.Terms.PlanCode,
            request.TargetBillingCycle ?? current.BillingCycle, null, request.TargetPracticeCount ?? current.SelectedPracticeCount);
}
