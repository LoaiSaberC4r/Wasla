using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.Clinical;
using Wasla.Application.Features.Families;
using Wasla.Domain.Families;
using Wasla.Domain.Security;
using static Wasla.Tests.Integration.Phase13ApiFixture;
using static Wasla.Tests.Integration.Phase16FamilyFixture;

namespace Wasla.Tests.Integration;

public sealed class Phase16FamilyMedicalApiTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Restriction_modification_evidence_withdraw_restore_and_dependent_reads_complete_real_journeys(bool sql)
    {
        await using var f = await Phase16FamilyFixture.CreateAsync(sql); var app = f.App;
        using var doctor = await app.ClientAsync("doctor"); using var reception = await app.ClientAsync("reception");
        var ticket = await app.StartAsync(doctor, await app.WalkInAsync(reception, patientId: f.ChildId));
        var encounterId = ticket.GetProperty("medicalEncounterId").GetGuid();
        var lab = await Phase15DiagnosticApiTests.AddAsync(app, doctor, encounterId, "lab", "Child test");
        await Phase15DiagnosticApiTests.CompleteAsync(app, doctor, ticket, lab);
        var orderId = lab.GetProperty("requestId").GetGuid(); lab = await GetAsync(doctor, $"/api/v1/doctors/me/lab-requests/{orderId}");
        var coverage = lab.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("itemId").GetGuid()).ToArray();
        using var uploaded = await Phase15DiagnosticApiTests.UploadAsync(doctor, $"/api/v1/doctors/me/lab-requests/{orderId}/results", "lab", coverage, lab.GetProperty("rowVersion").GetString());
        Assert.True(uploaded.IsSuccessStatusCode, await uploaded.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var result = (await JsonAsync(uploaded)).GetProperty("result"); var resultId = result.GetProperty("resultId").GetGuid();
        var attachment = result.GetProperty("current").GetProperty("attachments")[0].GetProperty("attachmentId").GetGuid();
        var media = f.Record() + $"/items/lab-result/{resultId}/attachments/{attachment}/content";
        var urls = new[] { f.Record() + "/overview", f.Record() + "/timeline", f.Record() + $"/encounters/{encounterId}", f.Record() + $"/items/lab-request/{orderId}", f.Record() + $"/items/lab-result/{resultId}", media };
        foreach (var url in urls)
        {
            using var read = await f.Mother.GetAsync(url, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.OK, read.StatusCode);
            if (url == media) { Assert.True(read.Headers.CacheControl?.NoStore); Assert.Equal("nosniff", read.Headers.GetValues("X-Content-Type-Options").Single()); }
            else { var json = await read.Content.ReadAsStringAsync(TestContext.Current.CancellationToken); Assert.DoesNotContain("clinicalNotes", json); Assert.DoesNotContain("mediaKey", json); Assert.DoesNotContain("beforeSnapshot", json); }
        }
        var switcher = await GetAsync(f.Mother, "/api/v1/families/mine/medical-record-dependents"); Assert.Equal(2, switcher.GetProperty("items").GetArrayLength());
        var cursor = (await GetAsync(f.Mother, f.Record() + "/timeline?pageSize=1")).GetProperty("nextCursor").GetString(); Assert.NotNull(cursor);
        const string key = "fixture-exact-replay";
        using var submitted = await SubmitAsync(f.Father, f.ChildId, app.OtherPatientId, key: key, file: true);
        Assert.True(submitted.StatusCode == HttpStatusCode.Created, await submitted.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var request = await JsonAsync(submitted); var id = request.GetProperty("requestId").GetGuid(); var rowVersion = request.GetProperty("rowVersion").GetString()!;
        using (var replay = await SubmitAsync(f.Father, f.ChildId, app.OtherPatientId, key: key, file: true))
        { Assert.Equal(HttpStatusCode.Created, replay.StatusCode); Assert.Equal(id, (await JsonAsync(replay)).GetProperty("requestId").GetGuid()); }
        using (var conflict = await SubmitAsync(f.Father, f.ChildId, app.OtherPatientId, key: key, reason: "different")) Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        using (var competing = await SubmitAsync(f.Father, f.ChildId, app.OtherPatientId)) Assert.Equal(HttpStatusCode.Conflict, competing.StatusCode);
        Assert.Equal("Allowed", (await GetAsync(f.Mother, f.Status())).GetProperty("status").GetString());
        var document = request.GetProperty("documents")[0].GetProperty("documentId").GetGuid();
        foreach (var pair in new[] { (Client: f.Father, Url: Own), (Client: f.Admin, Url: Inbox) })
        { using var evidence = await pair.Client.GetAsync(pair.Url + $"/{id}/documents/{document}/content", TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.OK, evidence.StatusCode); Assert.True(evidence.Headers.CacheControl?.NoStore); }
        using (var denied = await f.Mother.GetAsync(Own + $"/{id}", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        using (var denied = await f.Mother.GetAsync(Own + $"/{id}/documents/{document}/content", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        var review = await GetAsync(f.Admin, Inbox + $"/{id}"); Assert.True(review.TryGetProperty("relationshipVerification", out _)); Assert.DoesNotContain("diagnoses", review.GetRawText());
        await GetAsync(f.Admin, Inbox + "/summary"); await GetAsync(f.Admin, Inbox + "?type=Restriction&status=Pending"); await GetAsync(f.Father, Own + "/mine");
        request = await f.DecideAsync(id, "request-modification", rowVersion); Assert.Equal("ModificationRequested", request.GetProperty("status").GetString());
        using (var duplicate = await SubmitAsync(f.Father, f.ChildId, app.OtherPatientId)) Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        using var resubmit = await SubmitAsync(f.Father, f.ChildId, app.OtherPatientId, file: true, resubmit: id, rowVersion: request.GetProperty("rowVersion").GetString());
        Assert.True(resubmit.IsSuccessStatusCode, await resubmit.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)); request = await JsonAsync(resubmit);
        Assert.Equal(2, request.GetProperty("revision").GetInt32()); Assert.Equal(2, request.GetProperty("documents").GetArrayLength());
        using (var stale = await PostAsync(f.Admin, Inbox + $"/{id}/approve", new { rowVersion })) Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        request = await f.DecideAsync(id, "approve", request.GetProperty("rowVersion").GetString()!);
        var status = await GetAsync(f.Mother, f.Status()); Assert.Equal("Restricted", status.GetProperty("status").GetString()); Assert.True(status.GetProperty("canRequestRestoration").GetBoolean());
        Assert.DoesNotContain("Private justification", status.GetRawText()); Assert.DoesNotContain(id.ToString(), status.GetRawText());
        var roster = await GetAsync(f.Mother, "/api/v1/families/mine"); Assert.Contains(roster.GetProperty("members").EnumerateArray(), m => m.GetProperty("patientId").GetGuid() == f.ChildId);
        foreach (var url in urls) { using var denied = await f.Mother.GetAsync(url, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode); }
        await GetAsync(f.Father, f.Record() + "/overview"); await GetAsync(f.Mother, f.Record(f.SecondChildId) + "/overview");
        using (var wrongRestore = await SubmitAsync(f.Father, f.ChildId, app.OtherPatientId, "Restoration")) Assert.Equal(HttpStatusCode.UnprocessableEntity, wrongRestore.StatusCode);
        using var restore = await SubmitAsync(f.Mother, f.ChildId, app.OtherPatientId, "Restoration"); Assert.Equal(HttpStatusCode.Created, restore.StatusCode);
        var restoredRequest = await JsonAsync(restore); var restoreId = restoredRequest.GetProperty("requestId").GetGuid();
        using (var denied = await f.Mother.GetAsync(media, TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        await f.DecideAsync(restoreId, "approve", restoredRequest.GetProperty("rowVersion").GetString()!);
        await GetAsync(f.Mother, f.Record() + "/overview");
        using (var oldCursor = await f.Mother.GetAsync(f.Record() + "/timeline?types=all&pageSize=1&cursor=" + Uri.EscapeDataString(cursor!), TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.UnprocessableEntity, oldCursor.StatusCode);
        await GetAsync(f.Father, Own + $"/{id}/history"); await GetAsync(f.Admin, Inbox + $"/{id}/history");
        using var next = await SubmitAsync(f.Father, f.ChildId, app.OtherPatientId); Assert.Equal(HttpStatusCode.Created, next.StatusCode);
        var nextRequest = await JsonAsync(next); var nextId = nextRequest.GetProperty("requestId").GetGuid();
        using var withdrawal = await PostAsync(f.Father, Own + $"/{nextId}/withdraw", new { rowVersion = nextRequest.GetProperty("rowVersion").GetString() }); Assert.Equal(HttpStatusCode.OK, withdrawal.StatusCode);
        await app.WithDbAsync(async db => { Assert.Equal(1, await db.Set<FamilyMedicalAccessRestriction>().CountAsync()); Assert.Equal(FamilyMedicalRestrictionStatus.Lifted, (await db.Set<FamilyMedicalAccessRestriction>().SingleAsync()).Status); Assert.Equal(4, await db.FamilyMembers.CountAsync()); });
    }
    [Fact]
    public async Task Legal_gate_eligibility_revocation_age_boundary_and_staff_scopes_fail_closed()
    {
        await using var f = await Phase16FamilyFixture.CreateAsync(approve: false);
        Assert.Equal("RelationshipNotEligible", (await GetAsync(f.Father, f.Status())).GetProperty("status").GetString());
        Assert.Equal(0, (await GetAsync(f.Father, "/api/v1/families/mine/medical-record-dependents")).GetProperty("items").GetArrayLength());
        using (var denied = await f.Father.GetAsync(f.Record() + "/overview", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        var review = (await GetAsync(f.Admin, Eligibility + "/reviews")).GetProperty("items").EnumerateArray().Single(r => r.GetProperty("adultPatientId").GetGuid() == f.App.PatientId && r.GetProperty("childPatientId").GetGuid() == f.ChildId);
        var id = review.GetProperty("reviewId").GetGuid(); var detail = await GetAsync(f.Admin, Eligibility + $"/{id}"); Assert.True(detail.TryGetProperty("adultVerification", out _));
        var options = f.App.Services.GetRequiredService<IOptions<FamilyMedicalPolicyOptions>>().Value; options.MasterSourceOfTruthAdopted = false;
        using (var denied = await PostAsync(f.Admin, Eligibility + $"/{id}/approve", new { rowVersion = detail.GetProperty("rowVersion").GetString(), policyVersion = "TEST-ONLY-v1", basisReference = "TEST-reviewed-proof" })) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        options.MasterSourceOfTruthAdopted = true;
        var approved = await f.DecideEligibilityAsync(id, "approve", detail.GetProperty("rowVersion").GetString()!); await GetAsync(f.Father, f.Record() + "/overview");
        foreach (var user in new[] { "admin", "doctor", "reception" })
        { using var staff = await f.App.ClientAsync(user); using var denied = await staff.GetAsync(f.Record() + "/overview", TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode); }
        using (var unrelated = await f.Father.GetAsync($"/api/v1/families/mine/dependents/{Guid.NewGuid()}/medical-access", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.NotFound, unrelated.StatusCode);
        f.App.Services.GetRequiredService<IOptions<MedicalRecordOptions>>().Value.GuardianSharingEnabled = false;
        Assert.Equal("Unavailable", (await GetAsync(f.Father, f.Status())).GetProperty("status").GetString());
        using (var denied = await f.Father.GetAsync(f.Record() + "/overview", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        f.App.Services.GetRequiredService<IOptions<MedicalRecordOptions>>().Value.GuardianSharingEnabled = true;
        DateOnly birthday = default; await f.App.WithDbAsync(async db => birthday = (await db.Patients.SingleAsync(p => p.Id == f.ChildId)).DateOfBirth.AddYears(16));
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"); var boundary = TimeZoneInfo.ConvertTimeToUtc(birthday.ToDateTime(TimeOnly.MinValue), zone);
        f.App.Clock.UtcNow = boundary.AddSeconds(-1); await GetAsync(f.Father, f.Record() + "/overview");
        f.App.Clock.UtcNow = boundary; Assert.Equal("AgeNotEligible", (await GetAsync(f.Father, f.Status())).GetProperty("status").GetString());
        using (var denied = await f.Father.GetAsync(f.Record() + "/timeline", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        f.App.Clock.UtcNow = boundary.AddDays(-1);
        await f.DecideEligibilityAsync(id, "revoke", approved.GetProperty("rowVersion").GetString()!);
        using (var denied = await f.Father.GetAsync(f.Record() + "/overview", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        await GetAsync(f.Father, "/api/v1/patients/me/medical-record/overview");
    }
}
