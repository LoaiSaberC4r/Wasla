
using System.Security.Cryptography;
using System.Text.Json;
using BuildingBlock.Domain.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.Diagnostics;
using Wasla.Application.Features.Tickets.Common;
using Wasla.Domain.Diagnostics;
using Wasla.Domain.Labs;
using Wasla.Domain.Radiology;
using Wasla.Domain.Security;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed class DiagnosticImportService(WaslaDbContext db, IDiagnosticReadService reader,
    IOptions<DiagnosticCatalogImportOptions> options) : IDiagnosticImportService
{
    public async Task<Result<DiagnosticImportBatchResponse>> StageAsync(PreviewDiagnosticImportCommand r, Guid actor, DateTime now, CancellationToken ct)
    {
        if (r.FileName.Length > 255 || Path.GetFileName(r.FileName) != r.FileName || !r.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            return Result<DiagnosticImportBatchResponse>.Fail(DiagnosticErrors.Validation("DiagnosticCatalogImport.InvalidArchive"));
        if (!r.File.CanRead || !r.File.CanSeek)
            return Result<DiagnosticImportBatchResponse>.Fail(DiagnosticErrors.Validation("DiagnosticCatalogImport.InvalidArchive"));
        if (r.File.Length > options.Value.MaxPackageBytes)
            return Result<DiagnosticImportBatchResponse>.Fail(DiagnosticErrors.Validation("DiagnosticCatalogImport.PackageTooLarge"));
        var fileHash = await HashPackageAsync(r.File, options.Value.MaxPackageBytes, ct);
        if (fileHash.IsFailure) return Result<DiagnosticImportBatchResponse>.Fail(fileHash.Errors);
        var parsed = LoincPackageParser.Parse(r.File, r.Kind, r.SourceVersion, options.Value, ct);
        if (parsed.IsFailure) return Result<DiagnosticImportBatchResponse>.Fail(parsed.Errors);
        return r.Kind == DiagnosticKind.Lab ? await StageLabAsync(r, parsed.Value, fileHash.Value, actor, now, ct) : await StageRadiologyAsync(r, parsed.Value, fileHash.Value, actor, now, ct);
    }
    private static async Task<Result<string>> HashPackageAsync(Stream package, long limit, CancellationToken ct)
    {
        package.Position = 0;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        long total = 0;
        int count;
        while ((count = await package.ReadAsync(buffer, ct)) > 0)
        {
            total = checked(total + count);
            if (total > limit) return Result<string>.Fail(DiagnosticErrors.Validation("DiagnosticCatalogImport.PackageTooLarge"));
            hash.AppendData(buffer, 0, count);
        }
        return Result<string>.Ok(Convert.ToHexString(hash.GetHashAndReset()));
    }
    public Task<Result<DiagnosticImportBatchResponse>> MutateAsync(MutateDiagnosticImportCommand r, Guid actor, DateTime now, CancellationToken ct)
        => r.Kind == DiagnosticKind.Lab ? MutateLabAsync(r, actor, now, ct) : MutateRadiologyAsync(r, actor, now, ct);
    private static DiagnosticImportDisposition Classify(string? hash, string? arabic, string? status, ParsedLoincRow row)
        => hash == row.Hash ? DiagnosticImportDisposition.Unchanged : status != row.Data.Status ? DiagnosticImportDisposition.ExternalStatusChanged :
            arabic is null && row.Data.OfficialNameAr is not null ? DiagnosticImportDisposition.ArabicAdded : arabic != row.Data.OfficialNameAr ? DiagnosticImportDisposition.ArabicChanged : DiagnosticImportDisposition.Changed;

    private async Task<Result<DiagnosticImportBatchResponse>> StageLabAsync(PreviewDiagnosticImportCommand r, IReadOnlyList<ParsedLoincRow> rows, string fileHash, Guid actor, DateTime now, CancellationToken ct)
    {
        // Controlled import projection only; normal search stays filtered and paged in SQL.
        var existing = await db.Set<LabTestCatalog>().IgnoreAutoIncludes().AsNoTracking().Select(c => new { c.Id, c.LoincCode, c.NormalizedName, c.SourceHash, c.OfficialNameAr, c.ExternalStatus, c.RowVersion, c.Source, c.Status }).ToArrayAsync(ct);
        var codes = existing.Where(c => c.LoincCode is not null).ToDictionary(c => c.LoincCode!, StringComparer.Ordinal);
        var names = existing.Where(c => c.Source == MedicalCatalogSource.Wasla && c.Status != MedicalCatalogStatus.Merged).Select(c => c.NormalizedName).ToHashSet(StringComparer.Ordinal);
        var batch = LabCatalogImportBatch.Stage(r.SourceVersion, r.FileName, fileHash, actor, now);
        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            codes.TryGetValue(row.Data.Code, out var match);
            var disposition = match is not null ? Classify(match.SourceHash, match.OfficialNameAr, match.ExternalStatus, row) :
                names.Contains(DiagnosticText.Normalize(row.Data.NameEn)) ? DiagnosticImportDisposition.PossibleConflict : DiagnosticImportDisposition.New;
            batch.Add(row.Data, row.Hash, disposition, match?.Id, match?.RowVersion);
        }
        db.Set<LabCatalogImportBatch>().Add(batch); await db.SaveChangesAsync(ct);
        return Result<DiagnosticImportBatchResponse>.Ok((DiagnosticImportBatchResponse)(await reader.ImportAsync(new(r.Kind, batch.Id), PermissionNames.MedicalCatalogManagerDefaults, ct))!);
    }
    private async Task<Result<DiagnosticImportBatchResponse>> MutateLabAsync(MutateDiagnosticImportCommand r, Guid actor, DateTime now, CancellationToken ct)
    {
        var batch = await db.Set<LabCatalogImportBatch>().Include(b => b.Records).SingleOrDefaultAsync(b => b.Id == r.BatchId, ct);
        if (batch is null) return Result<DiagnosticImportBatchResponse>.Fail(DiagnosticErrors.NotFound("DiagnosticCatalogImport.BatchNotFound"));
        if (batch.Status == DiagnosticImportStatus.Applied && r.Apply) return Result<DiagnosticImportBatchResponse>.Ok((DiagnosticImportBatchResponse)(await reader.ImportAsync(new(r.Kind, batch.Id), PermissionNames.MedicalCatalogManagerDefaults, ct))!);
        if (Convert.ToBase64String(batch.RowVersion) != r.RowVersion) return Result<DiagnosticImportBatchResponse>.Fail(DiagnosticErrors.Conflict("DiagnosticCatalogImport.ConcurrencyConflict"));
        if (batch.Status != DiagnosticImportStatus.Staged) return Result<DiagnosticImportBatchResponse>.Fail(DiagnosticErrors.Conflict("DiagnosticCatalogImport.BatchNotStaged"));
        if (r.Apply)
        {
            if (!r.SkipPossibleConflicts && batch.Records.Any(x => x.Disposition == DiagnosticImportDisposition.PossibleConflict))
                return Result<DiagnosticImportBatchResponse>.Fail(DiagnosticErrors.Conflict("DiagnosticCatalogImport.PossibleConflict"));
            var existing = await db.Set<LabTestCatalog>().IgnoreAutoIncludes().ToDictionaryAsync(c => c.LoincCode ?? c.Id.ToString(), StringComparer.Ordinal, ct);
            foreach (var row in batch.Records)
            {
                existing.TryGetValue(row.LoincCode, out var match);
                if (row.MatchedCatalogId is { } expected && (match is null || match.Id != expected || row.MatchedRowVersion is null || !match.RowVersion.SequenceEqual(row.MatchedRowVersion)))
                    return Result<DiagnosticImportBatchResponse>.Fail(DiagnosticErrors.Conflict("DiagnosticCatalogImport.ConcurrencyConflict"));
                if (row.MatchedCatalogId is null && match is not null) return Result<DiagnosticImportBatchResponse>.Fail(DiagnosticErrors.Conflict("DiagnosticCatalogImport.ConcurrencyConflict"));
            }
            foreach (var row in batch.Records)
            {
                if (row.Disposition is DiagnosticImportDisposition.Skipped or DiagnosticImportDisposition.PossibleConflict) continue;
                var data = JsonSerializer.Deserialize<LoincSourceData>(row.SourceDataJson)!;
                if (existing.TryGetValue(row.LoincCode, out var catalog)) catalog.ApplySource(data, row.SourceHash, actor, now);
                else
                {
                    var created = LabTestCatalog.Import(data, row.SourceHash, actor, now);
                    if (created.IsFailure) return Result<DiagnosticImportBatchResponse>.Fail(created.Errors);
                    db.Set<LabTestCatalog>().Add(created.Value);
                }
            }
            var applied = batch.Apply(actor, now); if (applied.IsFailure) return Result<DiagnosticImportBatchResponse>.Fail(applied.Errors);
        }
        else
        {
            var discarded = batch.Discard(actor, now); if (discarded.IsFailure) return Result<DiagnosticImportBatchResponse>.Fail(discarded.Errors);
        }
        await db.SaveChangesAsync(ct);
        return Result<DiagnosticImportBatchResponse>.Ok((DiagnosticImportBatchResponse)(await reader.ImportAsync(new(r.Kind, batch.Id), PermissionNames.MedicalCatalogManagerDefaults, ct))!);
    }


    private async Task<Result<DiagnosticImportBatchResponse>> StageRadiologyAsync(PreviewDiagnosticImportCommand r, IReadOnlyList<ParsedLoincRow> rows, string fileHash, Guid actor, DateTime now, CancellationToken ct)
    {
        // Controlled import projection only; normal search stays filtered and paged in SQL.
        var existing = await db.Set<RadiologyProcedureCatalog>().IgnoreAutoIncludes().AsNoTracking().Select(c => new { c.Id, c.LoincCode, c.NormalizedName, c.SourceHash, c.OfficialNameAr, c.ExternalStatus, c.RowVersion, c.Source, c.Status }).ToArrayAsync(ct);
        var codes = existing.Where(c => c.LoincCode is not null).ToDictionary(c => c.LoincCode!, StringComparer.Ordinal);
        var names = existing.Where(c => c.Source == MedicalCatalogSource.Wasla && c.Status != MedicalCatalogStatus.Merged).Select(c => c.NormalizedName).ToHashSet(StringComparer.Ordinal);
        var batch = RadiologyCatalogImportBatch.Stage(r.SourceVersion, r.FileName, fileHash, actor, now);
        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            codes.TryGetValue(row.Data.Code, out var match);
            var disposition = match is not null ? Classify(match.SourceHash, match.OfficialNameAr, match.ExternalStatus, row) :
                names.Contains(DiagnosticText.Normalize(row.Data.NameEn)) ? DiagnosticImportDisposition.PossibleConflict : DiagnosticImportDisposition.New;
            batch.Add(row.Data, row.Hash, disposition, match?.Id, match?.RowVersion);
        }
        db.Set<RadiologyCatalogImportBatch>().Add(batch); await db.SaveChangesAsync(ct);
        return Result<DiagnosticImportBatchResponse>.Ok((DiagnosticImportBatchResponse)(await reader.ImportAsync(new(r.Kind, batch.Id), PermissionNames.MedicalCatalogManagerDefaults, ct))!);
    }
    private async Task<Result<DiagnosticImportBatchResponse>> MutateRadiologyAsync(MutateDiagnosticImportCommand r, Guid actor, DateTime now, CancellationToken ct)
    {
        var batch = await db.Set<RadiologyCatalogImportBatch>().Include(b => b.Records).SingleOrDefaultAsync(b => b.Id == r.BatchId, ct);
        if (batch is null) return Result<DiagnosticImportBatchResponse>.Fail(DiagnosticErrors.NotFound("DiagnosticCatalogImport.BatchNotFound"));
        if (batch.Status == DiagnosticImportStatus.Applied && r.Apply) return Result<DiagnosticImportBatchResponse>.Ok((DiagnosticImportBatchResponse)(await reader.ImportAsync(new(r.Kind, batch.Id), PermissionNames.MedicalCatalogManagerDefaults, ct))!);
        if (Convert.ToBase64String(batch.RowVersion) != r.RowVersion) return Result<DiagnosticImportBatchResponse>.Fail(DiagnosticErrors.Conflict("DiagnosticCatalogImport.ConcurrencyConflict"));
        if (batch.Status != DiagnosticImportStatus.Staged) return Result<DiagnosticImportBatchResponse>.Fail(DiagnosticErrors.Conflict("DiagnosticCatalogImport.BatchNotStaged"));
        if (r.Apply)
        {
            if (!r.SkipPossibleConflicts && batch.Records.Any(x => x.Disposition == DiagnosticImportDisposition.PossibleConflict))
                return Result<DiagnosticImportBatchResponse>.Fail(DiagnosticErrors.Conflict("DiagnosticCatalogImport.PossibleConflict"));
            var existing = await db.Set<RadiologyProcedureCatalog>().IgnoreAutoIncludes().ToDictionaryAsync(c => c.LoincCode ?? c.Id.ToString(), StringComparer.Ordinal, ct);
            foreach (var row in batch.Records)
            {
                existing.TryGetValue(row.LoincCode, out var match);
                if (row.MatchedCatalogId is { } expected && (match is null || match.Id != expected || row.MatchedRowVersion is null || !match.RowVersion.SequenceEqual(row.MatchedRowVersion)))
                    return Result<DiagnosticImportBatchResponse>.Fail(DiagnosticErrors.Conflict("DiagnosticCatalogImport.ConcurrencyConflict"));
                if (row.MatchedCatalogId is null && match is not null) return Result<DiagnosticImportBatchResponse>.Fail(DiagnosticErrors.Conflict("DiagnosticCatalogImport.ConcurrencyConflict"));
            }
            foreach (var row in batch.Records)
            {
                if (row.Disposition is DiagnosticImportDisposition.Skipped or DiagnosticImportDisposition.PossibleConflict) continue;
                var data = JsonSerializer.Deserialize<LoincSourceData>(row.SourceDataJson)!;
                if (existing.TryGetValue(row.LoincCode, out var catalog)) catalog.ApplySource(data, row.SourceHash, actor, now);
                else
                {
                    var created = RadiologyProcedureCatalog.Import(data, row.SourceHash, actor, now);
                    if (created.IsFailure) return Result<DiagnosticImportBatchResponse>.Fail(created.Errors);
                    db.Set<RadiologyProcedureCatalog>().Add(created.Value);
                }
            }
            var applied = batch.Apply(actor, now); if (applied.IsFailure) return Result<DiagnosticImportBatchResponse>.Fail(applied.Errors);
        }
        else
        {
            var discarded = batch.Discard(actor, now); if (discarded.IsFailure) return Result<DiagnosticImportBatchResponse>.Fail(discarded.Errors);
        }
        await db.SaveChangesAsync(ct);
        return Result<DiagnosticImportBatchResponse>.Ok((DiagnosticImportBatchResponse)(await reader.ImportAsync(new(r.Kind, batch.Id), PermissionNames.MedicalCatalogManagerDefaults, ct))!);
    }

}

