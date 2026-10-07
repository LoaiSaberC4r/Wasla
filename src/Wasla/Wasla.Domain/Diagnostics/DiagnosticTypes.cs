using System.Text.Json;
using BuildingBlock.Domain.Results;
using Wasla.Domain.Medications;
using Wasla.Domain.Resources;

namespace Wasla.Domain.Diagnostics;

public enum DiagnosticKind { Lab = 1, Radiology = 2 }
public enum MedicalCatalogSource { Loinc = 1, Wasla = 2 }
public enum MedicalCatalogStatus { Active = 1, Inactive = 2, Merged = 3 }
public enum MedicalCatalogRequestStatus { Pending = 1, NeedsMoreInfo = 2, Approved = 3, Rejected = 4 }
public enum DiagnosticOrigin { DuringEncounter = 1, PostVisit = 2 }
public enum DiagnosticRequestStatus { Draft = 1, Requested = 2, PartiallyCompleted = 3, Completed = 4, Cancelled = 5 }
public enum DiagnosticItemStatus { Requested = 1, Completed = 2, Cancelled = 3 }
public enum DiagnosticItemSource { Catalog = 1, DoctorSubmitted = 2 }
public enum PatientSubmissionStatus { PendingReview = 1, Accepted = 2, Rejected = 3, Withdrawn = 4, NoLongerApplicable = 5 }
public enum DiagnosticVersionStatus { Finalized = 1, Superseded = 2, Voided = 3 }
public enum DiagnosticAttachmentKind { Report = 1, Image = 2 }
public enum DiagnosticUploader { Doctor = 1, Patient = 2 }
public enum DiagnosticImportStatus { Staged = 1, Applied = 2, Discarded = 3 }
public enum DiagnosticImportDisposition { New = 1, Unchanged = 2, Changed = 3, ArabicAdded = 4, ArabicChanged = 5, ExternalStatusChanged = 6, PossibleConflict = 7, Skipped = 8 }

public sealed record CatalogPresentation(string? DisplayNameEn = null, string? DisplayNameAr = null,
    string? AliasesEn = null, string? AliasesAr = null, string? InternalNote = null);
public sealed record DiagnosticCatalogRequestData(string Name, string? Specimen = null, string? CatalogClarificationNote = null);
public sealed record LoincSourceData(string Code, string NameEn, string? OfficialNameAr, string Status,
    string SourceVersion, bool IsCommonOrder, IReadOnlyDictionary<string, string> Fields,
    IReadOnlyDictionary<string, string[]> Attributes,
    IReadOnlyList<IReadOnlyDictionary<string, string>>? RadiologyParts = null);
public sealed record DiagnosticAttachmentData(string PrivateMediaKey, string OriginalFileName, string ContentType,
    long SizeBytes, string Sha256, DateTime UploadedAtUtc, DiagnosticAttachmentKind Kind = DiagnosticAttachmentKind.Report);

public static class DiagnosticErrors
{
    public static Error Validation(string code) => Error.Validation(code, ErrorMessage.GetString(code));
    public static Error Conflict(string code) => Error.Conflict(code, ErrorMessage.GetString(code));
    public static Error NotFound(string code) => Error.NotFound(code, ErrorMessage.GetString(code));
    public static Error Denied(string code = "Diagnostics.AccessDenied") => Error.Security(code, ErrorMessage.GetString(code));
}
public static class DiagnosticText
{
    public static string Normalize(string? value) => MedicationText.Normalize(value);
    public static string? Clean(string? value) => MedicationText.Clean(value);
    public static string Json(object value) => JsonSerializer.Serialize(value);
    public static bool ValidReason(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 1000;
    public static bool ValidPresentation(CatalogPresentation d) => !(d.DisplayNameEn?.Length > 1000 ||
        d.DisplayNameAr?.Length > 1000 || d.AliasesEn?.Length > 4000 || d.AliasesAr?.Length > 4000 || d.InternalNote?.Length > 2000);
    public static bool ValidRequestData(DiagnosticCatalogRequestData d) => !string.IsNullOrWhiteSpace(d.Name) &&
        d.Name.Length <= 1000 && !(d.Specimen?.Length > 500 || d.CatalogClarificationNote?.Length > 2000);
    public static bool ValidAttachments(IReadOnlyList<DiagnosticAttachmentData> files) => files.Count is >= 1 and <= 20 &&
        files.Sum(f => f.SizeBytes) <= 100L * 1024 * 1024 && files.All(f => f.SizeBytes is > 0 and <= 10L * 1024 * 1024 &&
            !string.IsNullOrWhiteSpace(f.PrivateMediaKey) && f.OriginalFileName.Length <= 255 && f.Sha256.Length == 64 &&
            f.UploadedAtUtc != default && Enum.IsDefined(f.Kind) &&
            f.ContentType is "application/pdf" or "image/jpeg" or "image/png");
}
