using System.Net;
using System.Net.Http.Json;
using static Wasla.Tests.Integration.Phase13ApiFixture;

namespace Wasla.Tests.Integration;

public sealed class Phase16VisibilityApiTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task P16_22_23_and_EXT_01_02_03_support_atomic_hidden_completion_owner_scope_and_history(bool sql)
    {
        await using var app = await Phase15DiagnosticApiTests.CreateAsync(sql);
        using var doctor = await app.ClientAsync("doctor"); using var reception = await app.ClientAsync("reception");
        var ticket = await app.StartAsync(doctor, await app.WalkInAsync(reception));
        var id = ticket.GetProperty("medicalEncounterId").GetGuid(); var url = app.EncounterUrl(id);
        var e = await GetAsync(doctor, url); Assert.Equal("Visible", e.GetProperty("visibility").GetString());
        using (var response = await doctor.PatchAsJsonAsync(url + "/visibility", new { visibility = "Hidden", rowVersion = e.GetProperty("rowVersion").GetString() }, TestContext.Current.CancellationToken))
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using (var notes = await doctor.PatchAsJsonAsync(url + "/clinical-notes", new { clinicalNotes = "Sensitive note", rowVersion = e.GetProperty("rowVersion").GetString() }, TestContext.Current.CancellationToken)) e = await JsonAsync(notes);
        var key = Guid.NewGuid().ToString("N"); var body = new { visibility = "Hidden", ticketRowVersion = ticket.GetProperty("rowVersion").GetString(), encounterRowVersion = e.GetProperty("rowVersion").GetString() };
        await Phase15DiagnosticApiTests.OkAsync(await PostAsync(doctor, app.TicketUrl(ticket.GetProperty("ticketId").GetGuid()) + "/complete", body, key));
        await Phase15DiagnosticApiTests.OkAsync(await PostAsync(doctor, app.TicketUrl(ticket.GetProperty("ticketId").GetGuid()) + "/complete", body, key));
        e = await GetAsync(doctor, url); Assert.Equal("Hidden", e.GetProperty("visibility").GetString()); Assert.True(e.GetProperty("canChangeVisibility").GetBoolean());
        var stale = e.GetProperty("rowVersion").GetString();
        var changed = await Phase15DiagnosticApiTests.OkAsync(await doctor.PatchAsJsonAsync(url + "/visibility", new { visibility = "Visible", rowVersion = stale }, TestContext.Current.CancellationToken));
        Assert.Equal(2, changed.GetProperty("visibilityRevision").GetInt32());
        using (var conflict = await doctor.PatchAsJsonAsync(url + "/visibility", new { visibility = "Hidden", rowVersion = stale }, TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        using var foreign = await app.ClientAsync("other-doctor");
        using (var denied = await foreign.GetAsync(url + "/visibility/history", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        var history = await GetAsync(doctor, url + "/visibility/history?pageSize=1"); Assert.True(history.GetProperty("hasMore").GetBoolean());
        var next = await GetAsync(doctor, url + "/visibility/history?cursor=" + Uri.EscapeDataString(history.GetProperty("nextCursor").GetString()!));
        Assert.Equal("Completion", next.GetProperty("items")[0].GetProperty("source").GetString());
        var list = await GetAsync(doctor, $"/api/v1/doctors/me/practices/{app.PracticeId}/encounters");
        Assert.Equal("Visible", list.GetProperty("items")[0].GetProperty("visibility").GetString());
    }
}
