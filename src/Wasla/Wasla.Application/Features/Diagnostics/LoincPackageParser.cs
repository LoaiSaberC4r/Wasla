using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using BuildingBlock.Domain.Results;
using Wasla.Domain.Diagnostics;

namespace Wasla.Application.Features.Diagnostics;

public sealed class DiagnosticCatalogImportOptions
{
    public const string SectionName = "DiagnosticCatalogImport";
    public long MaxPackageBytes { get; set; } = 200L * 1024 * 1024;
    public long MaxExpandedBytes { get; set; } = 2L * 1024 * 1024 * 1024;
    public int MaxEntries { get; set; } = 4096;
    public int MaxRows { get; set; } = 500000;
    public int MaxCompressionRatio { get; set; } = 250;
    public string[] SupportedVersions { get; set; } = ["2.83"];
}
public sealed record ParsedLoincRow(LoincSourceData Data, string Hash);
public static partial class LoincPackageParser
{
    private static readonly string[] ArabicLocalizedFields = ["LinguisticVariantDisplayName", "LONG_COMMON_NAME_AR", "DisplayName"];
    [GeneratedRegex(@"^\d{1,7}-\d$")]
    private static partial Regex CodePattern();
    public static Result<IReadOnlyList<ParsedLoincRow>> Parse(byte[] bytes, DiagnosticKind kind, string version,
        DiagnosticCatalogImportOptions options, CancellationToken ct = default)
    {
        using var content = new MemoryStream(bytes, writable: false);
        return Parse(content, kind, version, options, ct);
    }
    // The caller owns a seekable upload/file stream. Reject non-seekable input rather than letting ZipArchive buffer it.
    public static Result<IReadOnlyList<ParsedLoincRow>> Parse(Stream content, DiagnosticKind kind, string version,
        DiagnosticCatalogImportOptions options, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!content.CanRead || !content.CanSeek) return Failure("InvalidArchive");
        if (content.Length > options.MaxPackageBytes) return Failure("PackageTooLarge");
        if (!options.SupportedVersions.Contains(version, StringComparer.Ordinal)) return Failure("UnsupportedSourceVersion");
        try
        {
            content.Position = 0;
            using var archive = new ZipArchive(content, ZipArchiveMode.Read, leaveOpen: true);
            if (archive.Entries.Count > options.MaxEntries) return Failure("UnsafeArchive");
            long expanded = 0;
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in archive.Entries)
            {
                ct.ThrowIfCancellationRequested();
                var path = NormalizePath(entry.FullName);
                if (path.StartsWith('/') || path.Contains(':') || path.Contains('\0') || path.Split('/').Any(s => s is "." or "..") ||
                    !paths.Add(path) || ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000) return Failure("UnsafeArchive");
                expanded = checked(expanded + entry.Length);
                if (expanded > options.MaxExpandedBytes || entry.Length > 1024L * 1024 * 1024 ||
                    entry.Length > 1024 * 1024 && entry.Length / Math.Max(1, entry.CompressedLength) > options.MaxCompressionRatio)
                    return Failure("UnsafeArchive");
            }
            ZipArchiveEntry FindExactPath(string path)
            {
                var matches = archive.Entries.Where(e => string.Equals(NormalizePath(e.FullName), path, StringComparison.OrdinalIgnoreCase)).ToArray();
                if (matches.Length > 1) throw new PackageException("UnsafeArchive");
                if (matches.Length == 0) throw new PackageException("RequiredFileMissing");
                return matches[0];
            }
            ZipArchiveEntry? FindAccessory(string officialPath, bool required)
            {
                var name = officialPath[(officialPath.LastIndexOf('/') + 1)..];
                var matches = archive.Entries.Where(e => string.Equals(
                    NormalizePath(e.FullName).Split('/')[^1], name, StringComparison.OrdinalIgnoreCase)).ToArray();
                // Accessories in official 2.83 are unique. Preserve legacy safe layouts, but never pick an ambiguous basename.
                if (matches.Length > 1) throw new PackageException("UnsafeArchive");
                if (matches.Length == 0 && required) throw new PackageException("RequiredFileMissing");
                var exact = matches.SingleOrDefault(e => string.Equals(NormalizePath(e.FullName), officialPath, StringComparison.OrdinalIgnoreCase));
                if (exact is not null) return exact;
                return matches.SingleOrDefault();
            }
            var loincEntry = FindExactPath("LoincTable/Loinc.csv");
            var rows = Read(loincEntry, options, ct, "LOINC_NUM", "COMPONENT", "STATUS", "CLASSTYPE", "ORDER_OBS", "LONG_COMMON_NAME");
            var source = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                var code = row["LOINC_NUM"];
                if (!CodePattern().IsMatch(code) || !source.TryAdd(code, row)) return Failure("DuplicateSourceIdentity");
            }
            var arabic = new Dictionary<string, string>(StringComparer.Ordinal);
            if (FindAccessory("AccessoryFiles/LinguisticVariants/arJO32LinguisticVariant.csv", false) is { } arEntry)
            {
                var arabicCodes = new HashSet<string>(StringComparer.Ordinal);
                foreach (var row in Read(arEntry, options, ct, "LOINC_NUM"))
                {
                    if (!CodePattern().IsMatch(row["LOINC_NUM"]) || !arabicCodes.Add(row["LOINC_NUM"])) return Failure("DuplicateSourceIdentity");
                    var name = GetArabicLocalizedName(row);
                    if (name is not null && !arabic.TryAdd(row["LOINC_NUM"], name)) return Failure("DuplicateSourceIdentity");
                }
            }
            var common = new HashSet<string>(StringComparer.Ordinal);
            if (kind == DiagnosticKind.Lab)
            {
                foreach (var row in Read(FindAccessory("AccessoryFiles/LoincUniversalLabOrdersValueSet/LoincUniversalLabOrdersValueSet.csv", true)!, options, ct))
                {
                    var code = Get(row, "LOINC_NUM") ?? Get(row, "LoincNumber") ?? Get(row, "LOINC") ?? Get(row, "LOINC Code");
                    if (code is null) return Failure("InvalidHeaders");
                    if (!CodePattern().IsMatch(code) || !common.Add(code)) return Failure("DuplicateSourceIdentity");
                }
            }
            var attrs = new Dictionary<string, Dictionary<string, List<string>>>(StringComparer.Ordinal);
            var radiologyRows = new Dictionary<string, List<IReadOnlyDictionary<string, string>>>(StringComparer.Ordinal);
            if (kind == DiagnosticKind.Radiology)
            {
                foreach (var row in Read(FindAccessory("AccessoryFiles/LoincRsnaRadiologyPlaybook/LoincRsnaRadiologyPlaybook.csv", true)!, options, ct, "LoincNumber", "PartTypeName", "PartName"))
                {
                    var code = row["LoincNumber"];
                    if (!source.ContainsKey(code)) return Failure("InvalidArchive");
                    if (!radiologyRows.TryGetValue(code, out var originalRows)) radiologyRows.Add(code, originalRows = []);
                    originalRows.Add(row);
                    if (!attrs.TryGetValue(code, out var parts)) attrs.Add(code, parts = new(StringComparer.OrdinalIgnoreCase));
                    if (!parts.TryGetValue(row["PartTypeName"], out var values)) parts.Add(row["PartTypeName"], values = []);
                    values.Add(row["PartName"]);
                }
            }
            var output = new List<ParsedLoincRow>();
            foreach (var (code, fields) in source.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                ct.ThrowIfCancellationRequested();
                if (kind == DiagnosticKind.Lab && !(fields["CLASSTYPE"] == "1" && fields["ORDER_OBS"] is "Order" or "Both")) continue;
                if (kind == DiagnosticKind.Radiology && !attrs.ContainsKey(code)) continue;
                var name = fields["LONG_COMMON_NAME"];
                if (string.IsNullOrWhiteSpace(name)) return Failure("InvalidHeaders");
                var attributes = attrs.TryGetValue(code, out var parts) ? parts.OrderBy(x => x.Key, StringComparer.Ordinal)
                    .ToDictionary(x => x.Key, x => x.Value.Distinct(StringComparer.Ordinal).ToArray(), StringComparer.Ordinal) : new Dictionary<string, string[]>();
                var data = new LoincSourceData(code, name, arabic.GetValueOrDefault(code), fields["STATUS"], version, common.Contains(code),
                    fields.OrderBy(x => x.Key, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal), attributes,
                    radiologyRows.TryGetValue(code, out var originalParts) ? originalParts : null);
                var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(DiagnosticText.Json(data with { SourceVersion = string.Empty }))));
                output.Add(new(data, hash));
            }
            return Result<IReadOnlyList<ParsedLoincRow>>.Ok(output);
        }
        catch (PackageException ex) { return Failure(ex.Code); }
        catch (DecoderFallbackException) { return Failure("UnsupportedEncoding"); }
        catch (Exception ex) when (ex is InvalidDataException or IOException or OverflowException or ArgumentException or NotSupportedException)
        { return Failure("InvalidArchive"); }
    }
    private static string NormalizePath(string path) => path.Replace('\\', '/');
    private static string? Get(Dictionary<string, string> row, string field) => row.TryGetValue(field, out var value) ? value : null;
    private static string? GetArabicLocalizedName(Dictionary<string, string> row)
    {
        // LONG_COMMON_NAME remains English in the official linguistic variant. Only localized fields are candidates.
        foreach (var field in ArabicLocalizedFields)
        {
            var name = Get(row, field);
            if (string.IsNullOrWhiteSpace(name)) continue;
            // Require an Arabic letter, while preserving mixed gene names, numbers, units and source formatting.
            foreach (var rune in name.EnumerateRunes())
                if (Rune.IsLetter(rune) && rune.Value is >= 0x0600 and <= 0x06FF or >= 0x0750 and <= 0x077F or
                    >= 0x0870 and <= 0x08FF or >= 0xFB50 and <= 0xFDFF or >= 0xFE70 and <= 0xFEFF or >= 0x1EE00 and <= 0x1EEFF)
                    return name;
        }
        return null;
    }
    private static Result<IReadOnlyList<ParsedLoincRow>> Failure(string code)
        => Result<IReadOnlyList<ParsedLoincRow>>.Fail(DiagnosticErrors.Validation("DiagnosticCatalogImport." + code));
    private static IEnumerable<Dictionary<string, string>> Read(ZipArchiveEntry entry, DiagnosticCatalogImportOptions options,
        CancellationToken ct, params string[] required)
    {
        using var stream = new BoundedEntryStream(entry.Open(), Math.Min(entry.Length, options.MaxExpandedBytes), ct);
        // Official UTF-8 CSV only. Strict decoding rejects lossy replacement of source terminology.
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: false);
        using var records = Csv(reader).GetEnumerator();
        if (!records.MoveNext()) throw new PackageException("InvalidHeaders");
        var headers = records.Current; headers[0] = headers[0].TrimStart('\uFEFF');
        if (headers.Distinct(StringComparer.OrdinalIgnoreCase).Count() != headers.Length || required.Any(r => !headers.Contains(r, StringComparer.OrdinalIgnoreCase))) throw new PackageException("InvalidHeaders");
        if (NormalizePath(entry.FullName).Split('/')[^1].Equals("arJO32LinguisticVariant.csv", StringComparison.OrdinalIgnoreCase) &&
            !headers.Any(h => ArabicLocalizedFields.Contains(h, StringComparer.OrdinalIgnoreCase))) throw new PackageException("InvalidHeaders");
        var count = 0;
        while (records.MoveNext())
        {
            ct.ThrowIfCancellationRequested();
            if (++count > options.MaxRows) throw new PackageException("UnsafeArchive");
            var values = records.Current; if (values.Length == 1 && values[0].Length == 0) continue;
            if (values.Length != headers.Length) throw new PackageException("InvalidHeaders");
            yield return headers.Select((h, i) => (h, values[i])).ToDictionary(x => x.h, x => x.Item2, StringComparer.OrdinalIgnoreCase);
        }
    }
    // RFC 4180: quoted commas, escaped quotes, embedded CR/LF and strict closing quotes.
    private static IEnumerable<string[]> Csv(TextReader reader)
    {
        var cells = new List<string>(); var field = new StringBuilder(); bool quoted = false, closed = false, started = false;
        while (true)
        {
            var next = reader.Read();
            if (next < 0)
            {
                if (quoted) throw new PackageException("InvalidHeaders");
                if (started || cells.Count > 0 || field.Length > 0) { cells.Add(field.ToString()); yield return cells.ToArray(); }
                yield break;
            }
            var c = (char)next; started = true;
            if (quoted)
            {
                if (c == '"') { if (reader.Peek() == '"') { reader.Read(); field.Append('"'); } else { quoted = false; closed = true; } }
                else field.Append(c);
            }
            else if (c == ',' || c == '\r' || c == '\n')
            {
                cells.Add(field.ToString()); field.Clear(); closed = false;
                if (c != ',') { if (c == '\r' && reader.Peek() == '\n') reader.Read(); yield return cells.ToArray(); cells.Clear(); started = false; }
            }
            else if (c == '"' && field.Length == 0 && !closed) quoted = true;
            else { if (closed || c == '"') throw new PackageException("InvalidHeaders"); field.Append(c); }
            if (field.Length > 100000 || cells.Count > 300) throw new PackageException("UnsafeArchive");
        }
    }
    private sealed class PackageException(string code) : Exception { public string Code { get; } = code; }
    private sealed class BoundedEntryStream(Stream source, long limit, CancellationToken ct) : Stream
    {
        private long _read;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => _read; set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override int Read(Span<byte> buffer)
        {
            ct.ThrowIfCancellationRequested();
            var count = source.Read(buffer); _read = checked(_read + count);
            if (_read > limit) throw new PackageException("UnsafeArchive");
            return count;
        }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) source.Dispose(); base.Dispose(disposing); }
    }
}
