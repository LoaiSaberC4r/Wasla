using Microsoft.EntityFrameworkCore;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Doctors;
using Wasla.Domain.Patients;
using Wasla.Domain.Security;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed class WaslaDataStore(WaslaDbContext dbContext) : IWaslaDataStore
{
    public Task<bool> UserNameExistsAsync(
        string userName,
        Guid? excludingUserId,
        CancellationToken cancellationToken)
        => dbContext.ApplicationUsers.AnyAsync(
            user => user.UserName == userName &&
                    (!excludingUserId.HasValue || user.Id != excludingUserId.Value),
            cancellationToken);

    public Task<bool> EmailExistsAsync(
        string email,
        Guid? excludingUserId,
        CancellationToken cancellationToken)
        => dbContext.ApplicationUsers.AnyAsync(
            user => user.Email == email &&
                    (!excludingUserId.HasValue || user.Id != excludingUserId.Value),
            cancellationToken);

    public Task<ApplicationUser?> FindUserByIdAsync(Guid id, CancellationToken cancellationToken)
        => dbContext.ApplicationUsers.SingleOrDefaultAsync(user => user.Id == id, cancellationToken);

    public Task<ApplicationUser?> FindUserByIdentifierAsync(string identifier, CancellationToken cancellationToken)
    {
        var normalizedEmail = identifier.Trim().ToLowerInvariant();
        var normalizedUserName = identifier.Trim();
        return dbContext.ApplicationUsers.SingleOrDefaultAsync(
            user => user.Email == normalizedEmail || user.UserName == normalizedUserName,
            cancellationToken);
    }

    public async Task<UserAccessSnapshot?> GetAccessSnapshotAsync(
        Guid applicationUserId,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.ApplicationUsers
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == applicationUserId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var roles = await (
            from userRole in dbContext.UserRoles.AsNoTracking()
            join role in dbContext.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where userRole.ApplicationUserId == applicationUserId
            orderby role.Name
            select role.Name).ToListAsync(cancellationToken);
        var permissions = await (
            from userRole in dbContext.UserRoles.AsNoTracking()
            join rolePermission in dbContext.RolePermissions.AsNoTracking()
                on userRole.RoleId equals rolePermission.RoleId
            join permission in dbContext.Permissions.AsNoTracking()
                on rolePermission.PermissionId equals permission.Id
            where userRole.ApplicationUserId == applicationUserId
            select permission.Name)
            .Distinct()
            .OrderBy(name => name)
            .ToListAsync(cancellationToken);
        var doctor = await dbContext.Doctors.AsNoTracking()
            .Where(item => item.ApplicationUserId == applicationUserId)
            .Select(item => new { item.Id, item.ApprovalStatus })
            .SingleOrDefaultAsync(cancellationToken);
        var patientId = await dbContext.Patients.AsNoTracking()
            .Where(item => item.ApplicationUserId == applicationUserId)
            .Select(item => (Guid?)item.Id)
            .SingleOrDefaultAsync(cancellationToken);
        var superAdmin = await dbContext.SuperAdmins.AsNoTracking()
            .Where(item => item.ApplicationUserId == applicationUserId)
            .Select(item => new { item.Id, item.IsRootSuperAdmin })
            .SingleOrDefaultAsync(cancellationToken);

        return new UserAccessSnapshot(
            user,
            roles,
            permissions,
            doctor?.Id,
            doctor?.ApprovalStatus,
            patientId,
            superAdmin?.Id,
            superAdmin?.IsRootSuperAdmin == true);
    }

    public Task<Doctor?> FindDoctorByIdAsync(Guid doctorId, CancellationToken cancellationToken)
        => dbContext.Doctors.SingleOrDefaultAsync(doctor => doctor.Id == doctorId, cancellationToken);

    public Task<Doctor?> FindDoctorByUserIdAsync(Guid applicationUserId, CancellationToken cancellationToken)
        => dbContext.Doctors.AsNoTracking()
            .SingleOrDefaultAsync(doctor => doctor.ApplicationUserId == applicationUserId, cancellationToken);

    public Task<Patient?> FindPatientByUserIdAsync(Guid applicationUserId, CancellationToken cancellationToken)
        => dbContext.Patients.AsNoTracking()
            .SingleOrDefaultAsync(patient => patient.ApplicationUserId == applicationUserId, cancellationToken);

    public Task<bool> NationalIdExistsAsync(
        string nationalId,
        Guid? excludingDoctorId,
        CancellationToken cancellationToken)
        => dbContext.Doctors.AsNoTracking().AnyAsync(
            doctor => doctor.NationalId == nationalId &&
                      (!excludingDoctorId.HasValue || doctor.Id != excludingDoctorId.Value),
            cancellationToken);

    public async Task<(IReadOnlyList<DoctorAdminRecord> Items, long TotalCount)> ListDoctorsAsync(
        DoctorApprovalStatus? approvalStatus,
        string? searchText,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Doctors.AsNoTracking();
        if (approvalStatus.HasValue)
        {
            query = query.Where(doctor => doctor.ApprovalStatus == approvalStatus.Value);
        }

        var search = searchText?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(doctor =>
                doctor.NameAr.Contains(search) ||
                doctor.NameEn != null && doctor.NameEn.Contains(search) ||
                doctor.ApplicationUser.UserName.Contains(search) ||
                doctor.ApplicationUser.Email.Contains(search) ||
                doctor.ApplicationUser.PhoneNumber != null && doctor.ApplicationUser.PhoneNumber.Contains(search));
        }

        var total = await query.LongCountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(doctor => doctor.CreatedOnUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(doctor => new DoctorAdminRecord(doctor, doctor.ApplicationUser))
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public Task<SuperAdminRecord?> FindSuperAdminAsync(
        Guid superAdminId,
        bool includeDeleted,
        CancellationToken cancellationToken)
    {
        IQueryable<SuperAdmin> query = dbContext.SuperAdmins;
        if (includeDeleted)
        {
            query = query.IgnoreQueryFilters();
        }

        return query.Where(admin => admin.Id == superAdminId)
            .Select(admin => new SuperAdminRecord(admin, admin.ApplicationUser))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<SuperAdmin?> FindSuperAdminByUserIdAsync(
        Guid applicationUserId,
        bool includeDeleted,
        CancellationToken cancellationToken)
    {
        IQueryable<SuperAdmin> query = dbContext.SuperAdmins;
        if (includeDeleted)
        {
            query = query.IgnoreQueryFilters();
        }

        return query.SingleOrDefaultAsync(
            admin => admin.ApplicationUserId == applicationUserId,
            cancellationToken);
    }

    public async Task<(IReadOnlyList<SuperAdminRecord> Items, long TotalCount)> ListSuperAdminsAsync(
        string? searchText,
        int pageNumber,
        int pageSize,
        bool includeDeleted,
        CancellationToken cancellationToken)
    {
        IQueryable<SuperAdmin> query = dbContext.SuperAdmins.AsNoTracking();
        if (includeDeleted)
        {
            query = query.IgnoreQueryFilters();
        }

        var search = searchText?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(admin =>
                admin.NameAr.Contains(search) ||
                admin.NameEn != null && admin.NameEn.Contains(search) ||
                admin.ApplicationUser.UserName.Contains(search) ||
                admin.ApplicationUser.Email.Contains(search));
        }

        var total = await query.LongCountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(admin => admin.IsRootSuperAdmin)
            .ThenBy(admin => admin.NameAr)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(admin => new SuperAdminRecord(admin, admin.ApplicationUser))
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public Task<Role?> FindRoleByIdAsync(Guid id, CancellationToken cancellationToken)
        => dbContext.Roles.SingleOrDefaultAsync(role => role.Id == id, cancellationToken);

    public Task<Role?> FindRoleByNameAsync(string name, CancellationToken cancellationToken)
        => dbContext.Roles.SingleOrDefaultAsync(role => role.Name == name, cancellationToken);

    public async Task<IReadOnlyList<Role>> ListRolesAsync(CancellationToken cancellationToken)
        => await dbContext.Roles.AsNoTracking().OrderBy(role => role.Name).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Permission>> ListPermissionsAsync(CancellationToken cancellationToken)
        => await dbContext.Permissions.AsNoTracking().OrderBy(permission => permission.Name).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RolePermission>> ListRolePermissionsAsync(
        Guid roleId,
        CancellationToken cancellationToken)
        => await dbContext.RolePermissions.Where(mapping => mapping.RoleId == roleId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Permission>> ListPermissionsByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        return await dbContext.Permissions
            .Where(permission => ids.Contains(permission.Id))
            .OrderBy(permission => permission.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<PasswordResetChallenge?> FindChallengeAsync(Guid id, CancellationToken cancellationToken)
        => dbContext.PasswordResetChallenges.SingleOrDefaultAsync(challenge => challenge.Id == id, cancellationToken);

    public Task<PasswordResetChallenge?> FindLatestChallengeAsync(
        Guid applicationUserId,
        CancellationToken cancellationToken)
        => dbContext.PasswordResetChallenges
            .Where(challenge => challenge.ApplicationUserId == applicationUserId)
            .OrderByDescending(challenge => challenge.CreatedOnUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<PasswordResetChallenge>> ListActiveChallengesAsync(
        Guid applicationUserId,
        CancellationToken cancellationToken)
        => await dbContext.PasswordResetChallenges
            .Where(challenge =>
                challenge.ApplicationUserId == applicationUserId &&
                challenge.InvalidatedOnUtc == null &&
                challenge.ConsumedOnUtc == null)
            .ToListAsync(cancellationToken);

    public void Add<TEntity>(TEntity entity) where TEntity : class
        => dbContext.Set<TEntity>().Add(entity);

    public void Remove<TEntity>(TEntity entity) where TEntity : class
        => dbContext.Set<TEntity>().Remove(entity);

    public void SetOriginalRowVersion<TEntity>(TEntity entity, byte[] rowVersion) where TEntity : class
        => dbContext.Entry(entity).Property("RowVersion").OriginalValue = rowVersion;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        => dbContext.SaveChangesAsync(cancellationToken);
}
