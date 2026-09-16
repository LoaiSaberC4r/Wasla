using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Doctors;
using Wasla.Domain.Patients;
using Wasla.Domain.Security;
using Wasla.Domain.ReferenceData;
using Wasla.Domain.Families;
using Wasla.Domain.Practices;
using Wasla.Domain.Reservations;

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
        var patientId = await dbContext.PatientAccountLinks.AsNoTracking()
            .Where(item => item.ApplicationUserId == applicationUserId)
            .Select(item => (Guid?)item.PatientId)
            .SingleOrDefaultAsync(cancellationToken);
        var receptionId = await dbContext.Receptions.AsNoTracking()
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
            receptionId,
            superAdmin?.Id,
            superAdmin?.IsRootSuperAdmin == true);
    }

    public Task<Doctor?> FindDoctorByIdAsync(Guid doctorId, CancellationToken cancellationToken)
        => dbContext.Doctors.SingleOrDefaultAsync(doctor => doctor.Id == doctorId, cancellationToken);

    public Task<Doctor?> FindDoctorByUserIdAsync(Guid applicationUserId, CancellationToken cancellationToken)
        => dbContext.Doctors.AsNoTracking()
            .SingleOrDefaultAsync(doctor => doctor.ApplicationUserId == applicationUserId, cancellationToken);

    public async Task<IReadOnlyList<DoctorQualification>> ListDoctorQualificationsAsync(
        Guid doctorId,
        CancellationToken cancellationToken)
        => await dbContext.DoctorQualifications.AsNoTracking()
            .Where(item => item.DoctorId == doctorId)
            .OrderBy(item => item.DisplayOrder)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

    public Task<DoctorQualification?> FindDoctorQualificationAsync(
        Guid qualificationId,
        CancellationToken cancellationToken)
        => dbContext.DoctorQualifications.SingleOrDefaultAsync(
            item => item.Id == qualificationId,
            cancellationToken);

    public Task<Patient?> FindPatientByUserIdAsync(Guid applicationUserId, CancellationToken cancellationToken)
        => (from link in dbContext.PatientAccountLinks
            join patient in dbContext.Patients on link.PatientId equals patient.Id
            where link.ApplicationUserId == applicationUserId
            select patient).SingleOrDefaultAsync(cancellationToken);

    public Task<Patient?> FindPatientByIdAsync(Guid patientId, CancellationToken cancellationToken)
        => dbContext.Patients.SingleOrDefaultAsync(patient => patient.Id == patientId, cancellationToken);

    public Task<PatientAccountLink?> FindPatientAccountLinkAsync(Guid applicationUserId, CancellationToken cancellationToken)
        => dbContext.PatientAccountLinks.AsNoTracking()
            .SingleOrDefaultAsync(link => link.ApplicationUserId == applicationUserId, cancellationToken);

    public Task<bool> HasUserRoleAsync(
        Guid applicationUserId,
        Guid roleId,
        CancellationToken cancellationToken)
        => dbContext.UserRoles.AsNoTracking().AnyAsync(
            item => item.ApplicationUserId == applicationUserId && item.RoleId == roleId,
            cancellationToken);

    public async Task<(IReadOnlyList<PatientSearchRecord> Items, long TotalCount)> SearchPatientsAsync(
        string? phoneNumber,
        string? name,
        DateOnly? dateOfBirth,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Patients.AsNoTracking();
        var normalizedPhone = phoneNumber?.Trim();
        var normalizedName = name?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedPhone))
        {
            query = query.Where(patient => patient.PhoneNumber != null && patient.PhoneNumber.Contains(normalizedPhone));
        }
        if (!string.IsNullOrWhiteSpace(normalizedName))
        {
            query = query.Where(patient => patient.NameAr.Contains(normalizedName) ||
                                           patient.NameEn != null && patient.NameEn.Contains(normalizedName));
        }
        if (dateOfBirth.HasValue)
        {
            query = query.Where(patient => patient.DateOfBirth == dateOfBirth.Value);
        }

        var total = await query.LongCountAsync(cancellationToken);
        var items = await query.OrderBy(patient => patient.NameAr)
            .ThenBy(patient => patient.DateOfBirth)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(patient => new PatientSearchRecord(
                patient.Id,
                patient.NameAr,
                patient.NameEn,
                patient.DateOfBirth,
                patient.Gender,
                patient.PhoneNumber,
                dbContext.PatientContacts.Any(contact => contact.PatientId == patient.Id && contact.PhoneNumber != "")))
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<IReadOnlyList<PatientContact>> ListPatientContactsAsync(Guid patientId, CancellationToken cancellationToken)
        => await dbContext.PatientContacts.AsNoTracking()
            .Where(contact => contact.PatientId == patientId)
            .OrderByDescending(contact => contact.IsPrimary)
            .ThenBy(contact => contact.NameAr)
            .ToListAsync(cancellationToken);

    public Task<PatientContact?> FindPatientContactAsync(Guid patientId, Guid contactId, CancellationToken cancellationToken)
        => dbContext.PatientContacts.SingleOrDefaultAsync(
            contact => contact.Id == contactId && contact.PatientId == patientId,
            cancellationToken);

    public Task<bool> HasUsablePrimaryContactAsync(Guid patientId, Guid? excludingContactId, CancellationToken cancellationToken)
        => dbContext.PatientContacts.AnyAsync(
            contact => contact.PatientId == patientId && contact.IsPrimary && contact.PhoneNumber != "" &&
                       (!excludingContactId.HasValue || contact.Id != excludingContactId.Value),
            cancellationToken);

    public Task<FamilyMember?> FindActiveFamilyMemberByPatientIdAsync(Guid patientId, CancellationToken cancellationToken)
        => dbContext.FamilyMembers.SingleOrDefaultAsync(
            member => member.PatientId == patientId && member.IsActive,
            cancellationToken);

    public Task<Family?> FindFamilyByIdAsync(Guid familyId, CancellationToken cancellationToken)
        => dbContext.Families.Include(family => family.Members)
            .SingleOrDefaultAsync(family => family.Id == familyId, cancellationToken);

    public async Task<IReadOnlyList<FamilyMemberViewRecord>> ListActiveFamilyMembersAsync(Guid familyId, CancellationToken cancellationToken)
        => await (from member in dbContext.FamilyMembers.AsNoTracking()
                  join patient in dbContext.Patients.AsNoTracking() on member.PatientId equals patient.Id
                  where member.FamilyId == familyId && member.IsActive
                  orderby member.Role, patient.NameAr
                  select new FamilyMemberViewRecord(member, patient)).ToListAsync(cancellationToken);

    public Task<FamilyRelationshipRequest?> FindOpenFamilyRelationshipRequestAsync(
        Guid requesterPatientId,
        Guid targetPatientId,
        FamilyRelationshipRequestType requestType,
        Guid? familyId,
        CancellationToken cancellationToken)
        => dbContext.FamilyRelationshipRequests.SingleOrDefaultAsync(
            item => item.RequesterPatientId == requesterPatientId &&
                    item.TargetPatientId == targetPatientId &&
                    item.RequestType == requestType && item.FamilyId == familyId &&
                    (item.Status == FamilyRelationshipRequestStatus.Pending ||
                     item.Status == FamilyRelationshipRequestStatus.ModificationRequested),
            cancellationToken);

    public Task<FamilyRelationshipRequestRecord?> FindFamilyRelationshipRequestAsync(Guid requestId, CancellationToken cancellationToken)
        => (from item in dbContext.FamilyRelationshipRequests
            join requester in dbContext.Patients on item.RequesterPatientId equals requester.Id
            join target in dbContext.Patients on item.TargetPatientId equals target.Id
            join submitter in dbContext.ApplicationUsers on item.SubmittedByApplicationUserId equals submitter.Id
            where item.Id == requestId
            select new FamilyRelationshipRequestRecord(item, requester, target, submitter))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<(IReadOnlyList<FamilyRelationshipRequestQueueRecord> Items, long TotalCount)> ListFamilyRelationshipRequestsAsync(
        FamilyRelationshipRequestStatus? status,
        FamilyRelationshipRequestType? requestType,
        string? search,
        Guid? submittedByApplicationUserId,
        Guid? relatedPatientId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = from item in dbContext.FamilyRelationshipRequests.AsNoTracking()
                    join requester in dbContext.Patients.AsNoTracking() on item.RequesterPatientId equals requester.Id
                    join target in dbContext.Patients.AsNoTracking() on item.TargetPatientId equals target.Id
                    select new { item, requester, target };
        if (status.HasValue)
        {
            query = query.Where(row => row.item.Status == status.Value);
        }
        if (requestType.HasValue)
        {
            query = query.Where(row => row.item.RequestType == requestType.Value);
        }
        if (submittedByApplicationUserId.HasValue)
        {
            query = query.Where(row => row.item.SubmittedByApplicationUserId == submittedByApplicationUserId.Value);
        }
        if (relatedPatientId.HasValue)
        {
            query = query.Where(row => row.item.RequesterPatientId == relatedPatientId.Value || row.item.TargetPatientId == relatedPatientId.Value);
        }
        var normalizedSearch = search?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            query = query.Where(row => row.requester.NameAr.Contains(normalizedSearch) ||
                                       row.target.NameAr.Contains(normalizedSearch) ||
                                       row.requester.NameEn != null && row.requester.NameEn.Contains(normalizedSearch) ||
                                       row.target.NameEn != null && row.target.NameEn.Contains(normalizedSearch));
        }

        var total = await query.LongCountAsync(cancellationToken);
        var items = await query.OrderByDescending(row => row.item.SubmittedOnUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(row => new FamilyRelationshipRequestQueueRecord(
                row.item.Id, row.item.RequestType, row.item.Status, row.requester.Id, row.requester.NameAr,
                row.target.Id, row.target.NameAr, row.item.RequesterClaimedRole, row.item.TargetClaimedRole,
                row.item.CurrentRevisionNumber, row.item.SubmittedOnUtc, row.item.RowVersion))
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<IReadOnlyList<FamilyRelationshipDocument>> ListFamilyRelationshipDocumentsAsync(Guid requestId, CancellationToken cancellationToken)
        => await dbContext.FamilyRelationshipDocuments.AsNoTracking()
            .Where(document => document.FamilyRelationshipRequestId == requestId)
            .OrderBy(document => document.RevisionNumber)
            .ThenBy(document => document.UploadedOnUtc)
            .ToListAsync(cancellationToken);

    public Task<FamilyRelationshipDocument?> FindFamilyRelationshipDocumentAsync(Guid requestId, Guid documentId, CancellationToken cancellationToken)
        => dbContext.FamilyRelationshipDocuments.AsNoTracking().SingleOrDefaultAsync(
            document => document.Id == documentId && document.FamilyRelationshipRequestId == requestId,
            cancellationToken);

    public async Task<IReadOnlyList<FamilyRelationshipRequestHistory>> ListFamilyRelationshipRequestHistoryAsync(Guid requestId, CancellationToken cancellationToken)
        => await dbContext.FamilyRelationshipRequestHistories.AsNoTracking()
            .Where(history => history.RequestId == requestId)
            .OrderBy(history => history.PerformedOnUtc)
            .ToListAsync(cancellationToken);

    public void MarkFamilyMembershipChanged(Family family)
    {
        ArgumentNullException.ThrowIfNull(family);
        dbContext.Entry(family).Property(item => item.Status).IsModified = true;
    }

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

    public Task<DoctorPractice?> FindDoctorPracticeLocationAsync(Guid doctorId, CancellationToken cancellationToken)
        => dbContext.DoctorPractices.SingleOrDefaultAsync(
            item => item.DoctorId == doctorId && item.IsLegacyOnboarding,
            cancellationToken);

    public Task<DoctorPracticeLocationViewRecord?> GetDoctorPracticeLocationAsync(Guid doctorId, CancellationToken cancellationToken)
        => (from location in dbContext.DoctorPracticeLocations.AsNoTracking()
            join governorate in dbContext.Governorates.AsNoTracking() on location.GovernorateId equals governorate.Id
            join city in dbContext.Cities.AsNoTracking() on location.CityId equals city.Id
            join area in dbContext.Areas.AsNoTracking() on location.AreaId equals area.Id
            where location.DoctorId == doctorId && location.IsLegacyOnboarding
            select new DoctorPracticeLocationViewRecord(
                location,
                new LocationReferenceRecord(governorate.Id, governorate.NameAr, governorate.NameEn),
                new LocationReferenceRecord(city.Id, city.NameAr, city.NameEn),
                new LocationReferenceRecord(area.Id, area.NameAr, area.NameEn)))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<DoctorPracticeViewRecord>> ListDoctorPracticesAsync(
        Guid doctorId,
        CancellationToken cancellationToken)
        => await (from practice in dbContext.DoctorPractices.AsNoTracking()
            join governorate in dbContext.Governorates.AsNoTracking() on practice.GovernorateId equals governorate.Id
            join city in dbContext.Cities.AsNoTracking() on practice.CityId equals city.Id
            join area in dbContext.Areas.AsNoTracking() on practice.AreaId equals area.Id
            where practice.DoctorId == doctorId
            orderby practice.NameAr, practice.Id
            select new DoctorPracticeViewRecord(
                practice,
                new LocationReferenceRecord(governorate.Id, governorate.NameAr, governorate.NameEn),
                new LocationReferenceRecord(city.Id, city.NameAr, city.NameEn),
                new LocationReferenceRecord(area.Id, area.NameAr, area.NameEn)))
            .ToListAsync(cancellationToken);

    public Task<DoctorPractice?> FindDoctorPracticeAsync(Guid practiceId, CancellationToken cancellationToken)
        => dbContext.DoctorPractices.SingleOrDefaultAsync(item => item.Id == practiceId, cancellationToken);

    public Task<DoctorPracticeViewRecord?> GetDoctorPracticeAsync(
        Guid practiceId,
        CancellationToken cancellationToken)
        => (from practice in dbContext.DoctorPractices.AsNoTracking()
            join governorate in dbContext.Governorates.AsNoTracking() on practice.GovernorateId equals governorate.Id
            join city in dbContext.Cities.AsNoTracking() on practice.CityId equals city.Id
            join area in dbContext.Areas.AsNoTracking() on practice.AreaId equals area.Id
            where practice.Id == practiceId
            select new DoctorPracticeViewRecord(
                practice,
                new LocationReferenceRecord(governorate.Id, governorate.NameAr, governorate.NameEn),
                new LocationReferenceRecord(city.Id, city.NameAr, city.NameEn),
                new LocationReferenceRecord(area.Id, area.NameAr, area.NameEn)))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<DoctorPracticeConfiguration?> FindDoctorPracticeConfigurationAsync(
        Guid practiceId,
        CancellationToken cancellationToken)
        => dbContext.DoctorPracticeConfigurations.SingleOrDefaultAsync(
            item => item.DoctorPracticeId == practiceId,
            cancellationToken);

    public Task<DoctorPracticeBranding?> FindDoctorPracticeBrandingAsync(
        Guid practiceId,
        CancellationToken cancellationToken)
        => dbContext.DoctorPracticeBrandings.SingleOrDefaultAsync(
            item => item.DoctorPracticeId == practiceId,
            cancellationToken);

    public async Task<IReadOnlyList<DoctorPracticeSchedulePeriod>> ListDoctorPracticeSchedulePeriodsAsync(
        Guid practiceId,
        CancellationToken cancellationToken)
        => await dbContext.DoctorPracticeSchedulePeriods.AsNoTracking()
            .Where(item => item.DoctorPracticeId == practiceId)
            .OrderBy(item => item.DayOfWeek)
            .ThenBy(item => item.StartTime)
            .ToListAsync(cancellationToken);

    public Task<DoctorPracticeSchedulePeriod?> FindDoctorPracticeSchedulePeriodAsync(
        Guid periodId,
        CancellationToken cancellationToken)
        => dbContext.DoctorPracticeSchedulePeriods.SingleOrDefaultAsync(item => item.Id == periodId, cancellationToken);

    public Task<bool> HasDoctorScheduleOverlapAsync(
        Guid doctorId,
        Guid practiceId,
        Guid? excludingPeriodId,
        DayOfWeek dayOfWeek,
        TimeOnly startTime,
        TimeOnly endTime,
        CancellationToken cancellationToken)
        => (from period in dbContext.DoctorPracticeSchedulePeriods.AsNoTracking()
            join practice in dbContext.DoctorPractices.AsNoTracking()
                on period.DoctorPracticeId equals practice.Id
            where practice.DoctorId == doctorId &&
                  period.DayOfWeek == dayOfWeek &&
                  period.StartTime < endTime && startTime < period.EndTime &&
                  (!excludingPeriodId.HasValue || period.Id != excludingPeriodId.Value)
            select period.Id).AnyAsync(cancellationToken);

    public async Task AcquireDoctorScheduleLockAsync(Guid doctorId, CancellationToken cancellationToken)
    {
        if (!string.Equals(
                dbContext.Database.ProviderName,
                "Microsoft.EntityFrameworkCore.SqlServer",
                StringComparison.Ordinal))
        {
            return;
        }

        var resource = $"Wasla:DoctorSchedule:{doctorId:N}";
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @result < 0 THROW 51001, 'Could not acquire the doctor schedule lock.', 1;",
            cancellationToken);
    }

    public async Task<IReadOnlyList<DoctorPracticeScheduleException>> ListDoctorPracticeScheduleExceptionsAsync(
        Guid practiceId,
        CancellationToken cancellationToken)
        => await dbContext.DoctorPracticeScheduleExceptions.AsNoTracking()
            .Where(item => item.DoctorPracticeId == practiceId)
            .OrderBy(item => item.Date)
            .ThenBy(item => item.StartTime)
            .ToListAsync(cancellationToken);

    public Task<DoctorPracticeScheduleException?> FindDoctorPracticeScheduleExceptionAsync(
        Guid exceptionId,
        CancellationToken cancellationToken)
        => dbContext.DoctorPracticeScheduleExceptions.SingleOrDefaultAsync(
            item => item.Id == exceptionId,
            cancellationToken);

    public async Task<IReadOnlyList<DoctorPracticeSegment>> ListDoctorPracticeSegmentsAsync(
        Guid practiceId,
        CancellationToken cancellationToken)
        => await dbContext.DoctorPracticeSegments.AsNoTracking()
            .Where(item => item.DoctorPracticeId == practiceId)
            .OrderByDescending(item => item.Priority)
            .ThenBy(item => item.NameAr)
            .ToListAsync(cancellationToken);

    public Task<DoctorPracticeSegment?> FindDoctorPracticeSegmentAsync(
        Guid segmentId,
        CancellationToken cancellationToken)
        => dbContext.DoctorPracticeSegments.SingleOrDefaultAsync(item => item.Id == segmentId, cancellationToken);

    public async Task<IReadOnlyList<DoctorPracticeVisitType>> ListDoctorPracticeVisitTypesAsync(
        Guid practiceId,
        CancellationToken cancellationToken)
        => await dbContext.DoctorPracticeVisitTypes.AsNoTracking()
            .Where(item => item.DoctorPracticeId == practiceId)
            .OrderBy(item => item.Type)
            .ToListAsync(cancellationToken);

    public Task<DoctorPracticeVisitType?> FindDoctorPracticeVisitTypeAsync(
        Guid visitTypeId,
        CancellationToken cancellationToken)
        => dbContext.DoctorPracticeVisitTypes.SingleOrDefaultAsync(item => item.Id == visitTypeId, cancellationToken);

    public async Task<IReadOnlyList<DoctorPracticeSegmentVisitTypePrice>> ListDoctorPracticePricesAsync(
        Guid practiceId,
        CancellationToken cancellationToken)
        => await dbContext.DoctorPracticeSegmentVisitTypePrices.AsNoTracking()
            .Where(item => item.DoctorPracticeId == practiceId)
            .OrderBy(item => item.SegmentId)
            .ThenBy(item => item.VisitTypeId)
            .ToListAsync(cancellationToken);

    public Task<DoctorPracticeSegmentVisitTypePrice?> FindDoctorPracticePriceAsync(
        Guid priceId,
        CancellationToken cancellationToken)
        => dbContext.DoctorPracticeSegmentVisitTypePrices.SingleOrDefaultAsync(
            item => item.Id == priceId,
            cancellationToken);

    public Task<bool> DoctorPracticePriceExistsAsync(
        Guid practiceId,
        Guid segmentId,
        Guid visitTypeId,
        Guid? excludingPriceId,
        CancellationToken cancellationToken)
        => dbContext.DoctorPracticeSegmentVisitTypePrices.AsNoTracking().AnyAsync(
            item => item.DoctorPracticeId == practiceId && item.SegmentId == segmentId &&
                    item.VisitTypeId == visitTypeId &&
                    (!excludingPriceId.HasValue || item.Id != excludingPriceId.Value),
            cancellationToken);

    public async Task<IReadOnlyList<ReceptionViewRecord>> ListDoctorReceptionsAsync(
        Guid doctorId,
        CancellationToken cancellationToken)
        => await (from reception in dbContext.Receptions.AsNoTracking()
            join user in dbContext.ApplicationUsers.AsNoTracking() on reception.ApplicationUserId equals user.Id
            where reception.OwnerDoctorId == doctorId
            orderby reception.NameAr
            select new ReceptionViewRecord(reception, user)).ToListAsync(cancellationToken);

    public Task<ReceptionViewRecord?> FindDoctorReceptionAsync(
        Guid receptionId,
        CancellationToken cancellationToken)
        => (from reception in dbContext.Receptions
            join user in dbContext.ApplicationUsers on reception.ApplicationUserId equals user.Id
            where reception.Id == receptionId
            select new ReceptionViewRecord(reception, user)).SingleOrDefaultAsync(cancellationToken);

    public Task<Reception?> FindReceptionByApplicationUserIdAsync(
        Guid applicationUserId,
        CancellationToken cancellationToken)
        => dbContext.Receptions.SingleOrDefaultAsync(
            item => item.ApplicationUserId == applicationUserId,
            cancellationToken);

    public async Task<IReadOnlyList<ReceptionPracticeAssignment>> ListReceptionAssignmentsAsync(
        Guid receptionId,
        CancellationToken cancellationToken)
        => await dbContext.ReceptionPracticeAssignments.AsNoTracking()
            .Where(item => item.ReceptionId == receptionId)
            .OrderBy(item => item.CreatedOnUtc)
            .ToListAsync(cancellationToken);

    public Task<ReceptionPracticeAssignment?> FindReceptionAssignmentAsync(
        Guid assignmentId,
        CancellationToken cancellationToken)
        => dbContext.ReceptionPracticeAssignments.SingleOrDefaultAsync(
            item => item.Id == assignmentId,
            cancellationToken);

    public Task<bool> ReceptionAssignmentExistsAsync(
        Guid receptionId,
        Guid practiceId,
        CancellationToken cancellationToken)
        => dbContext.ReceptionPracticeAssignments.AsNoTracking().AnyAsync(
            item => item.ReceptionId == receptionId && item.DoctorPracticeId == practiceId,
            cancellationToken);

    public async Task<IReadOnlyList<ReceptionPracticeAssignmentPermission>> ListReceptionAssignmentPermissionsAsync(
        Guid assignmentId,
        CancellationToken cancellationToken)
        => await dbContext.ReceptionPracticeAssignmentPermissions.AsNoTracking()
            .Where(item => item.AssignmentId == assignmentId)
            .OrderBy(item => item.PermissionId)
            .ToListAsync(cancellationToken);

    public async Task ReplaceReceptionAssignmentPermissionsAsync(
        Guid assignmentId,
        IReadOnlyCollection<ReceptionPracticeAssignmentPermission> replacements,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.ReceptionPracticeAssignmentPermissions
            .Where(item => item.AssignmentId == assignmentId)
            .ToListAsync(cancellationToken);
        dbContext.ReceptionPracticeAssignmentPermissions.RemoveRange(existing);
        await dbContext.ReceptionPracticeAssignmentPermissions.AddRangeAsync(replacements, cancellationToken);
    }

    public Task<bool> HasReceptionPracticeAccessAsync(
        Guid applicationUserId,
        Guid practiceId,
        string permissionName,
        CancellationToken cancellationToken)
        => (from user in dbContext.ApplicationUsers.AsNoTracking()
            join reception in dbContext.Receptions.AsNoTracking()
                on user.Id equals reception.ApplicationUserId
            join doctor in dbContext.Doctors.AsNoTracking()
                on reception.OwnerDoctorId equals doctor.Id
            join doctorUser in dbContext.ApplicationUsers.AsNoTracking()
                on doctor.ApplicationUserId equals doctorUser.Id
            join practice in dbContext.DoctorPractices.AsNoTracking()
                on doctor.Id equals practice.DoctorId
            join assignment in dbContext.ReceptionPracticeAssignments.AsNoTracking()
                on new { ReceptionId = reception.Id, DoctorPracticeId = practice.Id }
                equals new { assignment.ReceptionId, assignment.DoctorPracticeId }
            join assignmentPermission in dbContext.ReceptionPracticeAssignmentPermissions.AsNoTracking()
                on assignment.Id equals assignmentPermission.AssignmentId
            join permission in dbContext.Permissions.AsNoTracking()
                on assignmentPermission.PermissionId equals permission.Id
            where user.Id == applicationUserId && user.UserType == UserType.Reception &&
                  user.IsActive && !user.IsFirstLogin && doctorUser.IsActive &&
                  doctor.ApprovalStatus == DoctorApprovalStatus.Approved &&
                  practice.Id == practiceId && practice.IsActive && assignment.IsActive &&
                  permission.Name == permissionName &&
                  (from userRole in dbContext.UserRoles.AsNoTracking()
                   join rolePermission in dbContext.RolePermissions.AsNoTracking()
                       on userRole.RoleId equals rolePermission.RoleId
                   where userRole.ApplicationUserId == user.Id &&
                         rolePermission.PermissionId == permission.Id
                   select userRole.ApplicationUserId).Any()
            select assignment.Id).AnyAsync(cancellationToken);

    public Task<Reservation?> FindReservationAsync(Guid reservationId, CancellationToken cancellationToken)
        => dbContext.Reservations
            .Include(item => item.History)
            .SingleOrDefaultAsync(item => item.Id == reservationId, cancellationToken);

    public Task<ReservationViewRecord?> GetReservationViewAsync(
        Guid reservationId,
        CancellationToken cancellationToken)
        => (from reservation in dbContext.Reservations.AsNoTracking().Include(item => item.History)
            join patient in dbContext.Patients.AsNoTracking() on reservation.PatientId equals patient.Id
            join doctor in dbContext.Doctors.AsNoTracking() on reservation.DoctorId equals doctor.Id
            join practice in dbContext.DoctorPractices.AsNoTracking() on reservation.DoctorPracticeId equals practice.Id
            where reservation.Id == reservationId
            select new ReservationViewRecord(reservation, patient, doctor, practice))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<ReservationViewPage> ListReservationViewsAsync(
        IReadOnlyCollection<Guid>? patientIds,
        Guid? doctorId,
        Guid? practiceId,
        ReservationStatus? status,
        ReservationBookingSource? bookingSource,
        DateOnly? fromDate,
        DateOnly? toDate,
        Guid? segmentId,
        bool? isLate,
        DateTime lateThresholdUtc,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query =
            from reservation in dbContext.Reservations.AsNoTracking()
            join patient in dbContext.Patients.AsNoTracking() on reservation.PatientId equals patient.Id
            join doctor in dbContext.Doctors.AsNoTracking() on reservation.DoctorId equals doctor.Id
            join practice in dbContext.DoctorPractices.AsNoTracking() on reservation.DoctorPracticeId equals practice.Id
            select new ReservationViewRecord(reservation, patient, doctor, practice);

        if (patientIds is { Count: > 0 })
        {
            var ids = patientIds.Distinct().ToArray();
            query = query.Where(item => ids.Contains(item.Reservation.PatientId));
        }

        if (doctorId.HasValue)
        {
            query = query.Where(item => item.Reservation.DoctorId == doctorId.Value);
        }

        if (practiceId.HasValue)
        {
            query = query.Where(item => item.Reservation.DoctorPracticeId == practiceId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(item => item.Reservation.Status == status.Value);
        }

        if (bookingSource.HasValue)
        {
            query = query.Where(item => item.Reservation.BookingSource == bookingSource.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(item => item.Reservation.BusinessDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(item => item.Reservation.BusinessDate <= toDate.Value);
        }

        if (segmentId.HasValue)
        {
            query = query.Where(item => item.Reservation.SegmentId == segmentId.Value);
        }

        if (isLate.HasValue)
        {
            query = isLate.Value
                ? query.Where(item => item.Reservation.Status == ReservationStatus.Active &&
                                      item.Reservation.ScheduledStartUtc < lateThresholdUtc)
                : query.Where(item => item.Reservation.Status != ReservationStatus.Active ||
                                      item.Reservation.ScheduledStartUtc >= lateThresholdUtc);
        }

        var normalizedSearch = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        if (normalizedSearch is not null)
        {
            query = query.Where(item =>
                item.Reservation.ReservationReference.Contains(normalizedSearch) ||
                item.Patient.NameAr.Contains(normalizedSearch) ||
                item.Patient.NameEn != null && item.Patient.NameEn.Contains(normalizedSearch) ||
                item.Patient.PhoneNumber != null && item.Patient.PhoneNumber.Contains(normalizedSearch));
        }

        var summary = await query
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Total = group.LongCount(),
                Active = group.LongCount(item => item.Reservation.Status == ReservationStatus.Active),
                Late = group.LongCount(item => item.Reservation.Status == ReservationStatus.Active &&
                                               item.Reservation.ScheduledStartUtc < lateThresholdUtc),
                NoShow = group.LongCount(item => item.Reservation.Status == ReservationStatus.NoShow),
                Cancelled = group.LongCount(item => item.Reservation.Status == ReservationStatus.Cancelled),
                Converted = group.LongCount(item => item.Reservation.Status == ReservationStatus.ConvertedToTicket),
                Expired = group.LongCount(item => item.Reservation.Status == ReservationStatus.Expired)
            })
            .SingleOrDefaultAsync(cancellationToken);
        var items = await query
            .OrderBy(item => item.Reservation.ScheduledStartUtc)
            .ThenBy(item => item.Reservation.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);
        return new ReservationViewPage(
            items,
            summary?.Total ?? 0,
            summary?.Active ?? 0,
            summary?.Late ?? 0,
            summary?.NoShow ?? 0,
            summary?.Cancelled ?? 0,
            summary?.Converted ?? 0,
            summary?.Expired ?? 0);
    }

    public async Task<ReservationConflictSnapshot> GetReservationConflictSnapshotAsync(
        Guid patientId,
        Guid doctorId,
        Guid practiceId,
        Guid segmentId,
        DateOnly businessDate,
        DateTime scheduledStartUtc,
        DateTime scheduledEndUtc,
        DateTime utcNow,
        Guid? excludingReservationId,
        CancellationToken cancellationToken)
    {
        var excluding = excludingReservationId ?? Guid.Empty;
        var consuming = dbContext.Reservations.AsNoTracking().Where(item =>
            item.Id != excluding &&
            (item.Status == ReservationStatus.Active || item.Status == ReservationStatus.ConvertedToTicket));
        var slotOccupied = await consuming.AnyAsync(item =>
            item.DoctorPracticeId == practiceId && item.ScheduledStartUtc == scheduledStartUtc,
            cancellationToken);
        var dayQuery = consuming.Where(item =>
            item.DoctorPracticeId == practiceId && item.BusinessDate == businessDate);
        var consumedDailyCapacity = await dayQuery.CountAsync(cancellationToken);
        var segmentCounts = await dayQuery
            .GroupBy(item => item.SegmentId)
            .Select(group => new { SegmentId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.SegmentId, item => item.Count, cancellationToken);

        var active = dbContext.Reservations.AsNoTracking().Where(item =>
            item.Id != excluding && item.Status == ReservationStatus.Active && item.PatientId == patientId);
        var practiceDateConflict = await active.AnyAsync(item =>
            item.DoctorPracticeId == practiceId && item.BusinessDate == businessDate,
            cancellationToken);
        var overlap = await active.AnyAsync(item =>
            item.ScheduledStartUtc < scheduledEndUtc &&
            scheduledStartUtc < item.ScheduledStartUtc.AddMinutes(item.SlotDurationMinutesSnapshot),
            cancellationToken);
        var futureConsultation = await active.AnyAsync(item =>
            item.DoctorId == doctorId &&
            item.VisitTypeCodeSnapshot == DoctorPracticeVisitTypeCode.NewConsultation.ToString() &&
            item.ScheduledStartUtc > utcNow,
            cancellationToken);
        var sameDayNoShow = await dbContext.Reservations.AsNoTracking().AnyAsync(item =>
            item.Id != excluding && item.PatientId == patientId && item.DoctorPracticeId == practiceId &&
            item.BusinessDate == businessDate && item.Status == ReservationStatus.NoShow,
            cancellationToken);
        return new ReservationConflictSnapshot(
            slotOccupied,
            consumedDailyCapacity,
            segmentCounts,
            practiceDateConflict,
            overlap,
            futureConsultation,
            sameDayNoShow);
    }

    public Task<bool> ReservationReferenceExistsAsync(
        string reservationReference,
        CancellationToken cancellationToken)
        => dbContext.Reservations.AsNoTracking().AnyAsync(
            item => item.ReservationReference == reservationReference,
            cancellationToken);

    public Task<ReservationIdempotencyRecord?> FindReservationIdempotencyAsync(
        Guid actorApplicationUserId,
        string operation,
        string idempotencyKey,
        CancellationToken cancellationToken)
        => dbContext.ReservationIdempotencyRecords.SingleOrDefaultAsync(item =>
            item.ActorApplicationUserId == actorApplicationUserId &&
            item.Operation == operation && item.IdempotencyKey == idempotencyKey,
            cancellationToken);

    public async Task AcquireReservationLocksAsync(
        Guid patientId,
        Guid practiceId,
        DateOnly businessDate,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(
                dbContext.Database.ProviderName,
                "Microsoft.EntityFrameworkCore.SqlServer",
                StringComparison.Ordinal))
        {
            return;
        }

        // Patient first, then Practice/date is the deterministic lock order for every mutation.
        var patientResource = $"Wasla:Reservation:Patient:{patientId:N}";
        var practiceResource = $"Wasla:Reservation:Practice:{practiceId:N}:{businessDate:yyyyMMdd}";
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"DECLARE @r int; EXEC @r = sys.sp_getapplock @Resource={patientResource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @r < 0 THROW 51002, 'Could not acquire reservation patient lock.', 1;",
            cancellationToken);
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"DECLARE @r int; EXEC @r = sys.sp_getapplock @Resource={practiceResource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @r < 0 THROW 51003, 'Could not acquire reservation practice lock.', 1;",
            cancellationToken);
    }

    public async Task AcquireReservationIdempotencyLockAsync(
        Guid actorApplicationUserId,
        string operation,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(
                dbContext.Database.ProviderName,
                "Microsoft.EntityFrameworkCore.SqlServer",
                StringComparison.Ordinal))
        {
            return;
        }

        var keyHash = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(idempotencyKey.Trim())));
        var resource = $"Wasla:Reservation:Idempotency:{actorApplicationUserId:N}:{operation}:{keyHash}";
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"DECLARE @r int; EXEC @r = sys.sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @r < 0 THROW 51004, 'Could not acquire reservation idempotency lock.', 1;",
            cancellationToken);
    }

    public async Task<IReadOnlyList<Reservation>> ListDueActiveReservationsAsync(
        DateTime utcNow,
        int batchSize,
        CancellationToken cancellationToken)
        => await dbContext.Reservations
            .Include(item => item.History)
            .Where(item => item.Status == ReservationStatus.Active && item.ScheduledStartUtc < utcNow)
            .OrderBy(item => item.ScheduledStartUtc)
            .Take(batchSize)
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyList<Reservation>> ListFutureActiveReservationsByDoctorAsync(
        Guid doctorId,
        DateTime utcNow,
        CancellationToken cancellationToken)
        => await dbContext.Reservations
            .Include(item => item.History)
            .Where(item => item.DoctorId == doctorId && item.Status == ReservationStatus.Active &&
                           item.ScheduledStartUtc > utcNow)
            .OrderBy(item => item.ScheduledStartUtc)
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyList<Reservation>> ListFutureActiveReservationsByPracticeAsync(
        Guid practiceId,
        DateTime utcNow,
        CancellationToken cancellationToken)
        => await dbContext.Reservations
            .Include(item => item.History)
            .Where(item => item.DoctorPracticeId == practiceId && item.Status == ReservationStatus.Active &&
                           item.ScheduledStartUtc > utcNow)
            .OrderBy(item => item.ScheduledStartUtc)
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyList<ReservationPatientRecord>> ListFutureActiveReservationPatientsByPracticeAsync(
        Guid practiceId,
        DateTime utcNow,
        CancellationToken cancellationToken)
        => await (from reservation in dbContext.Reservations.AsNoTracking()
                  join patient in dbContext.Patients.AsNoTracking()
                      on reservation.PatientId equals patient.Id
                  where reservation.DoctorPracticeId == practiceId &&
                        reservation.Status == ReservationStatus.Active &&
                        reservation.ScheduledStartUtc > utcNow
                  orderby reservation.ScheduledStartUtc
                  select new ReservationPatientRecord(reservation, patient))
            .ToArrayAsync(cancellationToken);

    public void Add<TEntity>(TEntity entity) where TEntity : class
        => dbContext.Set<TEntity>().Add(entity);

    public void Remove<TEntity>(TEntity entity) where TEntity : class
        => dbContext.Set<TEntity>().Remove(entity);

    public void SetOriginalRowVersion<TEntity>(TEntity entity, byte[] rowVersion) where TEntity : class
        => dbContext.Entry(entity).Property("RowVersion").OriginalValue = rowVersion;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        => dbContext.SaveChangesAsync(cancellationToken);
}
