using System.Security.Cryptography;
using BuildingBlock.Domain.Results;
using Microsoft.EntityFrameworkCore;
using Wasla.Application.Features.Medications;
using Wasla.Domain.Medications;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed class DrugCatalogImportService(WaslaDbContext db) : IDrugCatalogImportService
{
    public async Task<Result<DrugCatalogImportBatch>> StageAsync(byte[] file, string? sourceVersion, string? commit,
        Guid actor, DateTime now, CancellationToken ct)
    {
        var parsed = DrugImportParser.Parse(file);
        if (parsed.IsFailure) return Result<DrugCatalogImportBatch>.Fail(parsed.Errors);
        // Full materialization is restricted to controlled imports, never runtime Doctor search.
        var existing = await db.DrugCatalogs.AsNoTracking().ToArrayAsync(ct);
        var identities = existing.Where(d => d.SourceIdentityFingerprint is not null).ToDictionary(d => d.SourceIdentityFingerprint!, StringComparer.Ordinal);
        var names = existing.ToLookup(d => d.NormalizedCommercialNameEn, StringComparer.Ordinal);
        var scientific = existing.Where(d => !string.IsNullOrWhiteSpace(d.NormalizedScientificName) && !string.IsNullOrWhiteSpace(d.NormalizedManufacturer))
            .ToLookup(d => (d.NormalizedScientificName, d.NormalizedManufacturer));
        var seenContent = new HashSet<string>(StringComparer.Ordinal); var seenIdentity = new HashSet<string>(StringComparer.Ordinal);
        var batch = DrugCatalogImportBatch.Stage(Convert.ToHexString(SHA256.HashData(file)), sourceVersion, commit, actor, now);
        foreach (var row in parsed.Value)
        {
            identities.TryGetValue(row.IdentityFingerprint, out var match);
            DrugImportChangeType change;
            if (!seenContent.Add(row.ContentHash)) change = DrugImportChangeType.ExactDuplicate;
            else if (!seenIdentity.Add(row.IdentityFingerprint)) change = DrugImportChangeType.PossibleDuplicate;
            else if (match is not null)
                change = match.SourceContentHash == row.ContentHash ? DrugImportChangeType.Unchanged :
                    match.IsPriceManuallyOverridden ? DrugImportChangeType.PossibleDuplicate : DrugImportChangeType.PriceChange;
            else
            {
                var candidates = names[MedicationText.Normalize(row.Data.CommercialNameEn)].ToArray();
                if (candidates.Length == 0 && !string.IsNullOrWhiteSpace(row.Data.ScientificName) && !string.IsNullOrWhiteSpace(row.Data.Manufacturer))
                    candidates = scientific[(MedicationText.Normalize(row.Data.ScientificName), MedicationText.Normalize(row.Data.Manufacturer))].ToArray();
                // Clinical identity candidates are staged and never overwrite or merge existing data.
                change = candidates.Length > 0 ? DrugImportChangeType.PossibleDuplicate :
                    DrugCatalog.InitialStatus(row.Data).Status == DrugCatalogStatus.NeedsReview ? DrugImportChangeType.NeedsReview : DrugImportChangeType.New;
            }
            batch.AddRecord(row.RowNumber, row.Data, row.IdentityFingerprint, row.ContentHash, change, match?.Id, match?.RowVersion, now);
        }
        foreach (var drug in existing.Where(d => d.OriginType == DrugOriginType.MedicianDB && d.SourceIdentityFingerprint is not null && !seenIdentity.Contains(d.SourceIdentityFingerprint)))
            batch.AddRecord(0, drug.Data(), drug.SourceIdentityFingerprint!, drug.SourceContentHash ?? string.Empty,
                DrugImportChangeType.MissingFromSource, drug.Id, drug.RowVersion, now);
        db.DrugCatalogImportBatches.Add(batch);
        await db.SaveChangesAsync(ct);
        return Result<DrugCatalogImportBatch>.Ok(batch);
    }
    public async Task<Result<DrugCatalogImportBatch>> ApplyAsync(Guid batchId, Guid actor, DateTime now, CancellationToken ct)
    {
        var batch = await db.DrugCatalogImportBatches.Include(b => b.Records).AsSplitQuery().SingleOrDefaultAsync(b => b.Id == batchId, ct);
        if (batch is null) return Result<DrugCatalogImportBatch>.Fail(MedicationErrors.NotFound("DrugCatalogImport.BatchNotFound"));
        if (batch.Status == DrugImportStatus.Applied) return Result<DrugCatalogImportBatch>.Ok(batch);
        if (batch.Status != DrugImportStatus.Staged) return Result<DrugCatalogImportBatch>.Fail(MedicationErrors.Conflict("DrugCatalogImport.BatchNotReady"));
        var drugs = await db.DrugCatalogs.ToArrayAsync(ct);
        var byId = drugs.ToDictionary(d => d.Id);
        var identities = drugs.Where(d => d.SourceIdentityFingerprint is not null).ToDictionary(d => d.SourceIdentityFingerprint!, StringComparer.Ordinal);
        // A preview is a reviewable plan. Catalog changes after staging require a new preview.
        foreach (var row in batch.Records.Where(r => r.MatchedDrugCatalogId is not null))
            if (!byId.TryGetValue(row.MatchedDrugCatalogId!.Value, out var drug) || row.MatchedRowVersion is null || !drug.RowVersion.SequenceEqual(row.MatchedRowVersion))
                return Result<DrugCatalogImportBatch>.Fail(MedicationErrors.Conflict("DrugCatalogImport.ConcurrencyConflict"));
        var input = batch.Records.Where(r => r.SourceRowNumber > 0).OrderBy(r => r.SourceRowNumber).ToArray();
        foreach (var row in input)
        {
            if (row.ChangeType == DrugImportChangeType.ExactDuplicate) continue;
            if (row.MatchedDrugCatalogId is { } matched)
            {
                var drug = byId[matched];
                if (row.ChangeType == DrugImportChangeType.PriceChange) drug.ApplyImportPrice(row.PriceEgp, row.ContentHash, actor, now);
                drug.MarkSourcePresence(true, batchId, actor, now); continue;
            }
            if (identities.ContainsKey(row.IdentityFingerprint))
            {
                // A second price variant of the same identity is retained in staging for review.
                if (row.ChangeType == DrugImportChangeType.PossibleDuplicate) continue;
                return Result<DrugCatalogImportBatch>.Fail(MedicationErrors.Conflict("DrugCatalogImport.ConcurrencyConflict"));
            }
            var created = DrugCatalog.Create(row.Data(), DrugOriginType.MedicianDB, actor, now, batchId,
                fingerprint: row.IdentityFingerprint, contentHash: row.ContentHash);
            if (created.IsFailure) return Result<DrugCatalogImportBatch>.Fail(created.Errors);
            if (row.ChangeType == DrugImportChangeType.PossibleDuplicate && created.Value.Status == DrugCatalogStatus.Active)
            {
                // A review candidate is not automatically prescribable; it preserves the complete source row.
                created.Value.RequireImportReview(actor, now);
            }
            identities.Add(row.IdentityFingerprint, created.Value); db.DrugCatalogs.Add(created.Value);
        }
        var sourceIdentities = input.Select(r => r.IdentityFingerprint).ToHashSet(StringComparer.Ordinal);
        foreach (var drug in drugs.Where(d => d.OriginType == DrugOriginType.MedicianDB && d.SourceIdentityFingerprint is not null))
            drug.MarkSourcePresence(sourceIdentities.Contains(drug.SourceIdentityFingerprint!), batchId, actor, now);
        var applied = batch.MarkApplied(now);
        if (applied.IsFailure) return Result<DrugCatalogImportBatch>.Fail(applied.Errors);
        // EF batches statements; no per-record SaveChanges and no runtime source/network dependency.
        await db.SaveChangesAsync(ct);
        return Result<DrugCatalogImportBatch>.Ok(batch);
    }
}
