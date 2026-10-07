using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Results;
using Wasla.Domain.Diagnostics;

namespace Wasla.Domain.Labs;

public sealed class LabCatalogImportBatch : AggregateRoot<Guid>
{
    private readonly List<LabCatalogImportRecord> _records = [];
    private LabCatalogImportBatch() { }
    public string SourceVersion { get; private set; } = string.Empty;
    public string OriginalFileName { get; private set; } = string.Empty;
    public string FileSha256 { get; private set; } = string.Empty;
    public Guid UploadedByUserId { get; private set; }
    public DateTime UploadedAtUtc { get; private set; }
    public DiagnosticImportStatus Status { get; private set; }
    public int TotalRecords { get; private set; }
    public DateTime? AppliedAtUtc { get; private set; }
    public Guid? AppliedByUserId { get; private set; }
    public DateTime? DiscardedAtUtc { get; private set; }
    public Guid? DiscardedByUserId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyCollection<LabCatalogImportRecord> Records => _records.AsReadOnly();
    public static LabCatalogImportBatch Stage(string version, string name, string hash, Guid actor, DateTime now)
        => new() { Id = Guid.NewGuid(), SourceVersion = version, OriginalFileName = name, FileSha256 = hash, UploadedByUserId = actor, UploadedAtUtc = now, Status = DiagnosticImportStatus.Staged };
    public void Add(LoincSourceData data, string hash, DiagnosticImportDisposition disposition, Guid? match, byte[]? token)
    {
        if (Status != DiagnosticImportStatus.Staged) throw new InvalidOperationException("Closed staging batch.");
        _records.Add(LabCatalogImportRecord.Create(Id, data, hash, disposition, match, token)); TotalRecords++;
    }
    public Result Apply(Guid actor, DateTime now)
    {
        if (Status == DiagnosticImportStatus.Applied) return Result.Ok();
        if (Status != DiagnosticImportStatus.Staged) return Result.Fail(DiagnosticErrors.Conflict("DiagnosticCatalogImport.BatchNotStaged"));
        Status = DiagnosticImportStatus.Applied; AppliedByUserId = actor; AppliedAtUtc = now; return Result.Ok();
    }
    public Result Discard(Guid actor, DateTime now)
    {
        if (Status != DiagnosticImportStatus.Staged) return Result.Fail(DiagnosticErrors.Conflict("DiagnosticCatalogImport.BatchNotStaged"));
        Status = DiagnosticImportStatus.Discarded; DiscardedByUserId = actor; DiscardedAtUtc = now; return Result.Ok();
    }
}
public sealed class LabCatalogImportRecord : Entity<Guid>
{
    private LabCatalogImportRecord() { }
    public Guid ImportBatchId { get; private set; }
    public string LoincCode { get; private set; } = string.Empty;
    public string NameEn { get; private set; } = string.Empty;
    public string SourceDataJson { get; private set; } = string.Empty;
    public string SourceHash { get; private set; } = string.Empty;
    public DiagnosticImportDisposition Disposition { get; private set; }
    public Guid? MatchedCatalogId { get; private set; }
    public byte[]? MatchedRowVersion { get; private set; }
    internal static LabCatalogImportRecord Create(Guid batch, LoincSourceData data, string hash, DiagnosticImportDisposition disposition, Guid? match, byte[]? token)
        => new()
        {
            Id = Guid.NewGuid(),
            ImportBatchId = batch,
            LoincCode = data.Code,
            NameEn = data.NameEn,
            SourceDataJson = DiagnosticText.Json(data),
            SourceHash = hash,
            Disposition = disposition,
            MatchedCatalogId = match,
            MatchedRowVersion = token
        };
}
