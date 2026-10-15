using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed record ArchiveCursor(string Binding, DateTime AsOfUtc, DateTime? LastTime, DateOnly? LastDate, Guid LastId, string? LastSource = null, long? Watermark = null);
internal sealed class ArchiveTokens(IDataProtectionProvider provider, WaslaDbContext db)
{
    private readonly IDataProtector cursor = provider.CreateProtector("Wasla.Phase18.PartTwo.Cursor.v1");

    public static string Binding(object filters, Guid actor, IEnumerable<Guid> scopes, IEnumerable<string> permissions)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { filters, actor,
            scopes = scopes.Order().ToArray(), permissions = permissions.Order(StringComparer.Ordinal).ToArray() }))));
    public string Encode(ArchiveCursor value) => cursor.Protect(JsonSerializer.Serialize(value));
    public ArchiveCursor? Decode(string? value, string binding)
    {
        if (value == null) return null;
        if (value.Length > 8192) throw new FormatException("Archive.InvalidCursor");
        try
        {
            var decoded = JsonSerializer.Deserialize<ArchiveCursor>(cursor.Unprotect(value));
            return decoded != null && decoded.Binding == binding ? decoded : throw new FormatException("Archive.InvalidCursor");
        }
        catch (Exception e) when (e is CryptographicException or JsonException or ArgumentException) { throw new FormatException("Archive.InvalidCursor"); }
    }
    private byte[]? eventSecret;
    public async Task InitialiseEventsAsync(CancellationToken ct)
    {
        if (eventSecret != null) return;
        var state = await db.ArchiveControlStates.SingleOrDefaultAsync(ct);
        if (state == null) { state = new(); db.ArchiveControlStates.Add(state); await db.SaveChangesAsync(ct); }
        eventSecret = state.EventKeySecret;
    }
    // Stable encrypted identities. Deterministic keyed nonces are derived from unique source+GUID;
    // different events have distinct nonces, and repeated identical payloads produce identical keys.
    public string Event(string source, Guid id)
    {
        var plain = Encoding.UTF8.GetBytes(source + ":" + id.ToString("N"));
        var secret = eventSecret ?? throw new InvalidOperationException("Archive.EventKeyUnavailable");
        var nonce = HMACSHA256.HashData(secret, plain)[..12]; var cipher = new byte[plain.Length]; var tag = new byte[16];
        using var aes = new AesGcm(secret, 16); aes.Encrypt(nonce, plain, cipher, tag);
        return Convert.ToBase64String([.. nonce, .. cipher, .. tag]).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
    public (string Source, Guid Id)? ResolveEvent(string key)
    {
        if (key.Length > 8192) return null;
        try
        {
            var encoded = key.Replace('-', '+').Replace('_', '/'); encoded = encoded.PadRight((encoded.Length + 3) / 4 * 4, '=');
            var bytes = Convert.FromBase64String(encoded); if (bytes.Length < 29) return null; var plain = new byte[bytes.Length - 28];
            using var aes = new AesGcm(eventSecret ?? throw new InvalidOperationException("Archive.EventKeyUnavailable"), 16);
            aes.Decrypt(bytes.AsSpan(0, 12), bytes.AsSpan(12, plain.Length), bytes.AsSpan(bytes.Length - 16), plain);
            var text = Encoding.UTF8.GetString(plain).Split(':'); return text.Length == 2 && Guid.TryParseExact(text[1], "N", out var id) ? (text[0], id) : null;
        }
        catch (Exception e) when (e is CryptographicException or ArgumentException or FormatException) { return null; }
    }
}
