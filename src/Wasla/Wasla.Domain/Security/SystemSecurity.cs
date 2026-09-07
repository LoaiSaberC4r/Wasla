namespace Wasla.Domain.Security;

public static class SystemRoleNames
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Doctor = "Doctor";
    public const string Reception = "Reception";
    public const string Patient = "Patient";
}

public static class SystemRoleIds
{
    public static readonly Guid SuperAdmin = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid Doctor = Guid.Parse("10000000-0000-0000-0000-000000000002");
    public static readonly Guid Reception = Guid.Parse("10000000-0000-0000-0000-000000000003");
    public static readonly Guid Patient = Guid.Parse("10000000-0000-0000-0000-000000000004");
}

public static class PermissionNames
{
    public const string DoctorsViewAll = "Doctors.ViewAll";
    public const string DoctorsViewDetails = "Doctors.ViewDetails";
    public const string DoctorsApprove = "Doctors.Approve";
    public const string DoctorsReject = "Doctors.Reject";
    public const string DoctorsSuspend = "Doctors.Suspend";
    public const string DoctorsReactivate = "Doctors.Reactivate";
    public const string RolesView = "Roles.View";
    public const string PermissionsView = "Permissions.View";
    public const string RolePermissionsManage = "RolePermissions.Manage";
    public const string DoctorOnboardingViewOwn = "DoctorOnboarding.ViewOwn";
    public const string PatientProfileViewOwn = "PatientProfile.ViewOwn";
    public const string SuperAdminsViewAll = "SuperAdmins.ViewAll";
    public const string SuperAdminsViewDetails = "SuperAdmins.ViewDetails";
    public const string SuperAdminsCreate = "SuperAdmins.Create";
    public const string SuperAdminsUpdate = "SuperAdmins.Update";
    public const string SuperAdminsActivate = "SuperAdmins.Activate";
    public const string SuperAdminsDeactivate = "SuperAdmins.Deactivate";
    public const string SuperAdminsDelete = "SuperAdmins.Delete";
    public const string SuperAdminsRestore = "SuperAdmins.Restore";

    public static readonly IReadOnlySet<string> RootOnly = new HashSet<string>(
        [
            SuperAdminsViewAll,
            SuperAdminsViewDetails,
            SuperAdminsCreate,
            SuperAdminsUpdate,
            SuperAdminsActivate,
            SuperAdminsDeactivate,
            SuperAdminsDelete,
            SuperAdminsRestore
        ],
        StringComparer.OrdinalIgnoreCase);

    public static readonly IReadOnlyList<string> All =
    [
        DoctorsViewAll,
        DoctorsViewDetails,
        DoctorsApprove,
        DoctorsReject,
        DoctorsSuspend,
        DoctorsReactivate,
        RolesView,
        PermissionsView,
        RolePermissionsManage,
        DoctorOnboardingViewOwn,
        PatientProfileViewOwn,
        SuperAdminsViewAll,
        SuperAdminsViewDetails,
        SuperAdminsCreate,
        SuperAdminsUpdate,
        SuperAdminsActivate,
        SuperAdminsDeactivate,
        SuperAdminsDelete,
        SuperAdminsRestore
    ];
}

public static class SystemPermissionIds
{
    public static Guid For(string permissionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionName);
        var index = PermissionNames.All
            .Select((name, position) => (name, position))
            .Single(item => string.Equals(item.name, permissionName, StringComparison.Ordinal))
            .position + 1;
        return Guid.Parse($"20000000-0000-0000-0000-{index:D12}");
    }
}

