using Microsoft.EntityFrameworkCore;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Doctors;
using Wasla.Domain.Patients;
using Wasla.Domain.Security;
using Wasla.Domain.ReferenceData;

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

    public Task<MedicalSpecialization?> FindMedicalSpecializationAsync(
        Guid id,
        bool includeDeleted,
        CancellationToken cancellationToken)
    {
        IQueryable<MedicalSpecialization> query = dbContext.MedicalSpecializations;
        if (includeDeleted)
        {
            query = query.IgnoreQueryFilters();
        }

        return query.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    }

    public Task<bool> MedicalSpecializationNameArExistsAsync(
        string nameAr,
        Guid? excludingId,
        CancellationToken cancellationToken)
        => dbContext.MedicalSpecializations.IgnoreQueryFilters().AsNoTracking().AnyAsync(
            item => item.NameAr == nameAr && (!excludingId.HasValue || item.Id != excludingId.Value),
            cancellationToken);

    public Task<bool> MedicalSpecializationNameEnExistsAsync(
        string nameEn,
        Guid? excludingId,
        CancellationToken cancellationToken)
        => dbContext.MedicalSpecializations.IgnoreQueryFilters().AsNoTracking().AnyAsync(
            item => item.NameEn == nameEn && (!excludingId.HasValue || item.Id != excludingId.Value),
            cancellationToken);

    public async Task<(IReadOnlyList<MedicalSpecialization> Items, long TotalCount)> ListMedicalSpecializationsAsync(
        string? search,
        bool? isActive,
        bool? isDeleted,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        IQueryable<MedicalSpecialization> query = dbContext.MedicalSpecializations.AsNoTracking().IgnoreQueryFilters();
        if (isDeleted.HasValue)
        {
            query = query.Where(item => item.IsDeleted == isDeleted.Value);
        }
        if (isActive.HasValue)
        {
            query = query.Where(item => item.IsActive == isActive.Value);
        }
        var normalizedSearch = search?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            query = query.Where(item => item.NameAr.Contains(normalizedSearch) ||
                                        item.NameEn != null && item.NameEn.Contains(normalizedSearch));
        }

        var count = await query.LongCountAsync(cancellationToken);
        var items = await query.OrderBy(item => item.SortOrder).ThenBy(item => item.NameAr)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return (items, count);
    }

    public async Task<IReadOnlyList<MedicalSpecializationOptionRecord>> ListSelectableMedicalSpecializationsAsync(
        CancellationToken cancellationToken)
        => await dbContext.MedicalSpecializations.AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.SortOrder).ThenBy(item => item.NameAr)
            .Select(item => new MedicalSpecializationOptionRecord(item.Id, item.NameAr, item.NameEn))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> ListAvailableMedicalSpecializationIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
        => await dbContext.MedicalSpecializations.AsNoTracking()
            .Where(item => ids.Contains(item.Id) && item.IsActive)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DoctorSpecializationViewRecord>> ListDoctorSpecializationsAsync(
        Guid doctorId,
        CancellationToken cancellationToken)
        => await (from selected in dbContext.DoctorSpecializations.AsNoTracking()
                  join specialization in dbContext.MedicalSpecializations.IgnoreQueryFilters().AsNoTracking()
                      on selected.MedicalSpecializationId equals specialization.Id
                  where selected.DoctorId == doctorId
                  orderby selected.IsPrimary descending, specialization.SortOrder, specialization.NameAr
                  select new DoctorSpecializationViewRecord(
                      specialization.Id,
                      specialization.NameAr,
                      specialization.NameEn,
                      selected.IsPrimary)).ToListAsync(cancellationToken);

    public Task<DoctorSpecializationRequest?> FindOpenDoctorSpecializationRequestAsync(
        Guid doctorId,
        CancellationToken cancellationToken)
        => dbContext.DoctorSpecializationRequests.SingleOrDefaultAsync(
            item => item.DoctorId == doctorId &&
                    (item.Status == DoctorSpecializationRequestStatus.PendingReview ||
                     item.Status == DoctorSpecializationRequestStatus.ModificationRequested),
            cancellationToken);

    public Task<DoctorSpecializationRequest?> FindLatestDoctorSpecializationRequestAsync(
        Guid doctorId,
        CancellationToken cancellationToken)
        => dbContext.DoctorSpecializationRequests.AsNoTracking()
            .Where(item => item.DoctorId == doctorId)
            .OrderByDescending(item => item.SubmittedOnUtc)
            .ThenByDescending(item => item.CreatedOnUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<DoctorSpecializationRequestOwnerRecord?> FindDoctorSpecializationRequestAsync(
        Guid requestId,
        CancellationToken cancellationToken)
        => (from request in dbContext.DoctorSpecializationRequests
            join doctor in dbContext.Doctors on request.DoctorId equals doctor.Id
            join user in dbContext.ApplicationUsers on doctor.ApplicationUserId equals user.Id
            where request.Id == requestId
            select new DoctorSpecializationRequestOwnerRecord(request, doctor, user))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<DoctorSpecializationRequestItemViewRecord>> ListDoctorSpecializationRequestItemsAsync(
        Guid requestId,
        int revisionNumber,
        CancellationToken cancellationToken)
        => await (from revision in dbContext.DoctorSpecializationRequestRevisions.AsNoTracking()
                  join item in dbContext.DoctorSpecializationRequestItems.AsNoTracking() on revision.Id equals item.RevisionId
                  join specialization in dbContext.MedicalSpecializations.IgnoreQueryFilters().AsNoTracking()
                      on item.MedicalSpecializationId equals specialization.Id
                  where revision.DoctorSpecializationRequestId == requestId && revision.RevisionNumber == revisionNumber
                  orderby item.IsPrimary descending, specialization.SortOrder, specialization.NameAr
                  select new DoctorSpecializationRequestItemViewRecord(
                      specialization.Id,
                      specialization.NameAr,
                      specialization.NameEn,
                      item.IsPrimary)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DoctorSpecializationRequestRevision>> ListDoctorSpecializationRequestRevisionsAsync(
        Guid requestId,
        CancellationToken cancellationToken)
        => await dbContext.DoctorSpecializationRequestRevisions.AsNoTracking()
            .Where(item => item.DoctorSpecializationRequestId == requestId)
            .OrderBy(item => item.RevisionNumber)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DoctorSpecializationRequestHistory>> ListDoctorSpecializationRequestHistoryAsync(
        Guid requestId,
        CancellationToken cancellationToken)
        => await dbContext.DoctorSpecializationRequestHistories.AsNoTracking()
            .Where(item => item.DoctorSpecializationRequestId == requestId)
            .OrderBy(item => item.PerformedOnUtc).ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<DoctorSpecializationRequestQueueRecord> Items, long TotalCount)> ListDoctorSpecializationRequestsAsync(
        DoctorSpecializationRequestStatus? status,
        DoctorSpecializationRequestType? type,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = from request in dbContext.DoctorSpecializationRequests.AsNoTracking()
                    join doctor in dbContext.Doctors.AsNoTracking() on request.DoctorId equals doctor.Id
                    join user in dbContext.ApplicationUsers.AsNoTracking() on doctor.ApplicationUserId equals user.Id
                    select new { Request = request, Doctor = doctor, User = user };
        if (status.HasValue)
        {
            query = query.Where(item => item.Request.Status == status.Value);
        }
        if (type.HasValue)
        {
            query = query.Where(item => item.Request.Type == type.Value);
        }
        var normalizedSearch = search?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            query = query.Where(item => item.Doctor.NameAr.Contains(normalizedSearch) ||
                                        item.Doctor.NameEn != null && item.Doctor.NameEn.Contains(normalizedSearch) ||
                                        item.User.Email.Contains(normalizedSearch) || item.User.UserName.Contains(normalizedSearch));
        }

        var count = await query.LongCountAsync(cancellationToken);
        var items = await query.OrderByDescending(item => item.Request.SubmittedOnUtc)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(item => new DoctorSpecializationRequestQueueRecord(
                item.Request.Id,
                item.Doctor.Id,
                item.Doctor.NameAr,
                item.Doctor.NameEn,
                item.User.Email,
                item.Request.Type,
                item.Request.Status,
                item.Request.CurrentRevisionNumber,
                item.Request.SubmittedOnUtc,
                item.Request.RowVersion))
            .ToListAsync(cancellationToken);
        return (items, count);
    }

    public async Task ReplaceDoctorSpecializationsAsync(
        Guid doctorId,
        IReadOnlyCollection<DoctorSpecialization> replacements,
        CancellationToken cancellationToken)
    {
        var current = await dbContext.DoctorSpecializations.Where(item => item.DoctorId == doctorId).ToListAsync(cancellationToken);
        dbContext.DoctorSpecializations.RemoveRange(current);
        await dbContext.DoctorSpecializations.AddRangeAsync(replacements, cancellationToken);
    }

    public async Task<IReadOnlyList<LocationReferenceRecord>> ListGovernoratesAsync(CancellationToken cancellationToken)
        => await dbContext.Governorates.AsNoTracking().Where(item => item.IsActive)
            .OrderBy(item => item.DisplayOrder).ThenBy(item => item.NameAr)
            .Select(item => new LocationReferenceRecord(item.Id, item.NameAr, item.NameEn)).ToListAsync(cancellationToken);

    public Task<bool> ActiveGovernorateExistsAsync(int governorateId, CancellationToken cancellationToken)
        => dbContext.Governorates.AsNoTracking().AnyAsync(item => item.Id == governorateId && item.IsActive, cancellationToken);

    public async Task<IReadOnlyList<LocationReferenceRecord>> ListCitiesAsync(int governorateId, CancellationToken cancellationToken)
        => await dbContext.Cities.AsNoTracking().Where(item => item.GovernorateId == governorateId && item.IsActive)
            .OrderBy(item => item.DisplayOrder).ThenBy(item => item.NameAr)
            .Select(item => new LocationReferenceRecord(item.Id, item.NameAr, item.NameEn)).ToListAsync(cancellationToken);

    public Task<bool> ActiveCityExistsAsync(int cityId, CancellationToken cancellationToken)
        => dbContext.Cities.AsNoTracking().AnyAsync(item => item.Id == cityId && item.IsActive, cancellationToken);

    public async Task<IReadOnlyList<LocationReferenceRecord>> ListAreasAsync(int cityId, CancellationToken cancellationToken)
        => await dbContext.Areas.AsNoTracking().Where(item => item.CityId == cityId && item.IsActive)
            .OrderBy(item => item.DisplayOrder).ThenBy(item => item.NameAr)
            .Select(item => new LocationReferenceRecord(item.Id, item.NameAr, item.NameEn)).ToListAsync(cancellationToken);

    public Task<LocationHierarchyRecord?> FindLocationHierarchyAsync(int areaId, CancellationToken cancellationToken)
        => (from area in dbContext.Areas.AsNoTracking()
            join city in dbContext.Cities.AsNoTracking() on area.CityId equals city.Id
            join governorate in dbContext.Governorates.AsNoTracking() on city.GovernorateId equals governorate.Id
            where area.Id == areaId
            select new LocationHierarchyRecord(
                area.Id,
                city.Id,
                governorate.Id,
                area.IsActive,
                city.IsActive,
                governorate.IsActive)).SingleOrDefaultAsync(cancellationToken);

    public Task<DoctorPracticeLocation?> FindDoctorPracticeLocationAsync(Guid doctorId, CancellationToken cancellationToken)
        => dbContext.DoctorPracticeLocations.SingleOrDefaultAsync(item => item.DoctorId == doctorId, cancellationToken);

    public Task<DoctorPracticeLocationViewRecord?> GetDoctorPracticeLocationAsync(Guid doctorId, CancellationToken cancellationToken)
        => (from location in dbContext.DoctorPracticeLocations.AsNoTracking()
            join governorate in dbContext.Governorates.AsNoTracking() on location.GovernorateId equals governorate.Id
            join city in dbContext.Cities.AsNoTracking() on location.CityId equals city.Id
            join area in dbContext.Areas.AsNoTracking() on location.AreaId equals area.Id
            where location.DoctorId == doctorId
            select new DoctorPracticeLocationViewRecord(
                location,
                new LocationReferenceRecord(governorate.Id, governorate.NameAr, governorate.NameEn),
                new LocationReferenceRecord(city.Id, city.NameAr, city.NameEn),
                new LocationReferenceRecord(area.Id, area.NameAr, area.NameEn)))
            .SingleOrDefaultAsync(cancellationToken);

    public void Add<TEntity>(TEntity entity) where TEntity : class
        => dbContext.Set<TEntity>().Add(entity);

    public void Remove<TEntity>(TEntity entity) where TEntity : class
        => dbContext.Set<TEntity>().Remove(entity);

    public void SetOriginalRowVersion<TEntity>(TEntity entity, byte[] rowVersion) where TEntity : class
        => dbContext.Entry(entity).Property("RowVersion").OriginalValue = rowVersion;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        => dbContext.SaveChangesAsync(cancellationToken);
}
