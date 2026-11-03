using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Subscriptions;

namespace Wasla.Application.Features.Subscriptions;

public sealed record SubscriptionEntitlementResult(Guid SubscriptionId, string ProductCode, string PlanCode, string FeatureCode, FeatureAccessLevel AccessLevel);
public interface ISubscriptionEntitlementService
{
    Task<Result<SubscriptionEntitlementResult>> EvaluateAsync(Guid doctorId, string featureCode, FeatureAccessLevel requiredAccess, CancellationToken cancellationToken);
    Task<Result<DoctorSubscription>> ActiveAsync(Guid doctorId, CancellationToken ct);
    Task<Result> ValidateCapacityAsync(Guid doctorId, bool reception, CancellationToken ct);
    Task<Result> ValidateOnboardingPracticeCapacityAsync(Guid doctorId, CancellationToken ct);
}
public sealed class SubscriptionEntitlementService(ISubscriptionDataStore store, IWaslaDataStore doctors, IDateTimeProvider clock) : ISubscriptionEntitlementService
{
    public async Task<Result<DoctorSubscription>> ActiveAsync(Guid doctorId, CancellationToken ct)
    {
        var doctor = await doctors.FindDoctorByIdAsync(doctorId, ct);
        var user = doctor is null ? null : await doctors.FindUserByIdAsync(doctor.ApplicationUserId, ct);
        if (doctor?.ApprovalStatus != DoctorApprovalStatus.Approved || user is not { IsActive: true })
            return Result<DoctorSubscription>.Fail(SubscriptionErrors.Denied("DoctorNotApproved"));
        var subscription = await store.CurrentAsync(doctorId, ct);
        if (subscription?.IsValidAt(clock.UtcNow) != true)
        {
            var scheduled = await store.ScheduledAsync(doctorId, ct);
            if (scheduled?.IsValidAt(clock.UtcNow) == true) subscription = scheduled;
        }
        if (subscription is null || !subscription.IsValidAt(clock.UtcNow))
            return Result<DoctorSubscription>.Fail(SubscriptionErrors.Denied(subscription is not null ? "Expired" :
                await store.PendingAsync(doctorId, ct) is not null ? "Pending" : "NotActive"));
        return Result<DoctorSubscription>.Ok(subscription);
    }
    public async Task<Result<SubscriptionEntitlementResult>> EvaluateAsync(Guid doctorId, string featureCode, FeatureAccessLevel requiredAccess, CancellationToken cancellationToken)
    {
        var active = await ActiveAsync(doctorId, cancellationToken);
        var historical = active.IsFailure;
        DoctorSubscription? subscription;
        if (historical)
        {
            if (requiredAccess != FeatureAccessLevel.ReadOnly || active.Errors.Any(e => e.Code == "Subscription.DoctorNotApproved"))
                return Result<SubscriptionEntitlementResult>.Fail(active.Errors);
            subscription = await store.LatestEndedAsync(doctorId, cancellationToken);
            if (subscription is null || subscription.EndsAtUtc > clock.UtcNow) return Result<SubscriptionEntitlementResult>.Fail(active.Errors);
        }
        else subscription = active.Value;
        var feature = (await store.FeaturesAsync(subscription.Id, cancellationToken)).SingleOrDefault(f => f.FeatureCodeSnapshot == featureCode);
        if (feature is null || feature.AccessLevel < requiredAccess) return Result<SubscriptionEntitlementResult>.Fail(SubscriptionErrors.Denied(feature is null ? "FeatureNotEntitled" : "AccessLevelInsufficient"));
        return Result<SubscriptionEntitlementResult>.Ok(new(subscription.Id, subscription.Terms.ProductCode, subscription.Terms.PlanCode, featureCode, historical ? FeatureAccessLevel.ReadOnly : feature.AccessLevel));
    }
    public async Task<Result> ValidateCapacityAsync(Guid doctorId, bool reception, CancellationToken ct)
    {
        await store.AcquireSubscriptionCapacityLockAsync(doctorId, ct);
        var access = await EvaluateAsync(doctorId, reception ? "RECEPTION_MANAGEMENT" : "PRACTICE_MANAGEMENT", FeatureAccessLevel.Full, ct);
        if (access.IsFailure) return Result.Fail(access.Errors);
        var subscription = (await ActiveAsync(doctorId, ct)).Value;
        var usage = await store.UsageAsync(doctorId, ct);
        var count = reception ? usage.ReceptionCount : usage.PracticeCount;
        var limit = reception ? subscription.ReceptionLimit : subscription.PracticeLimit;
        return count >= limit ? Result.Fail(SubscriptionErrors.Capacity(reception, count, limit)) : Result.Ok();
    }
    public async Task<Result> ValidateOnboardingPracticeCapacityAsync(Guid doctorId, CancellationToken ct)
    {
        await store.AcquireSubscriptionCapacityLockAsync(doctorId, ct);
        if (await store.CurrentAsync(doctorId, ct) is not null) return await ValidateCapacityAsync(doctorId, false, ct);
        // The retired onboarding contract creates one inactive draft only. It consumes the selected capacity before approval.
        var pending = await store.PendingAsync(doctorId, ct);
        if (pending is null) return Result.Fail(SubscriptionErrors.Denied("Pending"));
        if (!(await store.FeaturesAsync(pending.Id, ct)).Any(f => f.FeatureCodeSnapshot == "PRACTICE_MANAGEMENT" && f.AccessLevel == FeatureAccessLevel.Full)) return Result.Fail(SubscriptionErrors.Denied());
        var usage = await store.UsageAsync(doctorId, ct);
        return usage.PracticeCount >= pending.PracticeLimit ? Result.Fail(SubscriptionErrors.Capacity(false, usage.PracticeCount, pending.PracticeLimit)) : Result.Ok();
    }
}

