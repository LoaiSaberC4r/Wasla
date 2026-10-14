using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wasla.Domain.Operations;
using Wasla.Domain.Security;
using static Wasla.Tests.Integration.Phase13ApiFixture;

namespace Wasla.Tests.Integration;

public sealed class Phase18ReceptionApiTests
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Handover_scope_history_transitions_and_revoked_delegation_are_enforced(bool sql)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(sql);
        using var reception = await app.ClientAsync("reception"); using var doctor = await app.ClientAsync("doctor");
        var url = $"/api/v1/reception/practices/{app.PracticeId}/handovers";
        using var created = await PostAsync(reception, url, new { summary = "Check queue operation" }, "create");
        Assert.True(created.StatusCode == HttpStatusCode.OK, await created.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var row = await JsonAsync(created); var id = row.GetProperty("id").GetGuid();
        using var self = await PostAsync(reception, url + $"/{id}/acknowledge", new { rowVersion = Token(row) }, "self");
        Assert.Equal(HttpStatusCode.Conflict, self.StatusCode);
        using var invalidLink = await PostAsync(reception, url, new { summary = "Review", reservationId = Guid.NewGuid() }, "link");
        Assert.Equal(HttpStatusCode.NotFound, invalidLink.StatusCode);
        var doctorUrl = $"/api/v1/doctors/me/practices/{app.PracticeId}/handovers/{id}";
        using var closed = await PostAsync(doctor, doctorUrl + "/close", new { rowVersion = Token(row), outcome = "Operation reviewed" }, "close");
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        using var annotation = await PostAsync(reception, url + $"/{id}/annotations", new { rowVersion = Token(await JsonAsync(closed)), annotation = "Review" }, "annotate");
        Assert.Equal(HttpStatusCode.Conflict, annotation.StatusCode);
        var history = await GetAsync(reception, url + $"/{id}/history");
        Assert.Equal(2, history.GetProperty("totalCount").GetInt64());
        using var financial = await reception.GetAsync("/api/v1/reception/reports/operating-finance/drilldown?metricCode=AllOutstandingAsOf", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, financial.StatusCode);
        await app.WithDbAsync(async db =>
        {
            await db.ReceptionPracticeAssignmentPermissions.Where(p => p.AssignmentId == app.ReceptionAssignmentId &&
                p.PermissionId == SystemPermissionIds.For(Phase18PermissionNames.HandoverView)).ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        });
        using var revoked = await reception.GetAsync(url + $"/{id}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, revoked.StatusCode);
        var attention = await GetAsync(reception, "/api/v1/reception/attention");
        var unavailableHandovers = attention.GetProperty("metrics").EnumerateArray().Single(m => m.GetProperty("metricCode").GetString() == "OpenHandovers");
        Assert.Equal(JsonValueKind.Null, unavailableHandovers.GetProperty("value").ValueKind);
        Assert.Equal("NotAvailable", unavailableHandovers.GetProperty("dataStatus").GetString());
        await app.WithDbAsync(async db =>
        {
            var entry = await db.Set<ReceptionHandoverHistory>().FirstAsync(TestContext.Current.CancellationToken);
            db.Remove(entry); await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
        });
    }
    private static string Token(JsonElement row) => row.GetProperty("rowVersion").GetString()!;
}
