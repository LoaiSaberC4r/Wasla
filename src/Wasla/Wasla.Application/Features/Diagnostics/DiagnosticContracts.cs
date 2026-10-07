using System.Text.Json.Serialization;
using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Domain.Results;
using Wasla.Application.Features.Clinical;
using Wasla.Application.Persistence;
using Wasla.Domain.Diagnostics;

namespace Wasla.Application.Features.Diagnostics;

public sealed record CatalogCapabilities(bool CanEdit, bool CanActivate, bool CanDeactivate, bool CanMerge);
public sealed record MedicalCatalogResponse(Guid CatalogId, MedicalCatalogSource Source, MedicalCatalogStatus Status,
    string? LoincCode, string NameEn, string? NameAr, string? OfficialNameEn, string? OfficialNameAr, string? ExternalStatus,
    string? SourceVersion, bool IsCommonOrder, CatalogPresentation Presentation, string? SourceDataJson, string? AttributesJson,
    Guid? MergedIntoId, string RowVersion, CatalogCapabilities Capabilities, IReadOnlyList<DiagnosticHistoryResponse>? History = null);
public sealed record DiagnosticHistoryResponse(Guid Id, string Action, string? Reason, string? BeforeSnapshot, string AfterSnapshot,
    Guid ActorUserId, string ActorType, DateTime OccurredAtUtc);
public sealed record CatalogRequestCapabilities(bool CanEdit, bool CanRequestMoreInfo, bool CanApprove, bool CanReject);
public sealed record MedicalCatalogRequestResponse(Guid RequestId, Guid RequestedByDoctorId, string Name, string? Specimen,
    string? CatalogClarificationNote, MedicalCatalogRequestStatus Status, Guid? CanonicalCatalogId, string? ReasonType,
    string? ReviewReason, DateTime CreatedAtUtc, string RowVersion, CatalogRequestCapabilities Capabilities,
    IReadOnlyList<DiagnosticHistoryResponse> History, MedicalCatalogResponse? CanonicalCatalog = null);
public sealed record DiagnosticOrderCapabilities(bool CanManageDraft, bool CanCancelRemaining, bool CanUploadResult);
public sealed record DiagnosticItemCapabilities(bool CanEdit, bool CanRemove, bool CanCancel, bool HasCurrentResult);
public sealed record DiagnosticItemResponse(Guid ItemId, string? NameArSnapshot, string NameEnSnapshot, string? LoincCodeSnapshot,
    string? ModalitySnapshot, string? AnatomicLocationSnapshot, string? LateralitySnapshot, string? DoctorInstructions,
    DiagnosticItemStatus Status, string? CancellationReason, DiagnosticItemCapabilities Capabilities,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? CatalogId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? CatalogRequestId = null);
public sealed record DiagnosticRequestSummary(Guid RequestId, Guid MedicalEncounterId, Guid PatientId, Guid PracticeId,
    DiagnosticOrigin Origin, DiagnosticRequestStatus Status, DateTime? RequestedAtUtc, string RowVersion);
public sealed record DiagnosticRequestStateResponse(Guid? RequestId, Guid MedicalEncounterId, ClinicalPartyResponse Doctor,
    ClinicalPartyResponse Practice, DiagnosticOrigin Origin, DiagnosticRequestStatus? Status, string? PatientInstructions,
    DateTime? RequestedAtUtc, string? RowVersion, IReadOnlyList<DiagnosticItemResponse> Items, DiagnosticOrderCapabilities Capabilities,
    IReadOnlyList<DiagnosticResultResponse> CurrentResults, IReadOnlyList<DiagnosticSubmissionSummary> Submissions,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PostVisitReason = null);
public sealed record DiagnosticAttachmentResponse(Guid AttachmentId, string OriginalFileName, string ContentType, long SizeBytes,
    string Sha256, DateTime UploadedAtUtc, DiagnosticAttachmentKind Kind);
public sealed record DiagnosticResultCapabilities(bool CanCorrect, bool CanVoid);
public sealed record DiagnosticVersionResponse(Guid VersionId, int VersionNumber, DiagnosticVersionStatus Status,
    string? ExternalProviderName, DateOnly? ExternalReportDate, DiagnosticUploader OriginallyUploadedBy,
    DateTime OriginallyUploadedAtUtc, Guid ReviewedByDoctorId, DateTime ReviewedAtUtc, IReadOnlyList<Guid> CoveredItemIds,
    IReadOnlyList<DiagnosticAttachmentResponse> Attachments,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? CorrectionReason = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? VoidReason = null);
public sealed record DiagnosticResultResponse(Guid ResultId, Guid RequestId, int CurrentVersionNumber,
    DiagnosticVersionResponse? Current, string RowVersion, DiagnosticResultCapabilities Capabilities);
