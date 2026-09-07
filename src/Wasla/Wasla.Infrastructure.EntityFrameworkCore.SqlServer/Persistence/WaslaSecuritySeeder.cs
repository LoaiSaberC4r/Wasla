using BuildingBlock.Application.Abstraction.Encryption;
using BuildingBlock.Application.Time;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wasla.Domain.Common;
using Wasla.Domain.Security;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Options;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed class WaslaSecuritySeeder(
    WaslaDbContext dbContext,
    IPasswordService passwordService,
    IDateTimeProvider clock,
    IOptions<RootSuperAdminOptions> rootOptions)
{
    private static readonly Guid RootApplicationUserId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid RootSuperAdminId = Guid.Parse("30000000-0000-0000-0000-000000000002");
    private static readonly Guid RootUserRoleId = Guid.Parse("30000000-0000-0000-0000-000000000003");

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        ValidateRootOptions();
        await SeedRootSuperAdminAsync(cancellationToken);
        await SeedRolesAsync(cancellationToken);
        await SeedPermissionsAsync(cancellationToken);
        await SeedRolePermissionsAsync(cancellationToken);
        await SeedRootRoleAsync(cancellationToken);
    }

    private async Task SeedRootSuperAdminAsync(CancellationToken cancellationToken)
    {
        var configured = rootOptions.Value;
        var semanticUser = await dbContext.ApplicationUsers
            .SingleOrDefaultAsync(user => user.UserName == configured.UserName || user.Email == configured.Email, cancellationToken);
        if (semanticUser is not null && semanticUser.Id != RootApplicationUserId)
        {
            throw new InvalidOperationException("Root SuperAdmin credentials conflict with an existing user identity.");
        }

        var user = await dbContext.ApplicationUsers.FindAsync([RootApplicationUserId], cancellationToken);
        if (user is null)
        {
            var result = ApplicationUser.Create(
                RootApplicationUserId,
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
        if (semanticRoot is not null && semanticRoot.Id != RootSuperAdminId)
        {
            throw new InvalidOperationException("A Root SuperAdmin exists with a conflicting deterministic identity.");
        }

        var rootById = await dbContext.SuperAdmins.IgnoreQueryFilters()
            .SingleOrDefaultAsync(admin => admin.Id == RootSuperAdminId, cancellationToken);
        if (rootById is null)
        {
            var result = SuperAdmin.Create(
                RootSuperAdminId,
                RootApplicationUserId,
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
        else if (!rootById.IsRootSuperAdmin || rootById.ApplicationUserId != RootApplicationUserId)
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
                PermissionNames.RolePermissionsManage
            ],
            [SystemRoleIds.Doctor] = [PermissionNames.DoctorOnboardingViewOwn],
            [SystemRoleIds.Patient] = [PermissionNames.PatientProfileViewOwn]
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

    private async Task SeedRootRoleAsync(CancellationToken cancellationToken)
    {
        if (!await dbContext.UserRoles.AnyAsync(
                mapping => mapping.ApplicationUserId == RootApplicationUserId &&
                           mapping.RoleId == SystemRoleIds.SuperAdmin,
                cancellationToken))
        {
            dbContext.UserRoles.Add(new UserRole(
                RootUserRoleId,
                RootApplicationUserId,
                SystemRoleIds.SuperAdmin));
            await dbContext.SaveChangesAsync(cancellationToken);
        }
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
