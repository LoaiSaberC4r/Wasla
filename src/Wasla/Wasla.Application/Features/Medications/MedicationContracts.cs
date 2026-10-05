using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Domain.Results;
using Wasla.Application.Features.Clinical;
using Wasla.Application.Persistence;
using Wasla.Domain.Medications;

namespace Wasla.Application.Features.Medications;

public sealed record DrugCatalogResponse(Guid DrugCatalogId, string CommercialNameEn, string? CommercialNameAr,
    string? ScientificName, string? Manufacturer, string? DrugClass, string? Strength, string? DosageForm, string? Route,
    string? StrengthSuggestion, string? DosageFormSuggestion, decimal? ReferencePriceEgp, DrugCatalogStatus Status,
    string? StatusReason, Guid? MergedIntoDrugCatalogId, DrugOriginType OriginType, bool IsMissingFromLatestSource,
    bool IsManagerReviewed, bool IsPriceManuallyOverridden, string RowVersion);
public sealed record MedicationHistoryResponse(Guid Id, string Action, string? Reason, string? BeforeSnapshot,
    string AfterSnapshot, Guid PerformedByApplicationUserId, DateTime OccurredAtUtc);
public sealed record DrugCatalogDetailsResponse(DrugCatalogResponse Drug, IReadOnlyList<MedicationHistoryResponse> History);
public sealed record DrugRequestResponse(Guid RequestId, Guid RequestedByDoctorId, MedicationRequestData Medication,
    DrugCatalogRequestStatus Status, Guid? ApprovedDrugCatalogId, Guid? DuplicateOfDrugCatalogId,
    string? CurrentReviewReason, DateTime CreatedAtUtc, DateTime ModifiedAtUtc, string RowVersion,
    IReadOnlyList<MedicationHistoryResponse> History, DrugCatalogResponse? ApprovedDrug = null, DrugCatalogResponse? DuplicateDrug = null);
public sealed record DrugImportBatchResponse(Guid BatchId, string Source, string? SourceVersion, string? SourceCommitSha,
    string FileSha256, DrugImportStatus Status, int TotalRecords, int NewRecords, int UnchangedRecords, int PriceChanges,
    int NeedsReviewRecords, int MissingRecords, int PossibleDuplicateRecords, int ExactDuplicateRecords,
    DateTime CreatedAtUtc, DateTime? AppliedAtUtc, string RowVersion);
public sealed record DrugImportRecordResponse(Guid Id, int SourceRowNumber, DrugData Data, DrugImportChangeType ChangeType, Guid? MatchedDrugCatalogId);

public sealed record PrescriptionItemResponse(Guid ItemId, int SortOrder, MedicationSource MedicationSource,
    Guid? DrugCatalogId, Guid? DrugCatalogRequestId, string MedicationName, string? ScientificName, PrescriptionItemData Clinical);
public sealed record PrescriptionVersionResponse(Guid VersionId, int VersionNumber, PrescriptionVersionStatus Status,
    Guid? PreviousVersionId, string? CorrectionReason, DateTime CreatedAtUtc, DateTime? FinalizedAtUtc,
    DateTime? VoidedAtUtc, string? VoidReason, IReadOnlyList<PrescriptionItemResponse> Items);
public sealed record PrescriptionCapabilitiesResponse(bool CanManageDraft, bool CanRequestNewMedication,
    bool CanStartCorrection, bool CanFinalizeCorrection, bool CanDiscardCorrection, bool CanVoid);
public sealed record PrescriptionStateResponse(Guid? PrescriptionId, Guid MedicalEncounterId, Guid PracticeId,
    PrescriptionVersionResponse? Current, PrescriptionVersionResponse? Draft, string? RowVersion,
    PrescriptionCapabilitiesResponse Capabilities, IReadOnlyList<PrescriptionCompletionBlocker> CompletionBlockers);
public sealed record PatientPrescriptionItemResponse(int SortOrder, string MedicationName, string? ScientificName, PrescriptionItemData Clinical);
public sealed record PatientPrescriptionResponse(Guid PrescriptionId, Guid EncounterId, ClinicalPartyResponse Doctor,
    ClinicalPartyResponse Practice, DateTime VisitDateUtc, int VersionNumber, PrescriptionVersionStatus Status,
    DateTime? FinalizedAtUtc, DateTime? VoidedAtUtc, IReadOnlyList<PatientPrescriptionItemResponse> Items);
public sealed record PatientPrescriptionSummaryResponse(Guid PrescriptionId, Guid EncounterId, string DoctorNameAr,
    string? DoctorNameEn, string PracticeNameAr, string? PracticeNameEn, DateTime VisitDateUtc, int VersionNumber, PrescriptionVersionStatus Status);

