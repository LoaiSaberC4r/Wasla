using static Wasla.Tests.Integration.Phase13ApiFixture;
namespace Wasla.Tests.Integration;
public sealed class Phase5FinalContractTests
{
    [Fact]
    public async Task Swagger_publishes_exact_twenty_routes_and_purpose_and_header_contracts()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(); using var client = app.CreateClient();
        var swagger = await GetAsync(client, "/swagger/v1/swagger.json"); var paths = swagger.GetProperty("paths");
        var newOperations = 0;
        foreach (var path in paths.EnumerateObject())
            if (path.Name.StartsWith("/api/v1/patient-onboarding", StringComparison.Ordinal) || path.Name.StartsWith("/api/v1/admin/patient-claims", StringComparison.Ordinal) ||
                path.Name.StartsWith("/api/v1/admin/patient-duplicates", StringComparison.Ordinal) || path.Name is "/api/v1/patients/full" or "/api/v1/patients/registration-matches")
                newOperations += path.Value.EnumerateObject().Count(p => p.Name is "get" or "post");
        Assert.Equal(20, newOperations);
        Assert.True(paths.TryGetProperty("/api/v1/practices/{practiceId}/tickets/walk-in", out _));
        Assert.False(paths.TryGetProperty("/api/v1/reception/practices/{practiceId}/tickets/walk-in", out _));
        var full = paths.GetProperty("/api/v1/patients/full").GetProperty("post");
        Assert.Contains(full.GetProperty("parameters").EnumerateArray(), p => p.GetProperty("name").GetString() == "Idempotency-Key" && p.GetProperty("required").GetBoolean());
        Assert.True(full.GetProperty("responses").GetProperty("201").GetProperty("content").TryGetProperty("application/json", out _));
        var schemas = swagger.GetProperty("components").GetProperty("schemas");
        Assert.True(schemas.GetProperty("PatientEmailVerifiedResponse").GetProperty("properties").TryGetProperty("nextAction", out _));
        var fields = full.GetProperty("requestBody").GetProperty("content").GetProperty("multipart/form-data").GetProperty("schema").GetProperty("properties");
        Assert.Contains(fields.EnumerateObject(), f => f.Name.Equals("email", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(fields.EnumerateObject(), f => f.Name.Equals("password", StringComparison.OrdinalIgnoreCase));
    }
}
