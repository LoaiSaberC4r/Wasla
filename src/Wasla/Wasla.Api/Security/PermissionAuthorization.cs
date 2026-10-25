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
            effective = effective.Where(permission => string.Equals(
                permission,
                PermissionNames.DoctorOnboardingViewOwn,
                StringComparison.OrdinalIgnoreCase));
        }

        if (snapshot.IsRootSuperAdmin)
        {
            effective = effective.Concat(PermissionNames.RootOnly);
        }

        if (effective.Contains(requirement.Permission, StringComparer.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
        }
        else if (requirement.Permission == PermissionNames.NotificationsDeliveriesRetry)
            await notificationAudit.RetryDeniedAsync(userId, CancellationToken.None);
    }
}

