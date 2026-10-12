using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.Clinical;
using Wasla.Domain.Clinical;
using Wasla.Domain.Practices;
using Wasla.Domain.Security;
using Wasla.Domain.Tickets;
using static Wasla.Tests.Integration.Phase13ApiFixture;
using static Wasla.Tests.Integration.Phase15DiagnosticApiTests;

using static Wasla.Tests.Integration.Phase16Fixture;

namespace Wasla.Tests.Integration;

public sealed class Phase16PatientApiTests
{
    private const string PatientRecord = "/api/v1/patients/me/medical-record";
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task P16_01_02_03_06_patient_hidden_record_has_no_internal_notes_and_mixed_cursor_is_protected(bool sql)
    {
        await using var app = await Phase15DiagnosticApiTests.CreateAsync(sql); using var owner = await app.ClientAsync("doctor"); using var reception = await app.ClientAsync("reception"); using var patient = await app.ClientAsync("patient");
        var ticket = await app.StartAsync(owner, await app.WalkInAsync(reception)); var id = ticket.GetProperty("medicalEncounterId").GetGuid();
        var lab = await AddAsync(app, owner, id, "lab", "Investigation");
        var radio = await AddAsync(app, owner, id, "radiology", "Imaging");
        await CompleteAsync(app, owner, ticket, lab, radio); await HideAsync(app, owner, id);
        var overview = await GetAsync(patient, PatientRecord + "/overview?pageSize=1");
        Assert.Equal(1, overview.GetProperty("summary").GetProperty("completedVisitCount").GetInt32());
        Assert.DoesNotContain("clinicalNotes", overview.GetRawText(), StringComparison.OrdinalIgnoreCase);
        var page = overview.GetProperty("timeline"); var ids = new List<string>();
        while (true)
        {
            ids.AddRange(page.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("entryId").GetString()!));
            if (!page.GetProperty("hasMore").GetBoolean()) break;
            page = await GetAsync(patient, PatientRecord + "/timeline?pageSize=1&cursor=" + Uri.EscapeDataString(page.GetProperty("nextCursor").GetString()!));
        }
        Assert.Equal(3, ids.Count); Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
        var details = await GetAsync(patient, PatientRecord + $"/encounters/{id}"); Assert.DoesNotContain("clinicalNotes", details.GetRawText(), StringComparison.OrdinalIgnoreCase);
        var order = await GetAsync(patient, PatientRecord + "/items/lab-request/" + lab.GetProperty("requestId").GetGuid());
        Assert.Equal("MissingResult", order.GetProperty("safeContent").GetProperty("resultStatus").GetString());
        Assert.DoesNotContain("postVisitReason", order.GetRawText(), StringComparison.OrdinalIgnoreCase);
        var log = await GetAsync(patient, PatientRecord + "/access-log"); Assert.NotEmpty(log.GetProperty("items").EnumerateArray());
        Assert.DoesNotContain("reason", log.GetRawText(), StringComparison.OrdinalIgnoreCase);
        using (var bad = await patient.GetAsync(PatientRecord + "/timeline?cursor=forged", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.UnprocessableEntity, bad.StatusCode);
        using var foreignPatient = await app.ClientAsync("other-patient");
        using (var denied = await foreignPatient.GetAsync(PatientRecord + $"/encounters/{id}", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
    }
}
