using System.Text.Json.Serialization;
using BuildingBlock.Application.Abstraction;
using Wasla.Application.Features.Medications;
using Wasla.Application.Media;
using Wasla.Domain.Common;

namespace Wasla.Application.Features.Clinical;

public sealed record MedicalRecordPatient(Guid PatientId, string NameAr, string? NameEn, DateOnly DateOfBirth, Gender Gender);
public sealed record RecordContext(MedicalRecordMode Mode, Guid? TicketId, Guid? TreatingEncounterId, string? TreatmentStatus);
public sealed record RecordSummary(int CompletedVisitCount, int CurrentPrescriptionCount, int OfficialLabResultCount, int OfficialRadiologyResultCount, int HiddenEncounterCount);
public sealed record RecordCapabilities(bool CanView, bool CanViewAttachments, bool CanRequestHiddenAccess);
public record MedicalRecordOverviewResponse(MedicalRecordPatient Patient, RecordContext Context, RecordSummary Summary,
    RecordPage<TimelineEntry> Timeline, RecordCapabilities Capabilities, IReadOnlyList<string> AvailableFilters);
[JsonDerivedType(typeof(ClinicalTimelineEntry))]
[JsonDerivedType(typeof(HiddenTimelineEntry))]
public abstract record TimelineEntry(string Type, string EntryId);
public sealed record TimelineCapabilities(bool CanViewDetails, bool CanViewAttachments);
public sealed record ClinicalTimelineEntry(string Type, string EntryId, DateTime OccurredAtUtc, Guid SourceId, Guid EncounterId,
    string ResourceType, string Status, string Title, TimelineCapabilities Capabilities) : TimelineEntry(Type, EntryId);
public sealed record HiddenRequestState(string Status, Guid? RequestId);
public sealed record HiddenCapabilities(bool CanRequestAccess);
public sealed record HiddenTimelineEntry(string EntryId, DateOnly VisitDate, bool IsHidden, string HiddenEncounterToken,
    HiddenRequestState AccessRequest, HiddenCapabilities Capabilities) : TimelineEntry("HiddenEncounterPlaceholder", EntryId);
public sealed record SafeDiagnosis(Guid DiagnosisId, string Type, string DisplayText);
public record MedicalRecordEncounterDetailsResponse(Guid EncounterId, DateOnly VisitDate, ClinicalPartyResponse Doctor,
    ClinicalPartyResponse Practice, IReadOnlyList<SafeDiagnosis> Diagnoses, RecordPage<TimelineEntry> Outputs, RecordCapabilities Capabilities,
    ClinicalTimelineEntry? Prescription, IReadOnlyList<ClinicalTimelineEntry> LabRequests, IReadOnlyList<ClinicalTimelineEntry> LabResults,
    IReadOnlyList<ClinicalTimelineEntry> RadiologyRequests, IReadOnlyList<ClinicalTimelineEntry> RadiologyResults);
public sealed record RecordAttachment(Guid AttachmentId, string FileName, string ContentType, long SizeBytes, bool CanDownload);
public sealed record SafePrescriptionContent(IReadOnlyList<PatientPrescriptionItemResponse> Items);
public sealed record SafeOrderItem(Guid ItemId, string? NameAr, string NameEn, string? Code, string? Instructions, string Status);
public sealed record SafeOrderContent(DateTime RequestedAtUtc, string? PatientInstructions, IReadOnlyList<SafeOrderItem> Items,
    string ResultStatus);
public sealed record SafeResultContent(Guid RequestId, DateTime ReviewedAtUtc, string? ExternalProviderName, DateOnly? ExternalReportDate,
    IReadOnlyList<Guid> CoveredItemIds);
public record MedicalRecordItemDetailsResponse(string ResourceType, Guid SourceId, Guid EncounterId, string Status,
    int? CurrentVersion, object SafeContent, IReadOnlyList<RecordAttachment> Attachments, RecordCapabilities Capabilities);
