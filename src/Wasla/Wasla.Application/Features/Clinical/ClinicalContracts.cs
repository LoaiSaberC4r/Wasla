using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using Wasla.Application.Persistence;
using Wasla.Domain.Clinical;
using Wasla.Domain.Medications;
using Wasla.Application.Features.Medications;

namespace Wasla.Application.Features.Clinical;

public sealed record ClinicalPartyResponse(Guid Id, string NameAr, string? NameEn);
public sealed record DiagnosisResponse(Guid DiagnosisId, DiagnosisType Type, string DisplayText, string? Notes);
public sealed record EncounterCapabilitiesResponse(bool CanEditClinicalNotes, bool CanManageDiagnoses,
    bool CanComplete, bool CanAmend, bool CanCreateFollowUpEligibility, bool CanManagePrescription = false, bool CanRequestNewMedication = false);
public sealed record EncounterDetailsResponse(Guid EncounterId, Guid TicketId, ClinicalPartyResponse Doctor,
    ClinicalPartyResponse Practice, ClinicalPartyResponse Patient, EncounterStatus Status, DateTime StartedAtUtc,
    DateTime? CompletedAtUtc, string? ClinicalNotes, IReadOnlyList<DiagnosisResponse> Diagnoses,
    FollowUpEligibilityResponse? FollowUpEligibility, EncounterCapabilitiesResponse Capabilities, string RowVersion, PrescriptionStateResponse? Prescription = null,
    IReadOnlyList<PrescriptionCompletionBlocker>? CompletionBlockers = null);
public sealed record PatientEncounterDetailsResponse(Guid EncounterId, Guid TicketId, ClinicalPartyResponse Doctor,
    ClinicalPartyResponse Practice, DateTime StartedAtUtc, DateTime CompletedAtUtc,
    IReadOnlyList<DiagnosisResponse> CurrentActiveDiagnoses, FollowUpEligibilityResponse? FollowUpEligibility);
public sealed record EncounterSummaryResponse(Guid EncounterId, Guid TicketId, Guid PatientId, string PatientNameAr,
    string? PatientNameEn, Guid DoctorPracticeId, string PracticeNameAr, string? PracticeNameEn, string VisitTypeCode,
    string VisitTypeNameAr, string? VisitTypeNameEn, EncounterStatus Status, DateTime StartedAtUtc, DateTime? CompletedAtUtc,
    bool HasDiagnosis, bool HasFollowUpEligibility, string RowVersion);
public sealed record ClinicalPage<T>(IReadOnlyList<T> Items, long TotalCount, int PageNumber, int PageSize);
public sealed record FollowUpEligibilityResponse(Guid EligibilityId, Guid SourceEncounterId,
    ClinicalPartyResponse Patient, ClinicalPartyResponse Doctor, ClinicalPartyResponse Practice,
    FollowUpEligibilityStatus Status, DateOnly ValidUntil, Guid? ReservedReservationId, Guid? ReservedTicketId,
    Guid? ConsumedEncounterId, bool CanBook, string RowVersion);
public sealed record ReceptionFollowUpEligibilityResponse(Guid EligibilityId, Guid PatientId, Guid DoctorId,
    Guid PracticeId, DateOnly ValidUntil, FollowUpEligibilityStatus Status, bool CanBook, string RowVersion);
public sealed record AmendmentChangeResponse(AmendmentTarget Target, Guid? TargetEntityId,
    AmendmentChangeType ChangeType, string? BeforeSnapshot, string? AfterSnapshot);
public sealed record EncounterAmendmentResponse(Guid AmendmentId, int SequenceNumber, string Reason,
    Guid CreatedByDoctorId, Guid CreatedByApplicationUserId, DateTime CreatedAtUtc, IReadOnlyList<AmendmentChangeResponse> Changes);

public sealed record ListDoctorEncountersQuery(Guid PracticeId, Guid? PatientId = null, EncounterStatus? Status = null,
    DateOnly? FromDate = null, DateOnly? ToDate = null, string? Search = null, int PageNumber = 1, int PageSize = 20)
    : IQuery<ClinicalPage<EncounterSummaryResponse>>;
public sealed record GetDoctorEncounterQuery(Guid PracticeId, Guid Id, bool ByTicket = false) : IQuery<EncounterDetailsResponse>;
public sealed record ListMyEncountersQuery(int PageNumber = 1, int PageSize = 20) : IQuery<ClinicalPage<EncounterSummaryResponse>>;
public sealed record GetMyEncounterQuery(Guid EncounterId) : IQuery<PatientEncounterDetailsResponse>;
public sealed record UpdateClinicalNotesCommand(Guid PracticeId, Guid EncounterId, string? ClinicalNotes, string RowVersion)
    : ICommand<EncounterDetailsResponse>, ITransactionalCommand<WaslaWritePersistence>;
public enum DiagnosisMutation { Add = 1, Update = 2, Remove = 3 }
public sealed record ManageDiagnosisCommand(Guid PracticeId, Guid EncounterId, DiagnosisMutation Mutation,
    Guid? DiagnosisId, DiagnosisType? Type, string? DisplayText, string? Notes, string EncounterRowVersion)
    : ICommand<EncounterDetailsResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record CreateEncounterAmendmentCommand(Guid PracticeId, Guid EncounterId, string Reason,
    string EncounterRowVersion, IReadOnlyList<EncounterCorrection> Changes)
    : ICommand<EncounterDetailsResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record ListEncounterAmendmentsQuery(Guid PracticeId, Guid EncounterId) : IQuery<IReadOnlyList<EncounterAmendmentResponse>>;
public sealed record CreateFollowUpEligibilityCommand(Guid PracticeId, Guid EncounterId, DateOnly ValidUntil)
    : ICommand<FollowUpEligibilityResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record ListMyFollowUpEligibilitiesQuery(Guid? PatientId = null, FollowUpEligibilityStatus? Status = null,
    int PageNumber = 1, int PageSize = 20) : IQuery<ClinicalPage<FollowUpEligibilityResponse>>;
public sealed record GetMyFollowUpEligibilityQuery(Guid EligibilityId) : IQuery<FollowUpEligibilityResponse>;
public sealed record ListReceptionFollowUpEligibilitiesQuery(Guid PracticeId, Guid PatientId)
    : IQuery<IReadOnlyList<ReceptionFollowUpEligibilityResponse>>;

public interface IClinicalReadService
{
    Task<ClinicalPage<EncounterSummaryResponse>> ListEncountersAsync(Guid? doctorId, Guid? practiceId, Guid? patientId,
        EncounterStatus? status, DateTime? fromUtc, DateTime? throughUtc, string? search, int page, int size, CancellationToken ct);
    Task<EncounterDetailsResponse?> DoctorDetailsAsync(Guid doctorId, Guid practiceId, Guid id, bool byTicket, CancellationToken ct);
    Task<PatientEncounterDetailsResponse?> PatientDetailsAsync(Guid patientId, Guid id, CancellationToken ct);
    Task<IReadOnlyList<EncounterAmendmentResponse>> AmendmentsAsync(Guid encounterId, CancellationToken ct);
    Task<ClinicalPage<FollowUpEligibilityResponse>> EligibilitiesAsync(IReadOnlyCollection<Guid> patientIds,
        Guid? practiceId, FollowUpEligibilityStatus? status, Guid? id, int page, int size, CancellationToken ct);
    Task AuditReadAsync(Guid encounterId, Guid actor, DateTime nowUtc, CancellationToken ct);
}
