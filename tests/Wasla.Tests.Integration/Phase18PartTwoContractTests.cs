using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Wasla.Tests.Integration;

public sealed class Phase18PartTwoContractTests
{
    [Fact]
    public async Task All_38_contracts_have_typed_openapi_permissions_headers_and_no_destructive_routes()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(); using var client = app.CreateClient();
        using var response = await client.GetAsync("/swagger/v1/swagger.json", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!;
        var manifestPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../docs/phase18/part2-endpoint-manifest.json"));
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath, TestContext.Current.CancellationToken));
        Assert.Equal(38, manifest.RootElement.GetArrayLength());
        var selected = new JsonObject();
        foreach (var row in manifest.RootElement.EnumerateArray())
        {
            var route = row.GetProperty("route").GetString()!; var method = row.GetProperty("method").GetString()!.ToLowerInvariant();
            var operation = json["paths"]![route]![method]!;
            Assert.NotNull(operation["responses"]!["200"]!["content"]);
            Assert.NotNull(operation["responses"]!["429"]!["headers"]!["Retry-After"]);
            Assert.NotNull(operation["responses"]!["200"]!["headers"]!["Cache-Control"]);
            if (method == "post" && route.EndsWith("/pause", StringComparison.Ordinal) || route.EndsWith("/resume", StringComparison.Ordinal))
                Assert.Contains(operation["parameters"]!.AsArray(), p => p!["name"]!.GetValue<string>() == "Idempotency-Key" && p["required"]!.GetValue<bool>());
            selected[route] = json["paths"]![route]!.DeepClone();
        }
        foreach (var route in new[] { "/api/v1/reservations/mine/archived/patients/options", "/api/v1/reception/archives/practices/options" })
        {
            Assert.NotNull(json["paths"]![route]!["get"]!["responses"]!["200"]!["content"]);
            selected[route] = json["paths"]![route]!.DeepClone();
        }
        Assert.DoesNotContain(json["paths"]!.AsObject(), p => p.Key.Contains("archives", StringComparison.Ordinal) &&
            (p.Key.Contains("purge", StringComparison.OrdinalIgnoreCase) || p.Key.Contains("unarchive", StringComparison.OrdinalIgnoreCase) || p.Key.Contains("run-now", StringComparison.OrdinalIgnoreCase)));
        var schemas = json["components"]!["schemas"]!;
        Assert.Contains(schemas["ArchiveControlBody"]!["required"]!.AsArray(), x => x!.GetValue<string>() == "reason");
        Assert.Contains(schemas["TicketDetailsResponse"]!["required"]!.AsArray(), x => x!.GetValue<string>() == "isArchived");
        Assert.Contains(schemas["TicketOperationalCapabilities"]!["required"]!.AsArray(), x => x!.GetValue<string>() == "canCheckIn");
        Assert.Contains(schemas["ReservationCapabilitiesResponse"]!["required"]!.AsArray(), x => x!.GetValue<string>() == "canCancel");
        Assert.NotNull(schemas["TicketDetailsResponse"]!["properties"]!["operationalCapabilities"]!["anyOf"]);
        Assert.NotNull(json["paths"]!["/api/v1/admin/reservations/{reservationId}"]!["get"]!["responses"]!["200"]!["content"]!["application/json"]!["schema"]!["oneOf"]);
        var output = Environment.GetEnvironmentVariable("WASLA_CONTRACT_OUTPUT_DIRECTORY");
        if (output != null)
        {
            Directory.CreateDirectory(output);
            var used = new HashSet<string>(); var pending = new Queue<JsonNode>(); pending.Enqueue(selected);
            while (pending.TryDequeue(out var node))
            {
                if (node is JsonObject obj)
                    foreach (var entry in obj)
                    {
                        if (entry.Key == "$ref" && entry.Value?.GetValue<string>() is { } reference && reference.StartsWith("#/components/schemas/", StringComparison.Ordinal))
                        {
                            var name = reference[21..]; if (used.Add(name)) pending.Enqueue(schemas[name]!);
                        }
                        else if (entry.Value != null) pending.Enqueue(entry.Value);
                    }
                else if (node is JsonArray array) foreach (var child in array) if (child != null) pending.Enqueue(child);
            }
            var subset = new JsonObject(); foreach (var name in used.Order()) subset[name] = schemas[name]!.DeepClone();
            var components = new JsonObject { ["schemas"] = subset };
            if (json["components"]!["securitySchemes"] is { } securitySchemes) components["securitySchemes"] = securitySchemes.DeepClone();
            var snapshot = new JsonObject { ["openapi"] = json["openapi"]!.DeepClone(), ["info"] = json["info"]!.DeepClone(), ["paths"] = selected, ["components"] = components };
            if (json["security"] is { } security) snapshot["security"] = security.DeepClone();
            await File.WriteAllTextAsync(Path.Combine(output, "part2-openapi.json"), snapshot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), TestContext.Current.CancellationToken);
        }
    }
}