// Actor-specific public contracts share only these already-safe fields. Private owner clinical DTOs never enter this hierarchy.
public sealed record PatientSafeMedicalRecordOverviewResponse : MedicalRecordOverviewResponse
{
    public PatientSafeMedicalRecordOverviewResponse(MedicalRecordOverviewResponse safe) : base(safe.Patient, safe.Context, safe.Summary, safe.Timeline, safe.Capabilities, safe.AvailableFilters) { }
}
public sealed record DoctorSharedMedicalRecordOverviewResponse : MedicalRecordOverviewResponse
{
    public DoctorSharedMedicalRecordOverviewResponse(MedicalRecordOverviewResponse safe) : base(safe.Patient, safe.Context, safe.Summary, safe.Timeline, safe.Capabilities, safe.AvailableFilters) { }
}
public sealed record PatientSafeMedicalRecordEncounterResponse : MedicalRecordEncounterDetailsResponse
{
    public PatientSafeMedicalRecordEncounterResponse(MedicalRecordEncounterDetailsResponse s) : base(s.EncounterId, s.VisitDate, s.Doctor, s.Practice, s.Diagnoses, s.Outputs, s.Capabilities, s.Prescription, s.LabRequests, s.LabResults, s.RadiologyRequests, s.RadiologyResults) { }
}
public sealed record DoctorSharedMedicalRecordEncounterResponse : MedicalRecordEncounterDetailsResponse
{
    public DoctorSharedMedicalRecordEncounterResponse(MedicalRecordEncounterDetailsResponse s) : base(s.EncounterId, s.VisitDate, s.Doctor, s.Practice, s.Diagnoses, s.Outputs, s.Capabilities, s.Prescription, s.LabRequests, s.LabResults, s.RadiologyRequests, s.RadiologyResults) { }
}
public sealed record PatientSafeMedicalRecordItemResponse : MedicalRecordItemDetailsResponse
{
    public PatientSafeMedicalRecordItemResponse(MedicalRecordItemDetailsResponse s) : base(s.ResourceType, s.SourceId, s.EncounterId, s.Status, s.CurrentVersion, s.SafeContent, s.Attachments, s.Capabilities) { }
}
public sealed record DoctorSharedMedicalRecordItemResponse : MedicalRecordItemDetailsResponse
{
    public DoctorSharedMedicalRecordItemResponse(MedicalRecordItemDetailsResponse s) : base(s.ResourceType, s.SourceId, s.EncounterId, s.Status, s.CurrentVersion, s.SafeContent, s.Attachments, s.Capabilities) { }
}
public sealed record PatientSafeAccessLog(Guid EntryId, string ActorCategory, string Action, DateTime OccurredAtUtc);
public enum MedicalRecordRead { Overview = 1, Timeline = 2, Encounter = 3, Item = 4, AccessLog = 5, Dependents = 6 }
public sealed record MedicalRecordFilter(string? Types = null, DateOnly? FromDate = null, DateOnly? ToDate = null,
    string? Cursor = null, int PageSize = 20, Guid? EncounterId = null);
public sealed record ReadMedicalRecordQuery(MedicalRecordRead Read, MedicalRecordMode Mode, Guid? PracticeId = null,
    Guid? TicketId = null, Guid? DependentPatientId = null, Guid? EncounterId = null, string? ResourceType = null, Guid? SourceId = null,
    MedicalRecordFilter? Filter = null) : IQuery<object>;
public sealed record ReadMedicalRecordMediaQuery(MedicalRecordMode Mode, string ResourceType, Guid SourceId, Guid AttachmentId,
    Guid? PracticeId = null, Guid? TicketId = null, Guid? DependentPatientId = null) : IQuery<PrivateMedia>;
public interface IMedicalRecordReadService
{
    Task<object?> ReadAsync(ReadMedicalRecordQuery query, MedicalRecordScope scope, CancellationToken ct);
}
public interface IMedicalRecordMediaReader
{
    Task<PrivateMedia?> ReadAsync(ReadMedicalRecordMediaQuery query, MedicalRecordScope scope, CancellationToken ct);
}