public sealed record DiagnosticSubmissionSummary(Guid SubmissionId, Guid RequestId, PatientSubmissionStatus Status, DateTime SubmittedAtUtc);
public sealed record DiagnosticSubmissionCapabilities(bool CanWithdraw, bool CanAccept, bool CanReject);
public sealed record DiagnosticSubmissionResponse(Guid SubmissionId, Guid RequestId, PatientSubmissionStatus Status,
    string? ExternalProviderName, DateOnly? ExternalReportDate, string? PatientNote, string? PatientVisibleReason,
    Guid? AcceptedResultId, DateTime SubmittedAtUtc, string RowVersion, IReadOnlyList<DiagnosticAttachmentResponse> Attachments,
    DiagnosticSubmissionCapabilities Capabilities);
public sealed record DiagnosticResultMutationResponse(DiagnosticSubmissionResponse? Submission, DiagnosticResultResponse? Result,
    DiagnosticRequestStateResponse Request);
public sealed record DiagnosticImportCapabilities(bool CanApply, bool CanDiscard);
public sealed record DiagnosticImportBatchResponse(Guid BatchId, string SourceVersion, string OriginalFileName, string FileSha256,
    Guid UploadedByUserId, DateTime UploadedAtUtc, DiagnosticImportStatus Status, int TotalRecords,
    IReadOnlyDictionary<DiagnosticImportDisposition, int> Counts, Guid? AppliedByUserId, DateTime? AppliedAtUtc,
    string RowVersion, DiagnosticImportCapabilities Capabilities);
public sealed record DiagnosticImportRecordResponse(Guid RecordId, string LoincCode, string NameEn,
    DiagnosticImportDisposition Disposition, string SourceDataJson, Guid? MatchedCatalogId);
public sealed record DiagnosticFilter(Guid? PracticeId = null, Guid? PatientId = null, Guid? EncounterId = null, Guid? RequestId = null,
    DiagnosticOrigin? Origin = null, DiagnosticRequestStatus? Status = null, PatientSubmissionStatus? SubmissionStatus = null,
    DateTime? FromUtc = null, DateTime? ToUtc = null, int PageNumber = 1, int PageSize = 20);
public sealed record DiagnosticOrderItemInput(Guid? CatalogId = null, DiagnosticCatalogRequestData? NewTest = null, string? DoctorInstructions = null);
public sealed record DiagnosticUpload(string FileName, string ContentType, byte[] Content, DiagnosticAttachmentKind Kind = DiagnosticAttachmentKind.Report);
public enum CatalogMutation { Create = 1, Update = 2, Activate = 3, Deactivate = 4, Merge = 5 }
public enum CatalogRequestMutation { Create = 1, Update = 2, MoreInfo = 3, Approve = 4, Reject = 5 }
public enum DiagnosticOrderMutation { Add = 1, Update = 2, Remove = 3, PostVisit = 4, Cancel = 5 }
public enum DiagnosticResultMutation { Upload = 1, Correct = 2, Void = 3, Submit = 4, Accept = 5, Reject = 6, Withdraw = 7 }
public enum DiagnosticReadResource { Requests = 1, Results = 2, Submissions = 3, RequestHistory = 4, Versions = 5, Draft = 6 }

public sealed record SearchMedicalCatalogQuery(DiagnosticKind Kind, bool DoctorSearch, string? Search = null,
    MedicalCatalogStatus? Status = null, MedicalCatalogSource? Source = null, bool? HasArabic = null, bool CommonOnly = false,
    int PageNumber = 1, int PageSize = 20, Guid? Id = null) : IQuery<ClinicalPage<MedicalCatalogResponse>>;
public sealed record MutateMedicalCatalogCommand(DiagnosticKind Kind, CatalogMutation Mutation, Guid? Id = null,
    CatalogPresentation? Data = null, string? RowVersion = null, string? Reason = null, Guid? TargetId = null, string? IdempotencyKey = null)
    : ICommand<MedicalCatalogResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record ListMedicalCatalogRequestsQuery(DiagnosticKind Kind, bool Own, Guid? Id = null, MedicalCatalogRequestStatus? Status = null,
    Guid? DoctorId = null, string? Search = null, int PageNumber = 1, int PageSize = 20) : IQuery<ClinicalPage<MedicalCatalogRequestResponse>>;
