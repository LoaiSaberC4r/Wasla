using BuildingBlock.Application.Abstraction.Encryption;
using BuildingBlock.Application.Time;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wasla.Domain.Common;
using Wasla.Domain.Practices;
using Wasla.Domain.Security;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Options;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed class WaslaSecuritySeeder(
    WaslaDbContext dbContext,
    IPasswordService passwordService,
    IDateTimeProvider clock,
    IOptions<RootSuperAdminOptions> rootOptions)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        ValidateRootOptions();
        await SeedRootSuperAdminAsync(cancellationToken);
        await SeedRolesAsync(cancellationToken);
        await SeedPermissionsAsync(cancellationToken);
        await SeedRolePermissionsAsync(cancellationToken);
        await BackfillLegacyReceptionReservationPermissionsAsync(cancellationToken);
        await BackfillLegacyReceptionTicketPermissionsAsync(cancellationToken);
        await SeedRootRoleAsync(cancellationToken);
    }

    private async Task SeedRootSuperAdminAsync(CancellationToken cancellationToken)
    {
        var configured = rootOptions.Value;
        var semanticUser = await dbContext.ApplicationUsers
            .SingleOrDefaultAsync(user => user.UserName == configured.UserName || user.Email == configured.Email, cancellationToken);
        if (semanticUser is not null && semanticUser.Id != SystemSeedIds.RootApplicationUserId)
        {
            throw new InvalidOperationException("Root SuperAdmin credentials conflict with an existing user identity.");
        }

        var user = await dbContext.ApplicationUsers.FindAsync([SystemSeedIds.RootApplicationUserId], cancellationToken);
        if (user is null)
        {
            var result = ApplicationUser.Create(
                SystemSeedIds.RootApplicationUserId,
                configured.UserName,
                configured.Email,
                configured.PhoneNumber,
                await passwordService.HashAsync(configured.Password, cancellationToken),
                UserType.SuperAdmin,
                isFirstLogin: false,
                clock.UtcNow);
            if (result.IsFailure)
            {
                throw new InvalidOperationException("Root SuperAdmin user configuration is invalid.");
            }

            user = result.Value;
            dbContext.ApplicationUsers.Add(user);
        }
        else if (!string.Equals(user.UserName, configured.UserName.Trim(), StringComparison.Ordinal) ||
                 !string.Equals(user.Email, configured.Email.Trim(), StringComparison.OrdinalIgnoreCase) ||
                 user.UserType != UserType.SuperAdmin)
        {
            throw new InvalidOperationException("The deterministic Root SuperAdmin user id is assigned to conflicting data.");
        }

        var semanticRoot = await dbContext.SuperAdmins.IgnoreQueryFilters()
            .SingleOrDefaultAsync(admin => admin.IsRootSuperAdmin, cancellationToken);
        if (semanticRoot is not null && semanticRoot.Id != SystemSeedIds.RootSuperAdminId)
        {
            throw new InvalidOperationException("A Root SuperAdmin exists with a conflicting deterministic identity.");
        }

        var rootById = await dbContext.SuperAdmins.IgnoreQueryFilters()
            .SingleOrDefaultAsync(admin => admin.Id == SystemSeedIds.RootSuperAdminId, cancellationToken);
        if (rootById is null)
        {
            var result = SuperAdmin.Create(
                SystemSeedIds.RootSuperAdminId,
                SystemSeedIds.RootApplicationUserId,
                configured.NameAr,
                configured.NameEn,
                isRootSuperAdmin: true,
                createdByApplicationUserId: null);
            if (result.IsFailure)
            {
                throw new InvalidOperationException("Root SuperAdmin profile configuration is invalid.");
            }

            dbContext.SuperAdmins.Add(result.Value);
        }
        else if (!rootById.IsRootSuperAdmin || rootById.ApplicationUserId != SystemSeedIds.RootApplicationUserId)
        {
            throw new InvalidOperationException("The deterministic Root SuperAdmin profile id is assigned to conflicting data.");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedRolesAsync(CancellationToken cancellationToken)
    {
        var roles = new[]
        {
            (SystemRoleIds.SuperAdmin, SystemRoleNames.SuperAdmin),
            (SystemRoleIds.Doctor, SystemRoleNames.Doctor),
            (SystemRoleIds.Reception, SystemRoleNames.Reception),
            (SystemRoleIds.Patient, SystemRoleNames.Patient)
        };
        foreach (var (id, name) in roles)
        {
            var byName = await dbContext.Roles.SingleOrDefaultAsync(role => role.Name == name, cancellationToken);
            if (byName is not null && byName.Id != id)
            {
                throw new InvalidOperationException($"System role '{name}' has a conflicting identity.");
            }

            var byId = await dbContext.Roles.FindAsync([id], cancellationToken);
            if (byId is not null && !string.Equals(byId.Name, name, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"System role id for '{name}' is already used.");
            }

            if (byName is null && byId is null)
            {
                dbContext.Roles.Add(Role.Create(id, name, true).Value);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedPermissionsAsync(CancellationToken cancellationToken)
    {
        foreach (var name in PermissionNames.All)
        {
            var id = SystemPermissionIds.For(name);
            var byName = await dbContext.Permissions.SingleOrDefaultAsync(permission => permission.Name == name, cancellationToken);
            if (byName is not null && byName.Id != id)
            {
                throw new InvalidOperationException($"System permission '{name}' has a conflicting identity.");
            }

            var byId = await dbContext.Permissions.FindAsync([id], cancellationToken);
            if (byId is not null && !string.Equals(byId.Name, name, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"System permission id for '{name}' is already used.");
            }

            if (byName is null && byId is null)
            {
                dbContext.Permissions.Add(Permission.Create(id, name, true).Value);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedRolePermissionsAsync(CancellationToken cancellationToken)
    {
        var mappings = new Dictionary<Guid, IReadOnlyList<string>>
        {
            [SystemRoleIds.SuperAdmin] =
            [
                PermissionNames.DoctorsViewAll,
                PermissionNames.DoctorsViewDetails,
                PermissionNames.DoctorsApprove,
                PermissionNames.DoctorsReject,
                PermissionNames.DoctorsSuspend,
                PermissionNames.DoctorsReactivate,
                PermissionNames.RolesView,
                PermissionNames.PermissionsView,
                PermissionNames.RolePermissionsManage,
                PermissionNames.SpecializationsView,
                PermissionNames.SpecializationsCreate,
                PermissionNames.SpecializationsUpdate,
                PermissionNames.SpecializationsActivate,
                PermissionNames.SpecializationsDeactivate,
                PermissionNames.SpecializationsDelete,
                PermissionNames.SpecializationsRestore,
                PermissionNames.DoctorSpecializationRequestsViewAll,
                PermissionNames.DoctorSpecializationRequestsViewDetails,
                PermissionNames.DoctorSpecializationRequestsAdjust,
                PermissionNames.DoctorSpecializationRequestsApprove,
                PermissionNames.DoctorSpecializationRequestsReject,
                PermissionNames.DoctorSpecializationRequestsRequestModification,
                PermissionNames.FamilyRelationshipRequestsViewAll,
                PermissionNames.FamilyRelationshipRequestsViewDetails,
                PermissionNames.FamilyRelationshipRequestsApprove,
                PermissionNames.FamilyRelationshipRequestsReject,
                PermissionNames.FamilyRelationshipRequestsRequestModification,
                PermissionNames.ReservationsViewAdministrative
            ],
            [SystemRoleIds.Doctor] =
            [
                PermissionNames.DoctorOnboardingViewOwn,
                PermissionNames.DoctorProfileViewOwn,
                PermissionNames.DoctorProfileUpdateOwn,
                PermissionNames.DoctorSpecializationsViewOwn,
                PermissionNames.DoctorSpecializationsSubmitOwn,
                PermissionNames.DoctorSpecializationsResubmitOwn,
                PermissionNames.DoctorPracticeLocationViewOwn,
                PermissionNames.DoctorPracticeLocationManageOwn,
                PermissionNames.DoctorPracticesViewOwn,
                PermissionNames.DoctorPracticesManageOwn,
                PermissionNames.DoctorPracticesActivateOwn,
                PermissionNames.DoctorPracticeConfigurationViewOwn,
                PermissionNames.DoctorPracticeConfigurationManageOwn,
                PermissionNames.DoctorPracticeBrandingViewOwn,
                PermissionNames.DoctorPracticeBrandingManageOwn,
                PermissionNames.DoctorPracticeScheduleViewOwn,
                PermissionNames.DoctorPracticeScheduleManageOwn,
                PermissionNames.DoctorPracticeSegmentsViewOwn,
                PermissionNames.DoctorPracticeSegmentsManageOwn,
                PermissionNames.DoctorPracticePricingViewOwn,
                PermissionNames.DoctorPracticePricingManageOwn,
                PermissionNames.ReceptionUsersViewOwn,
                PermissionNames.ReceptionUsersManageOwn,
                PermissionNames.ReceptionAssignmentsViewOwn,
                PermissionNames.ReceptionAssignmentsManageOwn,
                PermissionNames.DoctorPracticeReservationsViewOwn,
                PermissionNames.DoctorPracticeReservationsCancelOwn,
                PermissionNames.DoctorPracticeReservationsRescheduleOwn,
                PermissionNames.DoctorPracticeTicketsViewOwn,
                PermissionNames.DoctorPracticeTicketsCallOwn,
                PermissionNames.DoctorPracticeTicketsConfirmNoResponseOwn,
                PermissionNames.DoctorPracticeTicketsManualCallOwn,
                PermissionNames.DoctorPracticeTicketsRestoreNoShowOwn,
                PermissionNames.DoctorPracticeTicketsCancelOwn,
                PermissionNames.DoctorPracticeTicketsStartOwn,
                PermissionNames.DoctorPracticeTicketsCompleteOwn
            ],
            [SystemRoleIds.Reception] =
            [
                PermissionNames.PatientsSearchBasic,
                PermissionNames.PatientsRegister,
                PermissionNames.FamilyRelationshipRequestsCreateAssisted,
                PermissionNames.FamilyRelationshipRequestsViewAssisted,
                PermissionNames.FamilyRelationshipRequestsResubmitAssisted,
                PermissionNames.PracticeReservationsManage,
                PermissionNames.PracticeReservationsView,
                PermissionNames.PracticeReservationsCreate,
                PermissionNames.PracticeReservationsCancel,
                PermissionNames.PracticeReservationsReschedule,
                PermissionNames.PracticeReservationsRestoreNoShow,
                PermissionNames.PracticeQueueManage,
                PermissionNames.PracticePaymentsRecord,
                PermissionNames.PracticeWalkInsCreate,
                PermissionNames.PracticeTicketsView,
                PermissionNames.PracticeTicketsCheckIn,
                PermissionNames.PracticeTicketsForceCheckIn,
                PermissionNames.PracticeTicketsCreateWalkIn,
                PermissionNames.PracticeTicketsRecordPayment,
                PermissionNames.PracticeTicketsCall,
                PermissionNames.PracticeTicketsManualCall,
                PermissionNames.PracticeTicketsRestoreNoShow,
                PermissionNames.PracticeTicketsCancel
            ],
            [SystemRoleIds.Patient] =
            [
                PermissionNames.PatientProfileViewOwn,
                PermissionNames.PatientProfileUpdateOwn,
                PermissionNames.PatientContactsViewOwn,
                PermissionNames.PatientContactsManageOwn,
                PermissionNames.FamiliesViewOwn,
                PermissionNames.FamiliesManageOwn,
                PermissionNames.FamilyRelationshipRequestsCreate,
                PermissionNames.FamilyRelationshipRequestsViewOwn,
                PermissionNames.FamilyRelationshipRequestsResubmitOwn,
                PermissionNames.ReservationsViewOwn,
                PermissionNames.ReservationsCreateOwn,
                PermissionNames.ReservationsCancelOwn,
                PermissionNames.ReservationsRescheduleOwn,
                PermissionNames.ReservationsViewDependents,
                PermissionNames.ReservationsCreateDependents,
                PermissionNames.ReservationsCancelDependents,
                PermissionNames.ReservationsRescheduleDependents,
                PermissionNames.TicketsViewOwn
            ]
        };
        foreach (var (roleId, permissionNames) in mappings)
        {
            foreach (var permissionName in permissionNames)
            {
                var permissionId = SystemPermissionIds.For(permissionName);
                if (await dbContext.RolePermissions.AnyAsync(
                        mapping => mapping.RoleId == roleId && mapping.PermissionId == permissionId,
                        cancellationToken))
                {
                    continue;
                }

                var deterministicMappingId = CreateMappingId(roleId, permissionId);
                dbContext.RolePermissions.Add(new RolePermission(
                    deterministicMappingId,
                    roleId,
                    permissionId));
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task BackfillLegacyReceptionReservationPermissionsAsync(CancellationToken cancellationToken)
    {
        var legacyId = SystemPermissionIds.For(PermissionNames.PracticeReservationsManage);
        var granularIds = new[]
        {
            SystemPermissionIds.For(PermissionNames.PracticeReservationsView),
            SystemPermissionIds.For(PermissionNames.PracticeReservationsCreate),
            SystemPermissionIds.For(PermissionNames.PracticeReservationsCancel),
            SystemPermissionIds.For(PermissionNames.PracticeReservationsReschedule),
            SystemPermissionIds.For(PermissionNames.PracticeReservationsRestoreNoShow)
        };
        var assignmentIds = await dbContext.ReceptionPracticeAssignmentPermissions
            .Where(item => item.PermissionId == legacyId)
            .Select(item => item.AssignmentId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        if (assignmentIds.Length == 0)
        {
            return;
        }

        var existing = await dbContext.ReceptionPracticeAssignmentPermissions
            .Where(item => assignmentIds.Contains(item.AssignmentId) && granularIds.Contains(item.PermissionId))
            .Select(item => new { item.AssignmentId, item.PermissionId })
            .ToArrayAsync(cancellationToken);
        var existingKeys = existing.Select(item => (item.AssignmentId, item.PermissionId)).ToHashSet();
        var now = DateTime.UtcNow;
        foreach (var assignmentId in assignmentIds)
        {
            foreach (var permissionId in granularIds)
            {
                if (!existingKeys.Contains((assignmentId, permissionId)))
                {
                    dbContext.ReceptionPracticeAssignmentPermissions.Add(
                        new ReceptionPracticeAssignmentPermission(
                            Guid.NewGuid(), assignmentId, permissionId, SystemSeedIds.RootApplicationUserId, now));
                }
            }
        }

        var legacyLinks = await dbContext.ReceptionPracticeAssignmentPermissions
            .Where(item => assignmentIds.Contains(item.AssignmentId) && item.PermissionId == legacyId)
            .ToArrayAsync(cancellationToken);
        dbContext.ReceptionPracticeAssignmentPermissions.RemoveRange(legacyLinks);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedRootRoleAsync(CancellationToken cancellationToken)
    {
        if (!await dbContext.UserRoles.AnyAsync(
                mapping => mapping.ApplicationUserId == SystemSeedIds.RootApplicationUserId &&
                           mapping.RoleId == SystemRoleIds.SuperAdmin,
                cancellationToken))
        {
            dbContext.UserRoles.Add(new UserRole(
                SystemSeedIds.RootUserRoleId,
                SystemSeedIds.RootApplicationUserId,
                SystemRoleIds.SuperAdmin));
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task BackfillLegacyReceptionTicketPermissionsAsync(CancellationToken cancellationToken)
    {
        var legacyToGranular = new Dictionary<Guid, Guid[]>
        {
            [SystemPermissionIds.For(PermissionNames.PracticeQueueManage)] =
            [
                SystemPermissionIds.For(PermissionNames.PracticeTicketsView),
                SystemPermissionIds.For(PermissionNames.PracticeTicketsCall),
                SystemPermissionIds.For(PermissionNames.PracticeTicketsManualCall),
                SystemPermissionIds.For(PermissionNames.PracticeTicketsRestoreNoShow),
                SystemPermissionIds.For(PermissionNames.PracticeTicketsCancel)
            ],
            [SystemPermissionIds.For(PermissionNames.PracticePaymentsRecord)] =
            [SystemPermissionIds.For(PermissionNames.PracticeTicketsRecordPayment)],
            [SystemPermissionIds.For(PermissionNames.PracticeWalkInsCreate)] =
            [SystemPermissionIds.For(PermissionNames.PracticeTicketsCreateWalkIn)],
            [SystemPermissionIds.For(PermissionNames.PracticeReservationsCreate)] =
            [
                SystemPermissionIds.For(PermissionNames.PracticeTicketsCheckIn),
                SystemPermissionIds.For(PermissionNames.PracticeTicketsForceCheckIn)
            ]
        };
        var legacyIds = legacyToGranular.Keys.ToArray();
        var legacyLinks = await dbContext.ReceptionPracticeAssignmentPermissions
            .Where(item => legacyIds.Contains(item.PermissionId))
            .Select(item => new { item.AssignmentId, item.PermissionId })
            .ToArrayAsync(cancellationToken);
        if (legacyLinks.Length == 0)
        {
            return;
        }

        var targetIds = legacyToGranular.Values.SelectMany(item => item).Distinct().ToArray();
        var assignmentIds = legacyLinks.Select(item => item.AssignmentId).Distinct().ToArray();
        var existing = await dbContext.ReceptionPracticeAssignmentPermissions
            .Where(item => assignmentIds.Contains(item.AssignmentId) && targetIds.Contains(item.PermissionId))
            .Select(item => new { item.AssignmentId, item.PermissionId })
            .ToArrayAsync(cancellationToken);
        var existingKeys = existing.Select(item => (item.AssignmentId, item.PermissionId)).ToHashSet();
        var now = DateTime.UtcNow;
        foreach (var legacyLink in legacyLinks)
        {
            foreach (var permissionId in legacyToGranular[legacyLink.PermissionId])
            {
                if (existingKeys.Add((legacyLink.AssignmentId, permissionId)))
                {
                    dbContext.ReceptionPracticeAssignmentPermissions.Add(
                        new ReceptionPracticeAssignmentPermission(
                            Guid.NewGuid(), legacyLink.AssignmentId, permissionId,
                            SystemSeedIds.RootApplicationUserId, now));
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private void ValidateRootOptions()
    {
        var options = rootOptions.Value;
        if (string.IsNullOrWhiteSpace(options.UserName) ||
            string.IsNullOrWhiteSpace(options.Email) ||
            string.IsNullOrWhiteSpace(options.NameAr) ||
            string.IsNullOrWhiteSpace(options.Password) ||
            !passwordService.IsStrongPassword(options.Password))
        {
            throw new InvalidOperationException(
                "RootSuperAdmin credentials must be supplied from secret configuration before seeding.");
        }
    }

    private static Guid CreateMappingId(Guid roleId, Guid permissionId)
    {
        Span<byte> input = stackalloc byte[32];
        roleId.TryWriteBytes(input[..16]);
        permissionId.TryWriteBytes(input[16..]);
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(input, hash);
        return new Guid(hash[..16]);
    }
}
