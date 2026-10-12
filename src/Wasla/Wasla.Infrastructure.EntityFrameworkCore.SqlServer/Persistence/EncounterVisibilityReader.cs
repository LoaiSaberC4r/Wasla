using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Wasla.Application.Features.Clinical;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed class MedicalRecordTokens(IDataProtectionProvider provider) : IMedicalRecordTokens
{
    public string Protect<T>(string purpose, T value) => provider.CreateProtector("Wasla.Phase16.v1", purpose).Protect(JsonSerializer.Serialize(value));
    public T? Read<T>(string purpose, string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 4096) return default;
        try { return JsonSerializer.Deserialize<T>(provider.CreateProtector("Wasla.Phase16.v1", purpose).Unprotect(token)); }
        catch (Exception e) when (e is CryptographicException or JsonException or FormatException) { return default; }
    }
}
internal sealed record VisibilityCursor(Guid EncounterId, int Revision);
internal sealed class EncounterVisibilityReader(WaslaDbContext db, IMedicalRecordTokens tokens) : IEncounterVisibilityReader
{
    public async Task<RecordPage<VisibilityHistoryResponse>> HistoryAsync(Guid encounterId, string? cursor, int pageSize, CancellationToken ct)
    {
        var key = cursor is null ? null : tokens.Read<VisibilityCursor>("VisibilityHistory", cursor);
        if (cursor is not null && (key is null || key.EncounterId != encounterId)) throw new ArgumentException("Invalid cursor.", nameof(cursor));
        var rows = await db.Set<Wasla.Domain.Clinical.EncounterVisibilityHistory>().AsNoTracking()
            .Where(h => h.MedicalEncounterId == encounterId && (key == null || h.Revision < key.Revision))
            .OrderByDescending(h => h.Revision).Take(pageSize + 1)
            .Select(h => new VisibilityHistoryResponse(h.Id, h.OldVisibility, h.NewVisibility, h.Revision, h.ActorApplicationUserId, h.OccurredAtUtc, h.Source)).ToArrayAsync(ct);
        var items = rows.Take(pageSize).ToArray(); var more = rows.Length > pageSize;
        return new(items, more ? tokens.Protect("VisibilityHistory", new VisibilityCursor(encounterId, items[^1].Revision)) : null, more);
    }
}