public interface ISubscriptionPermissionGuard
{
    Task<bool> AllowedForUserAsync(Guid userId, string permission, CancellationToken ct, SubscriptionResource? resource = null);
    Task<bool> AllowedAsync(UserAccessSnapshot user, string permission, CancellationToken ct, SubscriptionResource? resource = null);
    Task<IReadOnlyDictionary<string, SubscriptionPermissionDecision>> EvaluateManyAsync(UserAccessSnapshot user,
        IReadOnlyCollection<string> permissions, CancellationToken ct, SubscriptionResource? resource = null);
}
public sealed class SubscriptionPermissionGuard(ISubscriptionDataStore store, IWaslaDataStore doctors,
    ISubscriptionEntitlementService entitlements,
    ISubscriptionOperationalContinuityGuard continuity, IDateTimeProvider clock) : ISubscriptionPermissionGuard
{
    public async Task<bool> AllowedForUserAsync(Guid userId, string permission, CancellationToken ct, SubscriptionResource? resource = null)
    {
        var user = await doctors.GetAccessSnapshotAsync(userId, ct);
        return user is { User.IsActive: true, User.IsFirstLogin: false } && await AllowedAsync(user, permission, ct, resource);
    }
    public async Task<bool> AllowedAsync(UserAccessSnapshot user, string permission, CancellationToken ct, SubscriptionResource? resource = null)
    {
        return (await EvaluateManyAsync(user, [permission], ct, resource))[permission].Allowed;
    }

    public async Task<IReadOnlyDictionary<string, SubscriptionPermissionDecision>> EvaluateManyAsync(UserAccessSnapshot user,
        IReadOnlyCollection<string> permissions, CancellationToken ct, SubscriptionResource? resource = null)
    {
        var decisions = new Dictionary<string, SubscriptionPermissionDecision>(StringComparer.Ordinal);
        var commercial = new List<string>();
        foreach (var permission in permissions.Distinct(StringComparer.Ordinal))
        {
            // Patient rights, catalog managers and platform governance retain their own authorization policies.
            if (user.User.UserType is not (UserType.Doctor or UserType.Reception)) { decisions[permission] = new(true); continue; }
            if (!user.User.IsActive || user.User.IsFirstLogin || !user.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase))
            { decisions[permission] = new(false, "Subscription.PermissionDenied"); continue; }
            if (!CommercialPermissionCatalog.All.TryGetValue(permission, out var classification))
            { decisions[permission] = new(false, "Subscription.FeatureNotEntitled"); continue; }
            if (classification.Category == SubscriptionPermissionCategory.CommercialFeature) commercial.Add(permission);
            else decisions[permission] = classification.Category is SubscriptionPermissionCategory.SuperAdminGovernance or SubscriptionPermissionCategory.UniversalPatient
                ? new(false, "Subscription.PermissionDenied") : new(true);
        }
        if (commercial.Count == 0) return decisions;
        var doctorId = user.User.UserType == UserType.Reception
            ? (await doctors.FindReceptionByApplicationUserIdAsync(user.User.Id, ct))?.OwnerDoctorId : user.DoctorId;
        if (doctorId is null)
        { foreach (var permission in commercial) decisions[permission] = new(false, "Subscription.DoctorNotApproved"); return decisions; }
        var active = await entitlements.ActiveAsync(doctorId.Value, ct);
        if (active.IsFailure && active.Errors.Any(e => e.Code == "Subscription.DoctorNotApproved"))
        { foreach (var permission in commercial) decisions[permission] = new(false, "Subscription.DoctorNotApproved"); return decisions; }
        var rules = (await store.PermissionRulesAsync(commercial, ct)).ToDictionary(r => r.Permission, StringComparer.Ordinal);
        var features = active.IsSuccess ? await store.FeaturesAsync(active.Value.Id, ct) : [];
        var readWindows = active.IsFailure || commercial.Any(p => CommercialFeatures.RequiredAccess(p) == FeatureAccessLevel.ReadOnly)
            ? await store.ReadWindowsAsync(doctorId.Value, clock.UtcNow, ct) : [];
        var latest = active.IsFailure ? await store.LatestEndedAsync(doctorId.Value, ct) : null;
        var latestFeatures = latest is not null && latest.EndsAtUtc <= clock.UtcNow &&
            latest.Status is SubscriptionStatus.Active or SubscriptionStatus.Expired or SubscriptionStatus.Superseded
            ? await store.FeaturesAsync(latest.Id, ct) : [];
        SubscriptionCommitment? commitment = null;
        var commitmentLoaded = false;
        var evaluated = new Dictionary<(string Feature, FeatureAccessLevel Access, bool Archive), SubscriptionPermissionDecision>();
        foreach (var permission in commercial)
        {
            var classification = CommercialPermissionCatalog.All[permission];
            if (!rules.TryGetValue(permission, out var rule) || rule.FeatureCode != classification.FeatureCode || rule.RequiredAccess != classification.RequiredAccess)
            { decisions[permission] = new(false, "Subscription.FeatureNotEntitled"); continue; }
            var key = (rule.FeatureCode, rule.RequiredAccess, CommercialFeatures.IsArchivePermission(permission));
            if (evaluated.TryGetValue(key, out var cached)) { decisions[permission] = cached; continue; }
            var feature = features.SingleOrDefault(f => f.FeatureCodeSnapshot == rule.FeatureCode);
            var scope = new SubscriptionReadScope(feature?.AccessLevel >= FeatureAccessLevel.ReadOnly,
                readWindows.Where(w => w.FeatureCode == rule.FeatureCode).ToArray());
            SubscriptionPermissionDecision decision;
            if (rule.RequiredAccess == FeatureAccessLevel.ReadOnly)
            {
                var allowed = scope.CurrentEntitlement;
                if (!allowed && resource is not null)
                {
                    if (!commitmentLoaded) { commitment = await store.ReadCommitmentAsync(resource, ct); commitmentLoaded = true; }
                    allowed = commitment?.DoctorId == doctorId && scope.Covers(commitment.CreatedOnUtc);
                }
                else if (!allowed && key.Item3) allowed = scope.HasAccess;
                else if (!allowed && active.IsFailure)
                    allowed = latestFeatures.Any(f => f.FeatureCodeSnapshot == rule.FeatureCode && f.AccessLevel >= FeatureAccessLevel.ReadOnly);
                decision = new(allowed, allowed ? null : "Subscription.FeatureNotEntitled", scope);
            }
            else
            {
                var allowed = feature?.AccessLevel == FeatureAccessLevel.Full;
                if (!allowed && resource is not null && active.IsFailure)
                    allowed = (await continuity.EvaluateAsync(doctorId.Value, rule.FeatureCode, resource, ct)).IsSuccess;
                decision = new(allowed, allowed ? null : feature is not null ? "Subscription.AccessLevelInsufficient" :
                    active.IsFailure ? active.Errors[0].Code : "Subscription.FeatureNotEntitled", scope);
            }
            evaluated[key] = decision;
            decisions[permission] = decision;
        }
        return decisions;
    }
}
