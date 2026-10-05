using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Results;

namespace Wasla.Domain.Medications;

public enum DrugImportStatus { Staged = 1, Applied = 2, Failed = 3, Discarded = 4 }
public enum DrugImportChangeType { New = 1, Unchanged = 2, PriceChange = 3, NeedsReview = 4, MissingFromSource = 5, PossibleDuplicate = 6, ExactDuplicate = 7 }

public sealed class DrugCatalogImportBatch : AggregateRoot<Guid>
{
    private readonly List<DrugCatalogImportRecord> _records = [];
    private DrugCatalogImportBatch() { }
    public string Source { get; private set; } = "MedicianDB";
    public string? SourceVersion { get; private set; }
    public string? SourceCommitSha { get; private set; }
    public string FileSha256 { get; private set; } = string.Empty;
    public DrugImportStatus Status { get; private set; }
    public int TotalRecords { get; private set; }
    public int NewRecords { get; private set; }
    public int UnchangedRecords { get; private set; }
    public int PriceChanges { get; private set; }
    public int NeedsReviewRecords { get; private set; }
    public int MissingRecords { get; private set; }
    public int PossibleDuplicateRecords { get; private set; }
    public int ExactDuplicateRecords { get; private set; }
    public Guid CreatedByApplicationUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? AppliedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyCollection<DrugCatalogImportRecord> Records => _records.AsReadOnly();
    public static DrugCatalogImportBatch Stage(string hash, string? version, string? commit, Guid actor, DateTime now)
        => new() { Id = Guid.NewGuid(), FileSha256 = hash, SourceVersion = version, SourceCommitSha = commit,
            CreatedByApplicationUserId = actor, CreatedAtUtc = now, Status = DrugImportStatus.Staged };
    public void AddRecord(int row, DrugData data, string fingerprint, string hash, DrugImportChangeType change,
        Guid? matchedId, byte[]? matchedRowVersion, DateTime now)
    {
        if (Status != DrugImportStatus.Staged) throw new InvalidOperationException("Import staging is closed.");
        _records.Add(DrugCatalogImportRecord.Create(Id, row, data, fingerprint, hash, change, matchedId, matchedRowVersion, now));
        if (row > 0) TotalRecords++;
        switch (change)
        {
            case DrugImportChangeType.New: NewRecords++; break;
            case DrugImportChangeType.Unchanged: UnchangedRecords++; break;
            case DrugImportChangeType.PriceChange: PriceChanges++; break;
            case DrugImportChangeType.NeedsReview: NeedsReviewRecords++; NewRecords++; break;
            case DrugImportChangeType.MissingFromSource: MissingRecords++; break;
            case DrugImportChangeType.PossibleDuplicate: PossibleDuplicateRecords++; break;
            case DrugImportChangeType.ExactDuplicate: ExactDuplicateRecords++; break;
        }
    }
    public Result MarkApplied(DateTime now)
    {
        if (Status == DrugImportStatus.Applied) return Result.Ok();
        if (Status != DrugImportStatus.Staged) return Result.Fail(MedicationErrors.Conflict("DrugCatalogImport.BatchNotReady"));
        Status = DrugImportStatus.Applied; AppliedAtUtc = now; return Result.Ok();
    }
}

public sealed class DrugCatalogImportRecord : Entity<Guid>
{
    private DrugCatalogImportRecord() { }
    public Guid ImportBatchId { get; private set; }
    public int SourceRowNumber { get; private set; }
    public string CommercialNameEn { get; private set; } = string.Empty;
    public string? CommercialNameAr { get; private set; }
    public string? ScientificName { get; private set; }
    public string? Manufacturer { get; private set; }
    public string? DrugClass { get; private set; }
    public string? Route { get; private set; }
    public decimal? PriceEgp { get; private set; }
    public string IdentityFingerprint { get; private set; } = string.Empty;
    public string ContentHash { get; private set; } = string.Empty;
    public DrugImportChangeType ChangeType { get; private set; }
    public Guid? MatchedDrugCatalogId { get; private set; }
    public byte[]? MatchedRowVersion { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DrugData Data() => new(CommercialNameEn, CommercialNameAr, ScientificName, Manufacturer, DrugClass, Route, PriceEgp: PriceEgp);
    internal static DrugCatalogImportRecord Create(Guid batch, int row, DrugData data, string fingerprint,
        string hash, DrugImportChangeType change, Guid? match, byte[]? rowVersion, DateTime now)
        => new() { Id = Guid.NewGuid(), ImportBatchId = batch, SourceRowNumber = row, CommercialNameEn = data.CommercialNameEn,
            CommercialNameAr = data.CommercialNameAr, ScientificName = data.ScientificName, Manufacturer = data.Manufacturer,
            DrugClass = data.DrugClass, Route = data.Route, PriceEgp = data.PriceEgp, IdentityFingerprint = fingerprint,
            ContentHash = hash, ChangeType = change, MatchedDrugCatalogId = match, MatchedRowVersion = rowVersion, CreatedAtUtc = now };
}
