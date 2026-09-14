using Wasla.Domain.Common;
using Wasla.Domain.Doctors;
using Wasla.Domain.Patients;
using Wasla.Domain.Security;
using Wasla.Domain.ReferenceData;
using Wasla.Domain.Families;
using Wasla.Domain.Practices;

namespace Wasla.Application.Persistence;

public sealed record UserAccessSnapshot(
    ApplicationUser User,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    Guid? DoctorId,
    DoctorApprovalStatus? DoctorStatus,
    Guid? PatientId,
    Guid? ReceptionId,
    Guid? SuperAdminId,
    bool IsRootSuperAdmin);

public sealed record DoctorAdminRecord(Doctor Doctor, ApplicationUser User);

public sealed record SuperAdminRecord(SuperAdmin SuperAdmin, ApplicationUser User);

public sealed record MedicalSpecializationOptionRecord(Guid Id, string NameAr, string? NameEn);
public sealed record DoctorSpecializationViewRecord(Guid MedicalSpecializationId, string NameAr, string? NameEn, bool IsPrimary);
public sealed record DoctorSpecializationRequestItemViewRecord(Guid MedicalSpecializationId, string NameAr, string? NameEn, bool IsPrimary);
public sealed record DoctorSpecializationRequestQueueRecord(
    Guid RequestId,
    Guid DoctorId,
    string DoctorNameAr,
    string? DoctorNameEn,
    string Email,
    DoctorSpecializationRequestType Type,
    DoctorSpecializationRequestStatus Status,
    int CurrentRevisionNumber,
    DateTime SubmittedOnUtc,
    byte[] RowVersion);
public sealed record DoctorSpecializationRequestOwnerRecord(
    DoctorSpecializationRequest Request,
    Doctor Doctor,
    ApplicationUser User);
public sealed record LocationReferenceRecord(int Id, string NameAr, string? NameEn);
public sealed record LocationHierarchyRecord(
    int AreaId,
    int CityId,
    int GovernorateId,
    bool AreaIsActive,
    bool CityIsActive,
    bool GovernorateIsActive);
public sealed record DoctorPracticeLocationViewRecord(
    DoctorPractice Location,
    LocationReferenceRecord Governorate,
    LocationReferenceRecord City,
    LocationReferenceRecord Area);
public sealed record DoctorPracticeViewRecord(
    DoctorPractice Practice,
    LocationReferenceRecord Governorate,
    LocationReferenceRecord City,
    LocationReferenceRecord Area);
public sealed record ReceptionViewRecord(Reception Reception, ApplicationUser User);
public sealed record PatientSearchRecord(
    Guid PatientId,
    string NameAr,
    string? NameEn,
    DateOnly DateOfBirth,
    Gender Gender,
    string? PhoneNumber,
    bool HasContactPhone);
public sealed record FamilyMemberViewRecord(FamilyMember Member, Patient Patient);
public sealed record FamilyRelationshipRequestRecord(
    FamilyRelationshipRequest Request,
    Patient Requester,
    Patient Target,
    ApplicationUser Submitter);
