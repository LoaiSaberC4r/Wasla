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
    Task<Result<SubscriptionEntitlementResult>> EvaluateForPracticeAsync(Guid doctorId, Guid practiceId, string featureCode, FeatureAccessLevel requiredAccess, CancellationToken ct)
        => EvaluateAsync(doctorId, featureCode, requiredAccess, ct);
    Task<Result<DoctorSubscription>> ActiveAsync(Guid doctorId, CancellationToken ct);
    Task<Result> ValidateCapacityAsync(Guid doctorId, bool reception, CancellationToken ct);
    Task<Result> ValidateOnboardingPracticeCapacityAsync(Guid doctorId, CancellationToken ct);
}
public sealed class SubscriptionEntitlementService(ISubscriptionDataStore store, IWaslaDataStore doctors, IDateTimeProvider clock) : ISubscriptionEntitlementService
{
    private async Task<bool> DoctorApprovedAsync(Guid doctorId, CancellationToken ct)
    {
        var doctor = await doctors.FindDoctorByIdAsync(doctorId, ct);
        var user = doctor is null ? null : await doctors.FindUserByIdAsync(doctor.ApplicationUserId, ct);
        return doctor?.ApprovalStatus == DoctorApprovalStatus.Approved && user is { IsActive: true };
    }
    public async Task<Result<DoctorSubscription>> ActiveAsync(Guid doctorId, CancellationToken ct)
    {
        if (!await DoctorApprovedAsync(doctorId, ct)) return Result<DoctorSubscription>.Fail(SubscriptionErrors.Denied("DoctorNotApproved"));
        var periods = (await store.PeriodsAsync(doctorId, ct)).Where(p => p.IsValidAt(clock.UtcNow)).ToArray();
        return periods.Length == 1 ? Result<DoctorSubscription>.Ok(periods[0]) : Result<DoctorSubscription>.Fail(
            periods.Length > 1 ? SubscriptionErrors.Conflict("MultipleContractsRequireSelection") : SubscriptionErrors.Denied("NotActive"));
    }
    public async Task<Result<SubscriptionEntitlementResult>> EvaluateAsync(Guid doctorId, string featureCode, FeatureAccessLevel requiredAccess, CancellationToken cancellationToken)
    {
        var active = await ActiveAsync(doctorId, cancellationToken);
        return active.IsFailure ? Result<SubscriptionEntitlementResult>.Fail(active.Errors) : await FeatureAsync(active.Value, featureCode, requiredAccess, cancellationToken);
    }
    public async Task<Result<SubscriptionEntitlementResult>> EvaluateForPracticeAsync(Guid doctorId, Guid practiceId, string featureCode, FeatureAccessLevel requiredAccess, CancellationToken ct)
    {
        if (!await DoctorApprovedAsync(doctorId, ct)) return Result<SubscriptionEntitlementResult>.Fail(SubscriptionErrors.Denied("DoctorNotApproved"));
        var practice = await doctors.FindDoctorPracticeAsync(practiceId, ct);
        if (practice?.DoctorId != doctorId) return Result<SubscriptionEntitlementResult>.Fail(SubscriptionErrors.NotFound);
        var period = await store.EffectivePracticePeriodAsync(doctorId, practiceId, clock.UtcNow, ct);
        return period is null ? Result<SubscriptionEntitlementResult>.Fail(SubscriptionErrors.Denied("PracticeCommerciallyInactive")) : await FeatureAsync(period, featureCode, requiredAccess, ct);
    }
    private async Task<Result<SubscriptionEntitlementResult>> FeatureAsync(DoctorSubscription period, string featureCode, FeatureAccessLevel access, CancellationToken ct)
    {
        var feature = (await store.FeaturesAsync(period.Id, ct)).SingleOrDefault(f => f.FeatureCodeSnapshot == featureCode);
        return feature?.AccessLevel >= access ? Result<SubscriptionEntitlementResult>.Ok(new(period.Id, period.Terms.ProductCode, period.Terms.PlanCode, featureCode, feature.AccessLevel)) :
            Result<SubscriptionEntitlementResult>.Fail(SubscriptionErrors.Denied(feature is null ? "FeatureNotEntitled" : "AccessLevelInsufficient"));
    }
    public async Task<Result> ValidateCapacityAsync(Guid doctorId, bool reception, CancellationToken ct)
    {
        await store.AcquireSubscriptionCapacityLockAsync(doctorId, ct);
        var active = await ActiveAsync(doctorId, ct);
        if (active.IsFailure) return Result.Fail(active.Errors);
        var usage = await store.ContractUsageAsync(active.Value.SubscriptionContractId, ct);
        var count = reception ? usage.ReceptionCount : usage.PracticeCount;
        var limit = reception ? active.Value.ReceptionLimit : active.Value.PracticeLimit;
        return count >= limit ? Result.Fail(SubscriptionErrors.Capacity(reception, count, limit)) : Result.Ok();
    }
    public async Task<Result> ValidateOnboardingPracticeCapacityAsync(Guid doctorId, CancellationToken ct)
    {
        await store.AcquireSubscriptionCapacityLockAsync(doctorId, ct);
        var doctor = await doctors.FindDoctorByIdAsync(doctorId, ct);
        var usage = await store.UsageAsync(doctorId, ct);
        return doctor?.ApprovalStatus == DoctorApprovalStatus.Pending && usage.PracticeCount == 0
            ? Result.Ok() : Result.Fail(SubscriptionErrors.Denied("ContractSelectionRequired"));
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
        var reception = user.User.UserType == UserType.Reception ? await doctors.FindReceptionByApplicationUserIdAsync(user.User.Id, ct) : null;
        var doctorId = user.User.UserType == UserType.Reception ? reception?.OwnerDoctorId : user.DoctorId;
        if (doctorId is null)
        { foreach (var permission in commercial) decisions[permission] = new(false, "Subscription.DoctorNotApproved"); return decisions; }
        if (commercial.Any(p => CommercialFeatures.RequiredAccess(p) == FeatureAccessLevel.Full))
            await store.AcquireCommercialReadLockAsync(doctorId.Value, ct);
        var doctor = await doctors.FindDoctorByIdAsync(doctorId.Value, ct);
        var account = doctor is null ? null : await doctors.FindUserByIdAsync(doctor.ApplicationUserId, ct);
        if (doctor?.ApprovalStatus != DoctorApprovalStatus.Approved || account is not { IsActive: true })
        { foreach (var permission in commercial) decisions[permission] = new(false, "Subscription.DoctorNotApproved"); return decisions; }
        var commitment = resource is null ? null : await store.CommitmentAsync(resource, ct);
        var practiceId = commitment?.DoctorId == doctorId ? commitment.PracticeId : null;
        var delegated = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (practiceId.HasValue && user.User.UserType == UserType.Reception)
        {
            var practice = await doctors.FindDoctorPracticeAsync(practiceId.Value, ct);
            var actorAccount = await doctors.FindUserByIdAsync(user.User.Id, ct);
            if (reception is null || practice is not { IsActive: true } || actorAccount is not { IsActive: true, IsFirstLogin: false } || !await store.ReceptionRetainedAsync(practiceId.Value, reception.Id, clock.UtcNow, ct))
            { foreach (var permission in commercial) decisions[permission] = new(false, "Subscription.PermissionDenied"); return decisions; }
            var assignment = (await store.AssignmentsAsync(practiceId.Value, ct)).SingleOrDefault(a => a.ReceptionId == reception.Id && a.IsActive);
            if (assignment is not null)
            {
                var links = await doctors.ListReceptionAssignmentPermissionsAsync(assignment.Id, ct);
                foreach (var permission in await doctors.ListPermissionsByIdsAsync(links.Select(x => x.PermissionId).ToArray(), ct)) delegated.Add(permission.Name);
            }
        }
        var rules = (await store.PermissionRulesAsync(commercial, ct)).ToDictionary(r => r.Permission, StringComparer.Ordinal);
        var windows = await store.ReadWindowsAsync(doctorId.Value, clock.UtcNow, ct);
        var currentPracticePeriod = practiceId.HasValue ? await store.EffectivePracticePeriodAsync(doctorId.Value, practiceId.Value, clock.UtcNow, ct) : null;
        var currentFeatures = currentPracticePeriod is null ? Array.Empty<SubscriptionFeatureSnapshot>() : (await store.FeaturesAsync(currentPracticePeriod.Id, ct)).ToArray();
        foreach (var permission in commercial)
        {
            var classification = CommercialPermissionCatalog.All[permission];
            if (!rules.TryGetValue(permission, out var rule) || rule.FeatureCode != classification.FeatureCode || rule.RequiredAccess != classification.RequiredAccess)
            { decisions[permission] = new(false, "Subscription.FeatureNotEntitled"); continue; }
            var featureWindows = windows.Where(w => w.FeatureCode == rule.FeatureCode && (!practiceId.HasValue || w.PracticeId == practiceId)).ToArray();
            var scope = new SubscriptionReadScope(false, featureWindows, featureWindows.Where(w => w.Current && w.PracticeId.HasValue).Select(w => w.PracticeId!.Value).Distinct().ToArray());
            var allowed = false;
            var blockedReason = "Subscription.FeatureNotEntitled";
            if (resource?.Kind == SubscriptionResourceKind.AuthorizationPreflight)
            {
                var createsUnassignedResource = permission is "DoctorPractices.ViewOwn" or "DoctorPractices.ManageOwn" or "ReceptionUsers.ManageOwn" or "ReceptionUsers.ViewOwn" or "ReceptionUsers.ManageAccountStateOwn";
                allowed = createsUnassignedResource || featureWindows.Any(w => w.AccessLevel >= rule.RequiredAccess &&
                    (w.Current || rule.RequiredAccess == FeatureAccessLevel.ReadOnly && HistoricalCollectionReadAllowed(rule.FeatureCode, permission)));
                decisions[permission] = new(allowed, allowed ? null : blockedReason, scope);
                continue;
            }
            if (permission == "DoctorPractices.ViewOwn" && user.User.UserType == UserType.Doctor && (resource is null || practiceId.HasValue)) { decisions[permission] = new(true); continue; }
            if (resource is null && permission is "ReceptionUsers.ManageOwn" or "ReceptionUsers.ViewOwn" or "ReceptionUsers.ManageAccountStateOwn")
            { decisions[permission] = new(true); continue; }
            if (practiceId.HasValue)
            {
                var feature = currentFeatures.SingleOrDefault(f => f.FeatureCodeSnapshot == rule.FeatureCode);
                allowed = feature?.AccessLevel >= rule.RequiredAccess;
                blockedReason = currentPracticePeriod is null ? "Subscription.PracticeCommerciallyInactive" : feature is null ? "Subscription.FeatureNotEntitled" : "Subscription.AccessLevelInsufficient";
                if (user.User.UserType == UserType.Reception) allowed &= delegated.Contains(permission);
                // A linked reservation's original operational grant is immutable, including after a transfer to Full.
                var originBound = commitment?.ReservationId is not null && rule.RequiredAccess == FeatureAccessLevel.Full &&
                    rule.FeatureCode is "CHECKIN_WALKIN" or "TICKET_QUEUE" or "MEDICAL_ENCOUNTER" or "DIAGNOSIS" or "PRESCRIPTION" or "LAB_RADIOLOGY" or "FOLLOWUP_ELIGIBILITY" or "PAYMENT_COLLECTION";
                if (originBound || !allowed && rule.RequiredAccess == FeatureAccessLevel.Full && (commitment?.ReservationId is not null || commitment?.TicketId is not null))
                {
                    allowed = resource is not null && (await continuity.EvaluateAsync(doctorId.Value, rule.FeatureCode, resource, ct)).IsSuccess;
                    if (allowed && user.User.UserType == UserType.Reception)
                        allowed = delegated.Contains(permission);
                }
                if (!allowed && rule.RequiredAccess == FeatureAccessLevel.ReadOnly && commitment is not null)
                    allowed = resource?.Kind == SubscriptionResourceKind.Practice
                        ? (currentPracticePeriod is null || CommercialFeatures.IsArchivePermission(permission)) && HistoricalCollectionReadAllowed(rule.FeatureCode, permission) && scope.HasAccess
                        : scope.Covers(commitment.CreatedOnUtc, practiceId);
            }
            else if (resource is null && rule.RequiredAccess == FeatureAccessLevel.ReadOnly)
                allowed = featureWindows.Any(w => w.Current) || HistoricalCollectionReadAllowed(rule.FeatureCode, permission) && scope.HasAccess;
            else if (resource is null && rule.FeatureCode is "PDF_XLSX_EXPORT" or "OPERATING_EXPENSES" or "SALARY_MANAGEMENT")
                allowed = featureWindows.Any(w => w.Current && w.AccessLevel == FeatureAccessLevel.Full);
            else if (resource is null && permission is "FinancialExpenses.ManageOwn" or "FinancialExpenses.SettleOwn" or "FinancialExpenses.ReverseOwn" or
                "DrugCatalogRequests.CreateOwn" or "DrugCatalogRequests.UpdateOwn" or "LabCatalogRequests.CreateOwn" or "LabCatalogRequests.UpdateOwn" or
                "RadiologyCatalogRequests.CreateOwn" or "RadiologyCatalogRequests.UpdateOwn")
                allowed = featureWindows.Any(w => w.Current && w.AccessLevel == FeatureAccessLevel.Full);
            decisions[permission] = new(allowed, allowed ? null : blockedReason, scope);
            if (!allowed) CommercialSubscriptionDiagnostics.Denied(rule.FeatureCode, blockedReason);
        }
        return decisions;
    }
    private static bool HistoricalCollectionReadAllowed(string feature, string permission) => CommercialFeatures.IsArchivePermission(permission) ||
        feature is "RESERVATION_MANAGEMENT" or "TICKET_QUEUE" or "PAYMENT_COLLECTION" or "MEDICAL_ENCOUNTER" or "DIAGNOSIS" or "PRESCRIPTION" or "LAB_RADIOLOGY" or "MEDICAL_RECORD_ACCESS";

}