public interface IMedicationReadService
{
    Task<ClinicalPage<DrugCatalogResponse>> CatalogAsync(string? search, DrugCatalogStatus? status, bool doctorSearch, int page, int size, CancellationToken ct);
    Task<DrugCatalogDetailsResponse?> DrugAsync(Guid id, CancellationToken ct);
    Task<ClinicalPage<DrugRequestResponse>> RequestsAsync(Guid? doctorId, DrugCatalogRequestStatus? status, string? search,
        Guid? id, int page, int size, CancellationToken ct);
    Task<ClinicalPage<DrugImportBatchResponse>> BatchesAsync(Guid? id, int page, int size, CancellationToken ct);
    Task<ClinicalPage<DrugImportRecordResponse>> ImportChangesAsync(Guid batchId, DrugImportChangeType? change, int page, int size, CancellationToken ct);
    Task<PrescriptionStateResponse?> PrescriptionAsync(Guid? prescriptionId, Guid? encounterId, Guid doctorId, CancellationToken ct);
    Task<IReadOnlyList<PrescriptionVersionResponse>> VersionsAsync(Guid prescriptionId, Guid doctorId, int? number, CancellationToken ct);
    Task<ClinicalPage<PatientPrescriptionSummaryResponse>> PatientListAsync(Guid patientId, int page, int size, CancellationToken ct);
    Task<PatientPrescriptionResponse?> PatientPrescriptionAsync(Guid patientId, Guid id, CancellationToken ct);
}
public interface IDrugCatalogImportService
{
    Task<Result<DrugCatalogImportBatch>> StageAsync(byte[] file, string? sourceVersion, string? commit, Guid actor, DateTime now, CancellationToken ct);
    Task<Result<DrugCatalogImportBatch>> ApplyAsync(Guid batchId, Guid actor, DateTime now, CancellationToken ct);
}

public sealed record SearchDrugCatalogQuery(string? Search = null, DrugCatalogStatus? Status = null, bool DoctorSearch = true, int PageNumber = 1, int PageSize = 20)
    : IQuery<ClinicalPage<DrugCatalogResponse>>;
public sealed record GetDrugCatalogQuery(Guid DrugId) : IQuery<DrugCatalogDetailsResponse>;
public enum DrugCatalogMutation { Create = 1, Update = 2, Activate = 3, Deactivate = 4, Merge = 5 }
public sealed record ManageDrugCatalogCommand(DrugCatalogMutation Mutation, Guid? DrugId, DrugData? Data = null,
    string? RowVersion = null, string? Reason = null, Guid? TargetId = null, string? IdempotencyKey = null)
    : ICommand<DrugCatalogDetailsResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record ListDrugRequestsQuery(bool Own, Guid? RequestId = null, DrugCatalogRequestStatus? Status = null,
    Guid? DoctorId = null, string? Search = null, int PageNumber = 1, int PageSize = 20) : IQuery<ClinicalPage<DrugRequestResponse>>;
public sealed record SaveDrugRequestCommand(MedicationRequestData Data, Guid? RequestId = null, string? RowVersion = null)
    : ICommand<DrugRequestResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record ReviewDrugRequestCommand(Guid RequestId, DrugCatalogRequestStatus Status, string RowVersion,
    string? Reason = null, DrugData? ApprovedData = null, Guid? DuplicateOfDrugCatalogId = null, string? IdempotencyKey = null)
    : ICommand<DrugRequestResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record PreviewDrugImportCommand(byte[] File, string? SourceVersion, string? SourceCommitSha)
    : ICommand<DrugImportBatchResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record ApplyDrugImportCommand(Guid BatchId, string IdempotencyKey)
    : ICommand<DrugImportBatchResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record ListDrugImportsQuery(Guid? BatchId = null, int PageNumber = 1, int PageSize = 20) : IQuery<ClinicalPage<DrugImportBatchResponse>>;
public sealed record ListDrugImportChangesQuery(Guid BatchId, DrugImportChangeType? ChangeType = null, int PageNumber = 1, int PageSize = 20)
    : IQuery<ClinicalPage<DrugImportRecordResponse>>;

public enum PrescriptionMutation { AddItem = 1, UpdateItem = 2, RemoveItem = 3, StartCorrection = 4, FinalizeCorrection = 5, DiscardCorrection = 6, Void = 7 }
public sealed record MutatePrescriptionCommand(PrescriptionMutation Mutation, Guid? PracticeId = null,
    Guid? EncounterId = null, Guid? PrescriptionId = null, Guid? ItemId = null, Guid? DrugCatalogId = null,
    MedicationRequestData? NewMedication = null, PrescriptionItemData? Data = null, string? RowVersion = null,
    string? Reason = null, string? IdempotencyKey = null, bool Correction = false)
    : ICommand<PrescriptionStateResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record GetDoctorPrescriptionQuery(Guid PrescriptionId, bool CorrectionOnly = false) : IQuery<PrescriptionStateResponse>;
public sealed record ListPrescriptionVersionsQuery(Guid PrescriptionId, int? VersionNumber = null) : IQuery<IReadOnlyList<PrescriptionVersionResponse>>;
public sealed record ListMyPrescriptionsQuery(int PageNumber = 1, int PageSize = 20) : IQuery<ClinicalPage<PatientPrescriptionSummaryResponse>>;
public sealed record GetMyPrescriptionQuery(Guid PrescriptionId) : IQuery<PatientPrescriptionResponse>;
