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
}
public sealed class SubscriptionPermissionGuard(ISubscriptionDataStore store, IWaslaDataStore doctors,
    ISubscriptionEntitlementService entitlements,
    ISubscriptionOperationalContinuityGuard continuity) : ISubscriptionPermissionGuard
{
    public async Task<bool> AllowedForUserAsync(Guid userId, string permission, CancellationToken ct, SubscriptionResource? resource = null)
    {
        var user = await doctors.GetAccessSnapshotAsync(userId, ct);
        return user is { User.IsActive: true, User.IsFirstLogin: false } && await AllowedAsync(user, permission, ct, resource);
    }
    public async Task<bool> AllowedAsync(UserAccessSnapshot user, string permission, CancellationToken ct, SubscriptionResource? resource = null)
    {
        if (user.User.UserType is not (UserType.Doctor or UserType.Reception)) return true;
        Guid? doctorId = user.DoctorId;
        if (user.User.UserType == UserType.Reception)
            doctorId = (await doctors.FindReceptionByApplicationUserIdAsync(user.User.Id, ct))?.OwnerDoctorId;
        if (doctorId is null) return false;
        // Profile, onboarding and subscription account operations remain available without operational access.
        if (SubscriptionPermissionNames.DoctorDefaults.Contains(permission) ||
            Wasla.Domain.Security.PermissionNames.PendingDoctorOnboarding.Contains(permission) || permission.StartsWith("DoctorProfile.", StringComparison.Ordinal)) return true;
        var mapping = await store.PermissionMappingAsync(permission, ct);
        if (mapping is null) return CommercialFeatures.FeatureForPermission(permission) is null; // Missing expected seed fails closed.
        var feature = (await store.CatalogAsync(ct)).Features.SingleOrDefault(f => f.Id == mapping.FeatureId);
        if (feature is null) return false;
        var access = await entitlements.EvaluateAsync(doctorId.Value, feature.Code, mapping.RequiredAccessLevel, ct);
        if (access.IsSuccess) return true;
        if (resource is not null && mapping.RequiredAccessLevel == FeatureAccessLevel.Full &&
            (await continuity.EvaluateAsync(doctorId.Value, feature.Code, resource, ct)).IsSuccess) return true;
        return false;
    }
}
