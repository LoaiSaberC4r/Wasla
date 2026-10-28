using System.Net;
using Microsoft.EntityFrameworkCore;
using Wasla.Domain.Security;
using static Wasla.Tests.Integration.Phase13ApiFixture;
using static Wasla.Tests.Integration.Phase15DiagnosticApiTests;

namespace Wasla.Tests.Integration;

public sealed class DoctorPatientDiagnosticHistoryTests
{
    [Theory]
    [InlineData("lab", false)]
    [InlineData("radiology", false)]
    [InlineData("lab", true)]
    [InlineData("radiology", true)]
    public async Task Own_historical_results_versions_pending_submissions_and_media_are_read_only(string kind, bool sql)
    {
        await using var f = await Phase15DiagnosticApiTests.CreateAsync(sql); using var doctor = await f.ClientAsync("doctor");
        using var reception = await f.ClientAsync("reception"); using var patient = await f.ClientAsync("patient");
        var ticket = await f.StartAsync(doctor, await f.WalkInAsync(reception)); var encounter = ticket.GetProperty("medicalEncounterId").GetGuid();
        var order = await AddAsync(f, doctor, encounter, kind, "Synthetic A");
        order = await AddAsync(f, doctor, encounter, kind, "Synthetic B", order.GetProperty("rowVersion").GetString());
        var requestId = order.GetProperty("requestId").GetGuid(); var item = order.GetProperty("items")[0].GetProperty("itemId").GetGuid();
        await CompleteAsync(f, doctor, ticket, kind == "lab" ? order : null, kind == "radiology" ? order : null);
        order = await GetAsync(doctor, Url(kind, $"-requests/{requestId}"));
        var uploadToken = order.GetProperty("rowVersion").GetString();
        var uploadKey = Guid.NewGuid().ToString("N"); var uploadUrl = Url(kind, $"-requests/{requestId}/results");
        var uploaded = await OkAsync(await UploadAsync(doctor, uploadUrl, kind, [item], token: uploadToken, key: uploadKey));
        var result = uploaded.GetProperty("result"); var resultId = result.GetProperty("resultId").GetGuid();
        var submission = (await OkAsync(await UploadAsync(patient, $"/api/v1/{kind}-requests/mine/{requestId}/submissions", kind))).GetProperty("submission");
        var submissionId = submission.GetProperty("submissionId").GetGuid();
        await f.WithDbAsync(async db => { (await db.DoctorPractices.SingleAsync(p => p.Id == f.PracticeId)).Deactivate(f.DoctorUserId); await db.SaveChangesAsync(); });
        foreach (var resource in new[] { "-requests", "-results", "-result-submissions" })
        {
            var list = await GetAsync(doctor, Url(kind, resource) + $"?patientId={f.PatientId}"); Assert.Equal(1, list.GetProperty("totalCount").GetInt32());
        }
        var readOrder = await GetAsync(doctor, Url(kind, $"-requests/{requestId}"));
        var readEncounter = await GetAsync(doctor, f.EncounterUrl(encounter));
        Assert.Equal(1, readEncounter.GetProperty(kind == "lab" ? "labRequestsSummary" : "radiologyRequestsSummary").GetArrayLength());
        Assert.All(readOrder.GetProperty("capabilities").EnumerateObject(), p => Assert.False(p.Value.GetBoolean()));
        await GetAsync(doctor, Url(kind, $"-requests/{requestId}/history"));
        var readResult = await GetAsync(doctor, Url(kind, $"-results/{resultId}"));
        Assert.All(readResult.GetProperty("capabilities").EnumerateObject(), p => Assert.False(p.Value.GetBoolean()));
        await GetAsync(doctor, Url(kind, $"-results/{resultId}/versions"));
        var version = await GetAsync(doctor, Url(kind, $"-results/{resultId}/versions/1"));
        var attachmentId = version.GetProperty("attachments")[0].GetProperty("attachmentId").GetGuid();
        var mediaUrl = Url(kind, $"-results/{resultId}/versions/1/attachments/{attachmentId}/content");
        using (var media = await doctor.GetAsync(mediaUrl, TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.OK, media.StatusCode);
        var readSubmission = await GetAsync(doctor, Url(kind, $"-result-submissions/{submissionId}"));
        Assert.Equal("PendingReview", readSubmission.GetProperty("status").GetString());
        Assert.All(readSubmission.GetProperty("capabilities").EnumerateObject(), p => Assert.False(p.Value.GetBoolean()));
        var submissionAttachment = readSubmission.GetProperty("attachments")[0].GetProperty("attachmentId").GetGuid();
        using (var media = await doctor.GetAsync(Url(kind, $"-result-submissions/{submissionId}/attachments/{submissionAttachment}/content"), TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.OK, media.StatusCode);
        foreach (var (url, body) in new (string, object)[] {
            (Url(kind, $"-requests/{requestId}/cancel-remaining"), new { reason = "Forbidden", rowVersion = readOrder.GetProperty("rowVersion").GetString() }),
            (Url(kind, $"-result-submissions/{submissionId}/reject"), new { patientVisibleReason = "Forbidden", rowVersion = readSubmission.GetProperty("rowVersion").GetString() }),
            (Url(kind, $"-results/{resultId}/void"), new { reason = "Forbidden", rowVersion = readResult.GetProperty("rowVersion").GetString() }) })
        {
            using var denied = await PostAsync(doctor, url, body); Assert.Contains(denied.StatusCode, new[] { HttpStatusCode.Forbidden, HttpStatusCode.NotFound });
        }
        using (var replay = await UploadAsync(doctor, uploadUrl, kind, [item], token: uploadToken, key: uploadKey)) Assert.Equal(HttpStatusCode.Forbidden, replay.StatusCode);
        using (var denied = await UploadAsync(doctor, uploadUrl, kind, [item])) Assert.Contains(denied.StatusCode, new[] { HttpStatusCode.Forbidden, HttpStatusCode.NotFound });
        await DoctorPatientsApiTests.RemoveGrants(f, PermissionNames.DoctorPatientsViewHistoricalOwn);
        using (var denied = await doctor.GetAsync(mediaUrl, TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using (var denied = await doctor.GetAsync(Url(kind, $"-requests/{requestId}"), TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }
}