public sealed record FamilyRelationshipRequestQueueRecord(
    Guid RequestId,
    FamilyRelationshipRequestType RequestType,
    FamilyRelationshipRequestStatus Status,
    Guid RequesterPatientId,
    string RequesterNameAr,
    Guid TargetPatientId,
    string TargetNameAr,
    FamilyMemberRole RequesterClaimedRole,
    FamilyMemberRole TargetClaimedRole,
    int CurrentRevisionNumber,
    DateTime SubmittedOnUtc,
    byte[] RowVersion);

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
    Task<Patient?> FindPatientByIdAsync(Guid patientId, CancellationToken cancellationToken);
    Task<PatientAccountLink?> FindPatientAccountLinkAsync(Guid applicationUserId, CancellationToken cancellationToken);
    Task<(IReadOnlyList<PatientSearchRecord> Items, long TotalCount)> SearchPatientsAsync(
        string? phoneNumber,
        string? name,
        DateOnly? dateOfBirth,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<PatientContact>> ListPatientContactsAsync(Guid patientId, CancellationToken cancellationToken);
    Task<PatientContact?> FindPatientContactAsync(Guid patientId, Guid contactId, CancellationToken cancellationToken);
    Task<bool> HasUsablePrimaryContactAsync(Guid patientId, Guid? excludingContactId, CancellationToken cancellationToken);
    Task<FamilyMember?> FindActiveFamilyMemberByPatientIdAsync(Guid patientId, CancellationToken cancellationToken);
    Task<Family?> FindFamilyByIdAsync(Guid familyId, CancellationToken cancellationToken);
    Task<IReadOnlyList<FamilyMemberViewRecord>> ListActiveFamilyMembersAsync(Guid familyId, CancellationToken cancellationToken);
    Task<FamilyRelationshipRequest?> FindOpenFamilyRelationshipRequestAsync(
        Guid requesterPatientId,
        Guid targetPatientId,
        FamilyRelationshipRequestType requestType,
        Guid? familyId,
        CancellationToken cancellationToken);
    Task<FamilyRelationshipRequestRecord?> FindFamilyRelationshipRequestAsync(Guid requestId, CancellationToken cancellationToken);
    Task<(IReadOnlyList<FamilyRelationshipRequestQueueRecord> Items, long TotalCount)> ListFamilyRelationshipRequestsAsync(
        FamilyRelationshipRequestStatus? status,
        FamilyRelationshipRequestType? requestType,
        string? search,
        Guid? submittedByApplicationUserId,
        Guid? relatedPatientId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<FamilyRelationshipDocument>> ListFamilyRelationshipDocumentsAsync(Guid requestId, CancellationToken cancellationToken);
    Task<FamilyRelationshipDocument?> FindFamilyRelationshipDocumentAsync(Guid requestId, Guid documentId, CancellationToken cancellationToken);
    Task<IReadOnlyList<FamilyRelationshipRequestHistory>> ListFamilyRelationshipRequestHistoryAsync(Guid requestId, CancellationToken cancellationToken);
    void MarkFamilyMembershipChanged(Family family);
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
    Task<bool> HasUserRoleAsync(Guid applicationUserId, Guid roleId, CancellationToken cancellationToken);
    Task<PasswordResetChallenge?> FindChallengeAsync(Guid id, CancellationToken cancellationToken);
    Task<PasswordResetChallenge?> FindLatestChallengeAsync(Guid applicationUserId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PasswordResetChallenge>> ListActiveChallengesAsync(Guid applicationUserId, CancellationToken cancellationToken);
    Task<MedicalSpecialization?> FindMedicalSpecializationAsync(Guid id, bool includeDeleted, CancellationToken cancellationToken);
    Task<bool> MedicalSpecializationNameArExistsAsync(string nameAr, Guid? excludingId, CancellationToken cancellationToken);
    Task<bool> MedicalSpecializationNameEnExistsAsync(string nameEn, Guid? excludingId, CancellationToken cancellationToken);
    Task<(IReadOnlyList<MedicalSpecialization> Items, long TotalCount)> ListMedicalSpecializationsAsync(
        string? search,
        bool? isActive,
        bool? isDeleted,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<MedicalSpecializationOptionRecord>> ListSelectableMedicalSpecializationsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Guid>> ListAvailableMedicalSpecializationIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
    Task<IReadOnlyList<DoctorSpecializationViewRecord>> ListDoctorSpecializationsAsync(Guid doctorId, CancellationToken cancellationToken);
    Task<DoctorSpecializationRequest?> FindOpenDoctorSpecializationRequestAsync(Guid doctorId, CancellationToken cancellationToken);
    Task<DoctorSpecializationRequest?> FindLatestDoctorSpecializationRequestAsync(Guid doctorId, CancellationToken cancellationToken);
    Task<DoctorSpecializationRequestOwnerRecord?> FindDoctorSpecializationRequestAsync(Guid requestId, CancellationToken cancellationToken);
    Task<IReadOnlyList<DoctorSpecializationRequestItemViewRecord>> ListDoctorSpecializationRequestItemsAsync(
        Guid requestId,
        int revisionNumber,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<DoctorSpecializationRequestRevision>> ListDoctorSpecializationRequestRevisionsAsync(Guid requestId, CancellationToken cancellationToken);
    Task<IReadOnlyList<DoctorSpecializationRequestHistory>> ListDoctorSpecializationRequestHistoryAsync(Guid requestId, CancellationToken cancellationToken);
    Task<(IReadOnlyList<DoctorSpecializationRequestQueueRecord> Items, long TotalCount)> ListDoctorSpecializationRequestsAsync(
        DoctorSpecializationRequestStatus? status,
        DoctorSpecializationRequestType? type,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);
    Task ReplaceDoctorSpecializationsAsync(
        Guid doctorId,
        IReadOnlyCollection<DoctorSpecialization> replacements,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<LocationReferenceRecord>> ListGovernoratesAsync(CancellationToken cancellationToken);
    Task<bool> ActiveGovernorateExistsAsync(int governorateId, CancellationToken cancellationToken);
    Task<IReadOnlyList<LocationReferenceRecord>> ListCitiesAsync(int governorateId, CancellationToken cancellationToken);
    Task<bool> ActiveCityExistsAsync(int cityId, CancellationToken cancellationToken);
    Task<IReadOnlyList<LocationReferenceRecord>> ListAreasAsync(int cityId, CancellationToken cancellationToken);
    Task<LocationHierarchyRecord?> FindLocationHierarchyAsync(int areaId, CancellationToken cancellationToken);
    Task<DoctorPractice?> FindDoctorPracticeLocationAsync(Guid doctorId, CancellationToken cancellationToken);
    Task<DoctorPracticeLocationViewRecord?> GetDoctorPracticeLocationAsync(Guid doctorId, CancellationToken cancellationToken);
    Task<IReadOnlyList<DoctorPracticeViewRecord>> ListDoctorPracticesAsync(Guid doctorId, CancellationToken cancellationToken);
    Task<DoctorPractice?> FindDoctorPracticeAsync(Guid practiceId, CancellationToken cancellationToken);
    Task<DoctorPracticeViewRecord?> GetDoctorPracticeAsync(Guid practiceId, CancellationToken cancellationToken);
    Task<DoctorPracticeConfiguration?> FindDoctorPracticeConfigurationAsync(
        Guid practiceId,
        CancellationToken cancellationToken);
    Task<DoctorPracticeBranding?> FindDoctorPracticeBrandingAsync(
        Guid practiceId,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<DoctorPracticeSchedulePeriod>> ListDoctorPracticeSchedulePeriodsAsync(
        Guid practiceId,
        CancellationToken cancellationToken);
    Task<DoctorPracticeSchedulePeriod?> FindDoctorPracticeSchedulePeriodAsync(
        Guid periodId,
        CancellationToken cancellationToken);
    Task<bool> HasDoctorScheduleOverlapAsync(
        Guid doctorId,
        Guid practiceId,
        Guid? excludingPeriodId,
        DayOfWeek dayOfWeek,
        TimeOnly startTime,
        TimeOnly endTime,
        CancellationToken cancellationToken);
    Task AcquireDoctorScheduleLockAsync(Guid doctorId, CancellationToken cancellationToken);
    Task<IReadOnlyList<DoctorPracticeScheduleException>> ListDoctorPracticeScheduleExceptionsAsync(
        Guid practiceId,
        CancellationToken cancellationToken);
    Task<DoctorPracticeScheduleException?> FindDoctorPracticeScheduleExceptionAsync(
        Guid exceptionId,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<DoctorPracticeSegment>> ListDoctorPracticeSegmentsAsync(
        Guid practiceId,
        CancellationToken cancellationToken);
    Task<DoctorPracticeSegment?> FindDoctorPracticeSegmentAsync(Guid segmentId, CancellationToken cancellationToken);
    Task<IReadOnlyList<DoctorPracticeVisitType>> ListDoctorPracticeVisitTypesAsync(
        Guid practiceId,
        CancellationToken cancellationToken);
    Task<DoctorPracticeVisitType?> FindDoctorPracticeVisitTypeAsync(Guid visitTypeId, CancellationToken cancellationToken);
    Task<IReadOnlyList<DoctorPracticeSegmentVisitTypePrice>> ListDoctorPracticePricesAsync(
        Guid practiceId,
        CancellationToken cancellationToken);
    Task<DoctorPracticeSegmentVisitTypePrice?> FindDoctorPracticePriceAsync(
        Guid priceId,
        CancellationToken cancellationToken);
    Task<bool> DoctorPracticePriceExistsAsync(
        Guid practiceId,
        Guid segmentId,
        Guid visitTypeId,
        Guid? excludingPriceId,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<ReceptionViewRecord>> ListDoctorReceptionsAsync(
        Guid doctorId,
        CancellationToken cancellationToken);
    Task<ReceptionViewRecord?> FindDoctorReceptionAsync(Guid receptionId, CancellationToken cancellationToken);
    Task<Reception?> FindReceptionByApplicationUserIdAsync(
        Guid applicationUserId,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<ReceptionPracticeAssignment>> ListReceptionAssignmentsAsync(
        Guid receptionId,
        CancellationToken cancellationToken);
    Task<ReceptionPracticeAssignment?> FindReceptionAssignmentAsync(
        Guid assignmentId,
        CancellationToken cancellationToken);
    Task<bool> ReceptionAssignmentExistsAsync(
        Guid receptionId,
        Guid practiceId,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<ReceptionPracticeAssignmentPermission>> ListReceptionAssignmentPermissionsAsync(
        Guid assignmentId,
        CancellationToken cancellationToken);
    Task ReplaceReceptionAssignmentPermissionsAsync(
        Guid assignmentId,
        IReadOnlyCollection<ReceptionPracticeAssignmentPermission> replacements,
        CancellationToken cancellationToken);
    Task<bool> HasReceptionPracticePermissionAsync(
        Guid applicationUserId,
        Guid practiceId,
        string permissionName,
        CancellationToken cancellationToken);
    void Add<TEntity>(TEntity entity) where TEntity : class;
    void Remove<TEntity>(TEntity entity) where TEntity : class;
    void SetOriginalRowVersion<TEntity>(TEntity entity, byte[] rowVersion) where TEntity : class;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

