using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BuildingBlock.Domain.Results;
using Wasla.Domain.Medications;

namespace Wasla.Application.Features.Medications;

public sealed record ParsedDrugImportRow(int RowNumber, DrugData Data, string IdentityFingerprint, string ContentHash);
public static class DrugImportParser
{
    public const int MaximumFileBytes = 20 * 1024 * 1024;
    public const int MaximumRecords = 100_000;
    public static Result<IReadOnlyList<ParsedDrugImportRow>> Parse(byte[] file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file.Length is 0 or > MaximumFileBytes) return Result<IReadOnlyList<ParsedDrugImportRow>>.Fail(MedicationErrors.Validation("DrugCatalogImport.InvalidFile"));
        try
        {
            using var document = JsonDocument.Parse(file);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() > MaximumRecords)
                return Result<IReadOnlyList<ParsedDrugImportRow>>.Fail(MedicationErrors.Validation("DrugCatalogImport.InvalidSchema"));
            if (root.GetArrayLength() == 0) return Result<IReadOnlyList<ParsedDrugImportRow>>.Fail(MedicationErrors.Validation("DrugCatalogImport.Empty"));
            var rows = new List<ParsedDrugImportRow>(root.GetArrayLength()); var number = 0;
            foreach (var row in root.EnumerateArray())
            {
                number++;
                if (row.ValueKind != JsonValueKind.Object) throw new JsonException();
                string? Field(string field)
                {
                    if (!row.TryGetProperty(field, out var value)) throw new JsonException();
                    return value.ValueKind switch { JsonValueKind.Null => null, JsonValueKind.String => value.GetString(), _ => throw new JsonException() };
                }
                if (!row.TryGetProperty("price_egp", out var price)) throw new JsonException();
                var data = new DrugData(Field("commercial_name_en") ?? string.Empty, Field("commercial_name_ar"), Field("scientific_name"),
                    Field("manufacturer"), Field("drug_class"), Field("route"), PriceEgp: price.ValueKind == JsonValueKind.Null ? null : price.GetDecimal());
                if (MedicationText.Validate(data).IsFailure) return Result<IReadOnlyList<ParsedDrugImportRow>>.Fail(MedicationErrors.Validation("DrugCatalogImport.InvalidSchema", $"file.rows/{number}"));
                rows.Add(new(number, data, Hash(JsonSerializer.Serialize(data with { PriceEgp = null })), Hash(JsonSerializer.Serialize(data))));
            }
            return Result<IReadOnlyList<ParsedDrugImportRow>>.Ok(rows);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException or OverflowException)
        {
            return Result<IReadOnlyList<ParsedDrugImportRow>>.Fail(MedicationErrors.Validation("DrugCatalogImport.InvalidSchema"));
        }
    }
    public static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
