using System.Text.Json;
using Wasla.Domain.Medications;
using Wasla.Domain.Resources;
using static Wasla.Tests.Integration.Phase13ApiFixture;

namespace Wasla.Tests.Integration;

public sealed class Phase14ContractTests
{
    [Fact]
    public async Task OpenApi_publishes_phase14_routes_multipart_import_and_completion_concurrency_contract()
    {
        await using var app = await Phase14MedicationApiTests.CreateAsync(false); using var client = app.CreateClient();
        var document = await GetAsync(client, "/swagger/v1/swagger.json"); var paths = document.GetProperty("paths");
        foreach (var route in new[] { "/api/v1/admin/drug-catalog-managers", "/api/v1/doctors/me/drug-catalog",
            "/api/v1/admin/drug-catalog/imports/preview", "/api/v1/admin/drug-catalog/imports/{batchId}/apply",
            "/api/v1/doctors/me/practices/{practiceId}/encounters/{encounterId}/prescription/items",
            "/api/v1/doctors/me/prescriptions/{prescriptionId}/correction-draft/finalize", "/api/v1/prescriptions/mine/{prescriptionId}" }) Assert.True(paths.TryGetProperty(route, out _), route);
        Assert.True(paths.GetProperty("/api/v1/admin/drug-catalog/imports/preview").GetProperty("post").GetProperty("requestBody").GetProperty("content").TryGetProperty("multipart/form-data", out _));
        var schemas = document.GetProperty("components").GetProperty("schemas");
        Assert.True(schemas.GetProperty("CompleteVisitRequest").GetProperty("properties").TryGetProperty("prescriptionRowVersion", out _));
        Assert.True(schemas.GetProperty("PrescriptionItemRequest").GetProperty("properties").TryGetProperty("newMedication", out _));
        Assert.False(paths.GetProperty("/api/v1/admin/drug-catalog/{drugId}").TryGetProperty("delete", out _));
    }
    [Fact]
    public void Every_medication_error_has_english_and_arabic_resource_text()
    {
        var resource = new System.Resources.ResourceManager("Wasla.Domain.Resources.ErrorMessage", typeof(ErrorMessage).Assembly);
        var english = resource.GetResourceSet(System.Globalization.CultureInfo.InvariantCulture, true, true)!;
        var codes = english.Cast<System.Collections.DictionaryEntry>().Select(e => (string)e.Key).Where(k => k.StartsWith("Prescription", StringComparison.Ordinal) || k.StartsWith("DrugCatalog", StringComparison.Ordinal) || k.StartsWith("Medication.", StringComparison.Ordinal)).ToArray();
        Assert.True(codes.Length >= 50);
        foreach (var code in codes)
        {
            Assert.NotEqual(code, MedicationErrors.Validation(code).Message);
            var en = ErrorMessage.GetString(code, System.Globalization.CultureInfo.InvariantCulture);
            var ar = ErrorMessage.GetString(code, System.Globalization.CultureInfo.GetCultureInfo("ar"));
            Assert.False(string.IsNullOrWhiteSpace(en)); Assert.False(string.IsNullOrWhiteSpace(ar)); Assert.NotEqual(en, ar);
        }
    }
}
