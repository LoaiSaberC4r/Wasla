using BuildingBlock.Application.Abstraction;
using BuildingBlock.Domain.Results;
using Wasla.Application.Features.Clinical;
using Wasla.Application.Features.Medications;
using Wasla.Domain.Common;

namespace Wasla.Application.Features.DoctorPatients;

public sealed record DoctorPatientCapabilities(bool CanViewAttendances, bool CanViewEncounters, bool CanViewDiagnoses,
    bool CanViewPrescriptions, bool CanExportPrescriptions, bool CanViewAppointments, bool CanViewFollowUps,
    bool CanViewLabRequests, bool CanViewLabResults, bool CanViewLabSubmissions,
    bool CanViewRadiologyRequests, bool CanViewRadiologyResults, bool CanViewRadiologySubmissions);
public sealed record DoctorPatientPractice(Guid Id, string NameAr, string? NameEn, bool IsActive);
public sealed record DoctorPatientListItem(Guid PatientId, string NameAr, string? NameEn, string? PhoneNumber,
    string? Email, DateOnly DateOfBirth, int Age, Gender Gender, DateTime FirstAttendanceAtUtc,
    DateTime LastAttendanceAtUtc, DateOnly LastAttendanceBusinessDate, int AttendanceCount,
    int? CompletedEncounterCount, int PracticeCount, DoctorPatientPractice LastPractice,
    bool? HasUpcomingAppointment, bool HistoricalOnly, DoctorPatientCapabilities Capabilities);
public sealed record DoctorPatientsSummary(long AllTimeUniquePatients, long AttendedLast30Days, long ReturningPatients);
public sealed record DoctorPatientsPageResponse(IReadOnlyList<DoctorPatientListItem> Items, long TotalCount,
    int PageNumber, int PageSize, DoctorPatientsSummary Summary, DateTime AsOfUtc);
public sealed record DoctorMyPatientOverviewResponse(DoctorPatientListItem Patient,
    IReadOnlyList<DoctorPatientPractice> Practices, DoctorPatientAttendance? LastAttendance,
    DoctorPatientDiagnosis? LastDiagnosis, DoctorPatientPrescription? LastPrescription,
    int? LabRequestCount, int? RadiologyRequestCount, DoctorPatientAppointment? NextAppointment,
    DoctorPatientFollowUp? CurrentFollowUp);
public sealed record DoctorPatientAttendance(Guid TicketId, Guid PracticeId, string PracticeName, DateOnly BusinessDate,
    DateTime CheckInAtUtc, string Source, string CheckInMode, string TicketStatus, int TicketNumber,
    bool IsArchived, Guid? LinkedReservationId, Guid? EncounterId, string? EncounterStatus,
    bool CanViewTicket, bool CanViewEncounter);
public sealed record DoctorPatientDiagnosis(Guid DiagnosisId, Guid EncounterId, Guid PracticeId, DateOnly BusinessDate,
    DateTime EncounterDate, string DiagnosisType, string DisplayText, string EncounterStatus,
    bool IsActive, bool IsHistoricalPractice);
public sealed record DoctorPatientPrescription(Guid PrescriptionId, Guid EncounterId, Guid PracticeId,
    DateTime VisitDateUtc, int CurrentVersionNumber, string CurrentStatus, int MedicationCount,
    bool HasVersions, bool CanView, bool CanExport, bool IsHistoricalPractice);
public sealed record DoctorPatientAppointment(Guid ReservationId, Guid PracticeId, DateOnly BusinessDate,
    DateTime ScheduledStart, string VisitType, string Segment, string Status, Guid? LinkedTicketId,
    Guid? FollowUpEligibilityId, bool IsArchived, string Source);
public sealed record DoctorPatientFollowUp(Guid EligibilityId, Guid SourceEncounterId, Guid PracticeId,
    string Status, DateOnly ValidUntil, Guid? ReservedReservationId, Guid? ReservedTicketId,
    Guid? ConsumedEncounterId, bool CanBook, bool CanCreateFollowUpFromSource, bool IsHistoricalPractice);
public sealed record DoctorPatientsQuery(string? Search = null, Guid? PracticeId = null,
    DateOnly? LastAttendanceFrom = null, DateOnly? LastAttendanceTo = null, int? MinAttendanceCount = null,
    bool? HasUpcomingAppointment = null, string PracticeScope = "all", int PageNumber = 1, int PageSize = 20, DateTime? AsOfUtc = null)
    : IQuery<DoctorPatientsPageResponse>;
public sealed record DoctorMyPatientQuery(Guid PatientId) : IQuery<DoctorMyPatientOverviewResponse>;
public enum DoctorPatientHistory { Attendances, Diagnoses, Prescriptions, Appointments, FollowUps }
public sealed record DoctorPatientHistoryQuery(Guid PatientId, DoctorPatientHistory Resource, Guid? PracticeId = null,
    string? Status = null, DateOnly? FromDate = null, DateOnly? ToDate = null, int PageNumber = 1, int PageSize = 20)
    : IQuery<object>;
public sealed record PrescriptionPdfQuery(Guid PrescriptionId, int VersionNumber, string Disposition = "inline")
    : IQuery<PrescriptionPdfResponse>;
public sealed record PrescriptionPdfResponse(byte[] Bytes, string FileName, string Disposition);
public sealed record PrescriptionPdfDocument(Guid PrescriptionId, Guid PracticeId, Guid PatientId, Guid EncounterId, string DoctorName, string PracticeName,
    string PatientName, DateTime EncounterDateUtc, PrescriptionVersionResponse Version);
public interface IPrescriptionPdfRenderer
{
    Task<Result<byte[]>> RenderAsync(PrescriptionPdfDocument document, CancellationToken ct);
}
public sealed record DoctorPatientActor(Guid UserId, Guid DoctorId, IReadOnlyList<string> Permissions)
{
    public bool Has(string permission) => Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
}
public sealed record DoctorOwnReadContext(DoctorPatientActor Actor, Guid PracticeId, bool ReadOnly);
public sealed record DoctorPatientPracticeScope(Guid DoctorId, bool IsActive);
public interface IDoctorPatientReadService
{
    Task<DoctorPatientPracticeScope?> PracticeScopeAsync(Guid practiceId, CancellationToken ct);
    Task<bool> HasAttendanceAsync(Guid doctorId, Guid patientId, CancellationToken ct);
    Task<DoctorPatientsPageResponse> DirectoryAsync(DoctorPatientActor actor, DoctorPatientsQuery query, CancellationToken ct);
    Task<DoctorMyPatientOverviewResponse?> OverviewAsync(DoctorPatientActor actor, Guid patientId, CancellationToken ct);
    Task<object> HistoryAsync(DoctorPatientActor actor, DoctorPatientHistoryQuery query, CancellationToken ct);
    Task<PrescriptionPdfDocument?> PdfDocumentAsync(Guid doctorId, Guid prescriptionId, int version, CancellationToken ct);
    Task AuditAsync(DoctorPatientActor actor, string action, Guid targetId, CancellationToken ct);
}
