using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Wasla.Domain.Clinical;
using Wasla.Domain.Practices;
using Wasla.Domain.Tickets;
using Wasla.Application.Features.Clinical;
using static Wasla.Tests.Integration.Phase13ApiFixture;
using static Wasla.Tests.Integration.Phase15DiagnosticApiTests;
using static Wasla.Tests.Integration.Phase16Fixture;

namespace Wasla.Tests.Integration;

public sealed class Phase16HiddenAccessApiTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task P16_12_through_21_work_without_selected_owner_practice_and_return_current_scoped_status(bool sql)
    {
        await using var app = await Phase15DiagnosticApiTests.CreateAsync(sql); EnableSharing(app);
        using var owner = await app.ClientAsync("doctor"); using var requester = await app.ClientAsync("other-doctor"); using var reception = await app.ClientAsync("reception");
        var source = await app.StartAsync(owner, await app.WalkInAsync(reception)); var id = source.GetProperty("medicalEncounterId").GetGuid(); await app.CompleteAsync(owner, source); await HideAsync(app, owner, id);
        var treating = await TreatingAsync(app); var url = TreatingUrl(app, treating.Ticket);
        var token = app.Services.GetRequiredService<IMedicalRecordTokens>().Protect("HiddenEncounter", new HiddenEncounterToken(treating.User, treating.Doctor, treating.Ticket, id, 1, app.Clock.UtcNow));
        var created = await OkAsync(await PostAsync(requester, url + "/access-requests", new { hiddenEncounterToken = token, reason = " Reason " }));
        var rid = created.GetProperty("requestId").GetGuid(); Assert.Equal("Reason", created.GetProperty("reason").GetString());
        Assert.False(created.TryGetProperty("ownerDetails", out _)); Assert.Single((await GetAsync(requester, url + "/access-requests")).GetProperty("items").EnumerateArray());
        var inbox = "/api/v1/doctors/me/medical-record-access-requests";
        Assert.Equal(1, (await GetAsync(owner, inbox + "/inbox/summary")).GetProperty("actionableCount").GetInt32());
        var detail = await GetAsync(owner, inbox + $"/{rid}"); Assert.True(detail.TryGetProperty("ownerDetails", out _));
        var approved = await OkAsync(await PostAsync(owner, inbox + $"/{rid}/approve", new { rowVersion = detail.GetProperty("rowVersion").GetString() }));
        Assert.Equal(id, (await GetAsync(requester, url + $"/access-requests/{rid}")).GetProperty("grantedEncounterId").GetGuid());
        await OkAsync(await PostAsync(owner, inbox + $"/{rid}/revoke", new { rowVersion = approved.GetProperty("rowVersion").GetString() }));
        Assert.Equal("Revoked", (await GetAsync(requester, url + $"/access-requests/{rid}")).GetProperty("effectiveStatus").GetString());
        Assert.Equal(3, (await GetAsync(owner, inbox + $"/{rid}/history")).GetArrayLength());
        using var duplicate = await PostAsync(requester, url + "/access-requests", new { hiddenEncounterToken = token, reason = "Reason" }); Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }
    [Fact]
    public async Task Owner_global_inbox_aggregates_two_owned_practices_and_filters_without_switching_context()
    {
        await using var app = await Phase15DiagnosticApiTests.CreateAsync(); EnableSharing(app);
        using var owner = await app.ClientAsync("doctor"); using var requester = await app.ClientAsync("other-doctor"); using var reception = await app.ClientAsync("reception");
        var first = await app.StartAsync(owner, await app.WalkInAsync(reception)); var firstId = first.GetProperty("medicalEncounterId").GetGuid(); await app.CompleteAsync(owner, first); await HideAsync(app, owner, firstId);
        var secondId = Guid.Empty; var secondPractice = Guid.NewGuid();
        await app.WithDbAsync(async db =>
        {
            var p = DoctorPractice.Create(secondPractice, app.DoctorId, "Second", "Second", 1, 1, 1, "Address", 30, 31, app.DoctorUserId).Value;
            p.Activate(true, true, true, app.DoctorUserId); db.DoctorPractices.Add(p);
            var segment = DoctorPracticeSegment.CreateDefault(Guid.NewGuid(), p.Id, app.DoctorUserId).Value;
            var type = DoctorPracticeVisitType.CreateDefault(Guid.NewGuid(), p.Id, DoctorPracticeVisitTypeCode.NewConsultation, app.DoctorUserId).Value;
            db.DoctorPracticeSegments.Add(segment); db.DoctorPracticeVisitTypes.Add(type);
            var t = Ticket.CreateWalkIn(new TicketCreationSnapshot(Guid.NewGuid(), app.DoctorId, p.Id, app.PatientId, null, app.Today, 1,
                TicketSource.WalkIn, segment.Id, "Normal", "Normal", 0, type.Id, "NewConsultation", "Visit", "Visit", 300, "Africa/Cairo",
                app.Clock.UtcNow, app.Clock.UtcNow, CheckInMode.WalkIn, app.DoctorUserId, null)).Value;
            t.Call(app.DoctorUserId, app.Clock.UtcNow); t.StartVisit(app.DoctorUserId, app.Clock.UtcNow); var e = MedicalEncounter.Start(t, app.DoctorUserId, app.Clock.UtcNow).Value;
            e.UpdateClinicalNotes("Private second-practice note", app.DoctorUserId, app.Clock.UtcNow); e.Complete(t, app.DoctorUserId, app.Clock.UtcNow, EncounterVisibility.Hidden); t.Complete(app.DoctorUserId, app.Clock.UtcNow);
            db.Tickets.Add(t); db.MedicalEncounters.Add(e); await db.SaveChangesAsync(TestContext.Current.CancellationToken); secondId = e.Id;
        });
        var treating = await TreatingAsync(app); var url = TreatingUrl(app, treating.Ticket); var tokens = app.Services.GetRequiredService<IMedicalRecordTokens>();
        foreach (var id in new[] { firstId, secondId })
            await OkAsync(await PostAsync(requester, url + "/access-requests", new { hiddenEncounterToken = tokens.Protect("HiddenEncounter", new HiddenEncounterToken(treating.User, treating.Doctor, treating.Ticket, id, 1, app.Clock.UtcNow)), reason = "Reason" }));
        var inbox = "/api/v1/doctors/me/medical-record-access-requests/inbox";
        Assert.Equal(2, (await GetAsync(owner, inbox + "/summary")).GetProperty("actionableCount").GetInt32());
        Assert.Equal(2, (await GetAsync(owner, inbox)).GetProperty("items").GetArrayLength());
        Assert.Single((await GetAsync(owner, inbox + $"?practiceId={secondPractice}")).GetProperty("items").EnumerateArray());
    }
}
