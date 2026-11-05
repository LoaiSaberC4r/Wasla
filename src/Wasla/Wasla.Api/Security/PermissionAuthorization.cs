using BuildingBlock.Application.Abstraction.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Security;

namespace Wasla.Api.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class PermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "Permission:";

    public PermissionAttribute(string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        Policy = PolicyPrefix + permission;
    }
}

public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;

internal sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(PermissionAttribute.PolicyPrefix, StringComparison.Ordinal))
        {
            return await base.GetPolicyAsync(policyName);
        }

        var permission = policyName[PermissionAttribute.PolicyPrefix.Length..];
        return new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(permission))
            .Build();
    }
}

internal sealed class PermissionAuthorizationHandler(
    ICurrentUser currentUser,
    IWaslaDataStore dataStore,
    Wasla.Application.Features.Subscriptions.ISubscriptionPermissionGuard subscriptionGuard,
    Wasla.Application.Features.Notifications.INotificationAuthorizationAudit notificationAudit)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
        {
            return;
        }

        var snapshot = await dataStore.GetAccessSnapshotAsync(userId, CancellationToken.None);
        if (snapshot is null || !snapshot.User.IsActive || snapshot.User.IsFirstLogin)
        {
            if (requirement.Permission == PermissionNames.NotificationsDeliveriesRetry) await notificationAudit.RetryDeniedAsync(userId, CancellationToken.None);
            return;
        }

        IEnumerable<string> effective = snapshot.Permissions;
        if (requirement.Permission == PermissionNames.NotificationsDeliveriesRetry &&
            (snapshot.User.UserType != UserType.SuperAdmin || snapshot.SuperAdminId is null))
        {
            await notificationAudit.RetryDeniedAsync(userId, CancellationToken.None);
            return;
        }
        if (snapshot.User.UserType == UserType.Doctor &&
            snapshot.DoctorStatus == DoctorApprovalStatus.Pending)
        {
            effective = effective.Where(PermissionNames.PendingDoctorOnboarding.Contains);
        }
        else if (snapshot.User.UserType == UserType.Doctor &&
                 snapshot.DoctorStatus != DoctorApprovalStatus.Approved)
        {
            effective = effective.Where(permission => string.Equals(permission, PermissionNames.DoctorOnboardingViewOwn, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(permission, Wasla.Domain.Subscriptions.SubscriptionPermissionNames.ViewOwn, StringComparison.OrdinalIgnoreCase));
        }

        if (snapshot.IsRootSuperAdmin)
        {
            effective = effective.Concat(PermissionNames.RootOnly);
        }

        if (effective.Contains(requirement.Permission, StringComparer.OrdinalIgnoreCase))
        {
            // The handover read service hides missing/revoked practice delegation as 404.
            // Keep middleware as commercial preflight so that scoped decision stays there.
            var resource = requirement.Permission == Phase18PermissionNames.HandoverView
                ? new Wasla.Application.Features.Subscriptions.SubscriptionResource(Wasla.Application.Features.Subscriptions.SubscriptionResourceKind.AuthorizationPreflight, Guid.Empty)
                : ContinuityResource(context);
            if (await subscriptionGuard.AllowedAsync(snapshot, requirement.Permission, CancellationToken.None, resource))
                context.Succeed(requirement);
        }
        else if (requirement.Permission == PermissionNames.NotificationsDeliveriesRetry)
            await notificationAudit.RetryDeniedAsync(userId, CancellationToken.None);
    }

    private static Wasla.Application.Features.Subscriptions.SubscriptionResource? ContinuityResource(AuthorizationHandlerContext context)
    {
        if (context.Resource is not HttpContext http) return null;
        foreach (var (key, kind) in new[]
        {
            ("reservationId", Wasla.Application.Features.Subscriptions.SubscriptionResourceKind.Reservation),
            ("ticketId", Wasla.Application.Features.Subscriptions.SubscriptionResourceKind.Ticket),
            ("encounterId", Wasla.Application.Features.Subscriptions.SubscriptionResourceKind.Encounter),
            ("prescriptionId", Wasla.Application.Features.Subscriptions.SubscriptionResourceKind.Prescription),
            ("practiceId", Wasla.Application.Features.Subscriptions.SubscriptionResourceKind.Practice)
        })
            if (http.Request.RouteValues.TryGetValue(key, out var value) && Guid.TryParse(value?.ToString(), out var id)) return new(kind, id);
        return new(Wasla.Application.Features.Subscriptions.SubscriptionResourceKind.AuthorizationPreflight, Guid.Empty);
    }
}

