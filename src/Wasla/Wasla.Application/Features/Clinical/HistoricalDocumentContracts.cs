using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Media;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Domain.Results;
using Wasla.Application.Media;
using Wasla.Application.Persistence;
using Wasla.Domain.Patients;

namespace Wasla.Application.Features.Clinical;

public sealed class HistoricalDocumentOptions
{
    public bool Enabled { get; set; }
    public bool RequireRealScanner { get; set; } = true;
    public int MaxFiles { get; set; } = 5;
    public long MaxFileBytes { get; set; } = 10485760;
    public int MaxDocumentsPerSubject { get; set; } = 500;
    public long MaxBytesPerSubject { get; set; } = 1073741824;
    public int MaxWritesPerDay { get; set; } = 20;
    public bool IsValid() => MaxFiles is > 0 and <= 5 && MaxFileBytes is > 0 and <= 10485760 && MaxDocumentsPerSubject is > 0 and <= 2000 &&
        MaxBytesPerSubject is > 0 and <= 10737418240 && MaxWritesPerDay is > 0 and <= 100;
}
public sealed record HistoricalDocumentMetadata(HistoricalDocumentCategory Category, DateOnly? DocumentDate, bool DateUnknown,
    string? ProviderName, string? Title, string? PatientNote);
public sealed record HistoricalDocumentMutation(string Operation, Guid? ChildPatientId, Guid? DocumentId, string IdempotencyKey,
    string? RowVersion = null, HistoricalDocumentMetadata? Metadata = null, IReadOnlyList<MediaUpload>? Files = null, string? Reason = null)
    : ICommand<HistoricalDocumentMutationResponse>, ITransactionalCommand<WaslaWritePersistence>;
public sealed record HistoricalDocumentMutationResponse(Guid DocumentId, int VersionNumber, string Source = "PatientProvided", string VerificationStatus = "Unverified");
public sealed record HistoricalDocumentQuery(Guid? ChildPatientId = null, Guid? DocumentId = null, bool Versions = false, int PageNumber = 1, int PageSize = 20, bool IncludeWithdrawn = false) : IQuery<object>;
public sealed record HistoricalDocumentMediaQuery(Guid? ChildPatientId, Guid DocumentId, Guid AttachmentId) : IQuery<PrivateMedia>;
public sealed record HistoricalAttachmentResponse(Guid AttachmentId, string FileName, string ContentType, long Length);
public sealed record HistoricalVersionResponse(int VersionNumber, HistoricalDocumentMetadata Metadata, DateTime CreatedAtUtc, IReadOnlyList<HistoricalAttachmentResponse> Attachments);
public sealed record HistoricalDocumentResponse(Guid DocumentId, Guid SubjectPatientId, HistoricalDocumentState State, int CurrentVersionNumber,
    DateTime UploadedAtUtc, string RowVersion, HistoricalVersionResponse CurrentVersion, bool CanRevise, bool CanWithdraw,
    string Source = "PatientProvided", string VerificationStatus = "Unverified");
public interface IHistoricalDocumentService
{
    Task<Result<HistoricalDocumentMutationResponse>> MutateAsync(HistoricalDocumentMutation command, CancellationToken ct);
    Task<Result<object>> ReadAsync(HistoricalDocumentQuery query, CancellationToken ct);
    Task<Result<PrivateMedia>> DownloadAsync(HistoricalDocumentMediaQuery query, CancellationToken ct);
}
internal sealed class HistoricalDocumentMutationHandler(IHistoricalDocumentService service) : ICommandHandler<HistoricalDocumentMutation, HistoricalDocumentMutationResponse>
{ public Task<Result<HistoricalDocumentMutationResponse>> Handle(HistoricalDocumentMutation request, CancellationToken ct) => service.MutateAsync(request, ct); }
internal sealed class HistoricalDocumentQueryHandler(IHistoricalDocumentService service) : IQueryHandler<HistoricalDocumentQuery, object>
{ public Task<Result<object>> Handle(HistoricalDocumentQuery request, CancellationToken ct) => service.ReadAsync(request, ct); }
internal sealed class HistoricalDocumentMediaHandler(IHistoricalDocumentService service) : IQueryHandler<HistoricalDocumentMediaQuery, PrivateMedia>
{ public Task<Result<PrivateMedia>> Handle(HistoricalDocumentMediaQuery request, CancellationToken ct) => service.DownloadAsync(request, ct); }
