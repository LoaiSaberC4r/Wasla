using Wasla.Domain.Common;
using Wasla.Domain.Doctors;
using Wasla.Domain.Patients;
using Wasla.Domain.Security;

namespace Wasla.Application.Persistence;

public sealed record UserAccessSnapshot(
    ApplicationUser User,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    Guid? DoctorId,
    DoctorApprovalStatus? DoctorStatus,
    Guid? PatientId,
    Guid? SuperAdminId,
    bool IsRootSuperAdmin);

public sealed record DoctorAdminRecord(Doctor Doctor, ApplicationUser User);

public sealed record SuperAdminRecord(SuperAdmin SuperAdmin, ApplicationUser User);

public interface IWaslaDataStore
{
    Task<bool> UserNameExistsAsync(string userName, Guid? excludingUserId, CancellationToken cancellationToken);
    Task<bool> EmailExistsAsync(string email, Guid? excludingUserId, CancellationToken cancellationToken);
    Task<ApplicationUser?> FindUserByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<ApplicationUser?> FindUserByIdentifierAsync(string identifier, CancellationToken cancellationToken);
    Task<UserAccessSnapshot?> GetAccessSnapshotAsync(Guid applicationUserId, CancellationToken cancellationToken);
    Task<Doctor?> FindDoctorByIdAsync(Guid doctorId, CancellationToken cancellationToken);
    Task<Doctor?> FindDoctorByUserIdAsync(Guid applicationUserId, CancellationToken cancellationToken);
    Task<Patient?> FindPatientByUserIdAsync(Guid applicationUserId, CancellationToken cancellationToken);
    Task<bool> NationalIdExistsAsync(string nationalId, Guid? excludingDoctorId, CancellationToken cancellationToken);
    Task<(IReadOnlyList<DoctorAdminRecord> Items, long TotalCount)> ListDoctorsAsync(
        DoctorApprovalStatus? approvalStatus,
        string? searchText,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);
    Task<SuperAdminRecord?> FindSuperAdminAsync(Guid superAdminId, bool includeDeleted, CancellationToken cancellationToken);
    Task<SuperAdmin?> FindSuperAdminByUserIdAsync(Guid applicationUserId, bool includeDeleted, CancellationToken cancellationToken);
    Task<(IReadOnlyList<SuperAdminRecord> Items, long TotalCount)> ListSuperAdminsAsync(
        string? searchText,
        int pageNumber,
        int pageSize,
        bool includeDeleted,
        CancellationToken cancellationToken);
    Task<Role?> FindRoleByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Role?> FindRoleByNameAsync(string name, CancellationToken cancellationToken);
    Task<IReadOnlyList<Role>> ListRolesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Permission>> ListPermissionsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<RolePermission>> ListRolePermissionsAsync(Guid roleId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Permission>> ListPermissionsByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
    Task<PasswordResetChallenge?> FindChallengeAsync(Guid id, CancellationToken cancellationToken);
    Task<PasswordResetChallenge?> FindLatestChallengeAsync(Guid applicationUserId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PasswordResetChallenge>> ListActiveChallengesAsync(Guid applicationUserId, CancellationToken cancellationToken);
    void Add<TEntity>(TEntity entity) where TEntity : class;
    void Remove<TEntity>(TEntity entity) where TEntity : class;
    void SetOriginalRowVersion<TEntity>(TEntity entity, byte[] rowVersion) where TEntity : class;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

