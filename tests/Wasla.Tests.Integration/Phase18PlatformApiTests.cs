using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wasla.Domain.Operations;
using static Wasla.Tests.Integration.Phase13ApiFixture;

namespace Wasla.Tests.Integration;

public sealed class Phase18PlatformApiTests
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Administrative_cases_have_explicit_concurrent_transitions_and_platform_health_uses_actual_data(bool sql)
    {
        await using var app = await Phase15DiagnosticApiTests.CreateAsync(sql); using var root = await app.ClientAsync("root");
        var options = await GetAsync(root, "/api/v1/admin/investigation-cases/filter-options");
        var admin = options.GetProperty("activeSuperAdmins")[0].GetProperty("id").GetGuid();
        using var created = await PostAsync(root, "/api/v1/admin/investigation-cases", new
        { titleAr = "مراجعة تشغيل", description = "Review operation", issueType = "AdministrativeSupport" }, "create");
        Assert.True(created.StatusCode == HttpStatusCode.OK, await created.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var row = await JsonAsync(created); var id = row.GetProperty("id").GetGuid(); var url = "/api/v1/admin/investigation-cases/" + id;
        using var unassigned = await PostAsync(root, url + "/start-review", new { rowVersion = Token(row) }, "unassigned");
        Assert.Equal(HttpStatusCode.Conflict, unassigned.StatusCode);
        using var assign = await PostAsync(root, url + "/assign", new { rowVersion = Token(row), assignedSuperAdminId = admin }, "assign");
        Assert.Equal(HttpStatusCode.OK, assign.StatusCode); var assigned = await JsonAsync(assign);
        using var stale = await PostAsync(root, url + "/start-review", new { rowVersion = Token(row) }, "stale");
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var start = await PostAsync(root, url + "/start-review", new { rowVersion = Token(assigned) }, "start");
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        using var resolve = await PostAsync(root, url + "/resolve", new { rowVersion = Token(await JsonAsync(start)), resolutionType = "Verified", notes = "Operation verified" }, "resolve");
        Assert.Equal(HttpStatusCode.OK, resolve.StatusCode);
        using var close = await PostAsync(root, url + "/close", new { rowVersion = Token(await JsonAsync(resolve)), notes = "Review completed" }, "close");
        Assert.Equal(HttpStatusCode.OK, close.StatusCode);
        using var reopen = await PostAsync(root, url + "/reopen", new { rowVersion = Token(await JsonAsync(close)), notes = "New administrative evidence" }, "reopen");
        Assert.Equal(HttpStatusCode.OK, reopen.StatusCode);
        var history = await GetAsync(root, url + "/history"); Assert.Equal(6, history.GetProperty("totalCount").GetInt64());
        var health = await GetAsync(root, "/api/v1/admin/operations/jobs/health");
        Assert.Equal("NotMeasured", health.GetProperty("generationHealth").GetString());
        Assert.Empty(health.GetProperty("recentRuns").EnumerateArray());
        var overview = await GetAsync(root, "/api/v1/admin/dashboard/overview");
        Assert.DoesNotContain(overview.GetProperty("metrics").EnumerateArray(), m => m.GetProperty("metricCode").GetString() == "PaidOperatingExpenses");
        var serialized = overview.GetRawText(); Assert.DoesNotContain("nationalId", serialized, StringComparison.OrdinalIgnoreCase);
        var security = await GetAsync(root, "/api/v1/admin/reports/security-metadata");
        Assert.Equal("NotInstrumented", security.GetProperty("metrics").EnumerateArray().Single(m => m.GetProperty("metricCode").GetString() == "SecurityLoginEvents").GetProperty("dataStatus").GetString());
        using var doctor = await app.ClientAsync("doctor"); using var denied = await doctor.GetAsync(url, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        await app.WithDbAsync(async db =>
        {
            var h = await db.Set<InvestigationHistory>().FirstAsync(TestContext.Current.CancellationToken); db.Remove(h);
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
        });
    }
    private static string Token(JsonElement row) => row.GetProperty("rowVersion").GetString()!;
}