public sealed record MutateMedicalCatalogRequestCommand(DiagnosticKind Kind, CatalogRequestMutation Mutation, Guid? Id = null,
    DiagnosticCatalogRequestData? Data = null, string? RowVersion = null, string? Reason = null, Guid? CanonicalCatalogId = null,
    CatalogPresentation? ApprovedData = null, string? IdempotencyKey = null)
    : ICommand<MedicalCatalogRequestResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record DiagnosticOrderCommand(DiagnosticKind Kind, DiagnosticOrderMutation Mutation, Guid? PracticeId = null,
    Guid? EncounterId = null, Guid? RequestId = null, Guid? ItemId = null, string? RowVersion = null,
    DiagnosticOrderItemInput? Item = null, IReadOnlyList<DiagnosticOrderItemInput>? Items = null,
    string? PatientInstructions = null, string? Reason = null, string? IdempotencyKey = null)
    : ICommand<DiagnosticRequestStateResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record DiagnosticResultCommand(DiagnosticKind Kind, DiagnosticResultMutation Mutation, Guid? RequestId = null,
    Guid? ResultId = null, Guid? SubmissionId = null, string? RowVersion = null, IReadOnlyList<Guid>? CoveredItemIds = null,
    IReadOnlyList<DiagnosticUpload>? Attachments = null, string? ExternalProviderName = null, DateOnly? ExternalReportDate = null,
    string? PatientNote = null, string? Reason = null, string? IdempotencyKey = null)
    : ICommand<DiagnosticResultMutationResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record ReadDiagnosticQuery(DiagnosticKind Kind, DiagnosticReadResource Resource, bool Patient = false,
    Guid? Id = null, Guid? PracticeId = null, Guid? EncounterId = null, int? VersionNumber = null, DiagnosticFilter? Filter = null) : IQuery<object>;
public sealed record DiagnosticMediaQuery(DiagnosticKind Kind, bool Patient, bool Submission, Guid Id, Guid AttachmentId,
    int? VersionNumber = null) : IQuery<Wasla.Application.Media.PrivateMedia>;
public sealed record PreviewDiagnosticImportCommand(DiagnosticKind Kind, byte[] File, string FileName, string SourceVersion)
    : ICommand<DiagnosticImportBatchResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record MutateDiagnosticImportCommand(DiagnosticKind Kind, Guid BatchId, bool Apply, string RowVersion, string? IdempotencyKey = null, bool SkipPossibleConflicts = false)
    : ICommand<DiagnosticImportBatchResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record ReadDiagnosticImportQuery(DiagnosticKind Kind, Guid? BatchId = null, bool Changes = false,
    DiagnosticImportDisposition? Disposition = null, string? Search = null, int PageNumber = 1, int PageSize = 20) : IQuery<object>;

public interface IDiagnosticReadService
{
    Task<ClinicalPage<MedicalCatalogResponse>> CatalogAsync(SearchMedicalCatalogQuery query, IReadOnlyList<string> permissions, CancellationToken ct);
    Task<ClinicalPage<MedicalCatalogRequestResponse>> CatalogRequestsAsync(ListMedicalCatalogRequestsQuery query, Guid? doctorId, IReadOnlyList<string> permissions, CancellationToken ct);
    Task<object?> ReadAsync(ReadDiagnosticQuery query, Guid ownerId, IReadOnlyList<string> permissions, CancellationToken ct);
    Task<DiagnosticRequestStateResponse?> RequestAsync(DiagnosticKind kind, Guid id, Guid ownerId, bool patient, IReadOnlyList<string> permissions, CancellationToken ct);
    Task<DiagnosticResultResponse?> ResultAsync(DiagnosticKind kind, Guid id, Guid ownerId, bool patient, IReadOnlyList<string> permissions, CancellationToken ct);
    Task<DiagnosticSubmissionResponse?> SubmissionAsync(DiagnosticKind kind, Guid id, Guid ownerId, bool patient, IReadOnlyList<string> permissions, CancellationToken ct);
    Task<string?> MediaKeyAsync(DiagnosticMediaQuery query, Guid ownerId, CancellationToken ct);
    Task AuditReadAsync(DiagnosticKind kind, DiagnosticReadResource resource, Guid id, Guid? attachmentId, Guid actor, DateTime nowUtc, CancellationToken ct);
    Task<object?> ImportAsync(ReadDiagnosticImportQuery query, IReadOnlyList<string> permissions, CancellationToken ct);
}
public interface IDiagnosticImportService
{
    Task<Result<DiagnosticImportBatchResponse>> StageAsync(PreviewDiagnosticImportCommand query, Guid actor, DateTime now, CancellationToken ct);
    Task<Result<DiagnosticImportBatchResponse>> MutateAsync(MutateDiagnosticImportCommand query, Guid actor, DateTime now, CancellationToken ct);
}
public interface IDiagnosticCoverageReader
{
    Task<IReadOnlySet<Guid>> CurrentCoverageAsync(DiagnosticKind kind, Guid requestId, CancellationToken ct);
}
