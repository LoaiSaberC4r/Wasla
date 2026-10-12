using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wasla.Application.Features.Clinical;
using static Wasla.Tests.Integration.Phase13ApiFixture;

namespace Wasla.Tests.Integration;

public sealed class Phase16ContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = MakeOptions();
    private static JsonSerializerOptions MakeOptions()
    { var options = new JsonSerializerOptions(JsonSerializerDefaults.Web); options.Converters.Add(new JsonStringEnumConverter()); return options; }
    private static readonly string[] HiddenFields = ["accessRequest", "capabilities", "entryId", "hiddenEncounterToken", "isHidden", "type", "visitDate"];
    [Fact]
    public async Task All_P16_01_through_29_routes_and_EXT_01_through_03_resolve_with_documented_methods()
    {
        await using var app = await Phase15DiagnosticApiTests.CreateAsync(); using var client = app.CreateClient();
        var api = await GetAsync(client, "/swagger/v1/swagger.json"); var paths = api.GetProperty("paths");
        var self = "/api/v1/patients/me/medical-record";
        var treating = "/api/v1/doctors/me/practices/{practiceId}/tickets/{ticketId}/medical-record";
        var inbox = "/api/v1/doctors/me/medical-record-access-requests";
        var visibility = "/api/v1/doctors/me/practices/{practiceId}/encounters/{encounterId}/visibility";
        var family = "/api/v1/families/mine/dependents/{dependentPatientId}/medical-record";
        var routes = new List<(string Path, string Method)>();
        foreach (var b in new[] { self, treating, family })
            foreach (var suffix in new[] { "/overview", "/timeline", "/encounters/{encounterId}", "/items/{resourceType}/{sourceId}", "/items/{resourceType}/{sourceId}/attachments/{attachmentId}/content" }) routes.Add((b + suffix, "get"));
        routes.Add((self + "/access-log", "get")); routes.Add((treating + "/access-requests", "get")); routes.Add((treating + "/access-requests/{requestId}", "get")); routes.Add((treating + "/access-requests", "post"));
        foreach (var suffix in new[] { "/inbox/summary", "/inbox", "/{requestId}", "/{requestId}/history" }) routes.Add((inbox + suffix, "get"));
        foreach (var suffix in new[] { "approve", "reject", "revoke" }) routes.Add((inbox + "/{requestId}/" + suffix, "post"));
        routes.Add((visibility, "patch")); routes.Add((visibility + "/history", "get")); routes.Add(("/api/v1/families/mine/medical-record-dependents", "get"));
        Assert.Equal(29, routes.Count);
        foreach (var r in routes)
        {
            Assert.True(paths.TryGetProperty(r.Path, out var p), r.Path); Assert.True(p.TryGetProperty(r.Method, out var method), r.Method + " " + r.Path);
            Assert.True(method.GetProperty("responses").TryGetProperty("401", out _)); Assert.True(method.GetProperty("responses").TryGetProperty("404", out _));
        }
        Assert.True(paths.GetProperty("/api/v1/practices/{practiceId}/tickets/{ticketId}/complete").TryGetProperty("post", out _));
        Assert.True(paths.GetProperty("/api/v1/doctors/me/practices/{practiceId}/encounters/{encounterId}").TryGetProperty("get", out _));
        Assert.True(paths.GetProperty("/api/v1/doctors/me/practices/{practiceId}/encounters").TryGetProperty("get", out _));
        var schemas = api.GetProperty("components").GetProperty("schemas");
        Assert.True(schemas.GetProperty("CompleteVisitRequest").GetProperty("properties").TryGetProperty("visibility", out _));
        Assert.True(schemas.GetProperty("EncounterDetailsResponse").GetProperty("properties").TryGetProperty("canChangeVisibility", out _));
        Assert.True(schemas.GetProperty("EncounterSummaryResponse").GetProperty("properties").TryGetProperty("visibility", out _));
        Assert.Equal(3, schemas.GetProperty("MedicalRecordItemDetailsResponse").GetProperty("properties").GetProperty("safeContent").GetProperty("oneOf").GetArrayLength());
        Assert.Equal(2, schemas.GetProperty("TimelineEntry").GetProperty("oneOf").GetArrayLength());
        using var unauthenticated = await client.GetAsync(self + "/overview", TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
    }
    [Fact]
    public void Hidden_discriminated_payload_has_exact_safe_fields_when_serialized_through_the_timeline_base_type()
    {
        TimelineEntry entry = new HiddenTimelineEntry("opaque", new DateOnly(2026, 10, 1), true, "protected", new("NotRequested", null), new(true));
        var json = JsonSerializer.Serialize(new RecordPage<TimelineEntry>([entry], null, false), JsonOptions);
        using var document = JsonDocument.Parse(json); var row = document.RootElement.GetProperty("items")[0];
        Assert.Equal(HiddenFields, row.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.DoesNotContain(typeof(SafeDiagnosis).GetProperties(), p => p.Name == "Notes");
        Assert.DoesNotContain(typeof(MedicalRecordEncounterDetailsResponse).GetProperties(), p => p.Name is "ClinicalNotes" or "Amendments");
        Assert.DoesNotContain(typeof(RecordAttachment).GetProperties(), p => p.Name is "PrivateMediaKey" or "StorageKey" or "PublicUrl");
    }
}
