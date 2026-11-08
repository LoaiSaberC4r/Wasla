using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.Clinical;
using Wasla.Domain.Diagnostics;
using static Wasla.Tests.Integration.Phase13ApiFixture;
using static Wasla.Tests.Integration.Phase15DiagnosticApiTests;
namespace Wasla.Tests.Integration;
public sealed class DependentDiagnosticApiTests
{
    [Theory]
    [InlineData("lab", false)]
    [InlineData("radiology", false)]
    [InlineData("lab", true)]
    [InlineData("radiology", true)]
    public async Task Proxy_submits_only_to_exact_child_request_and_source_doctor_keeps_review(string kind, bool sql)
    {
        await using var family = await Phase16FamilyFixture.CreateAsync(sql);
        var f = family.App; using var doctor = await f.ClientAsync("doctor"); using var reception = await f.ClientAsync("reception");
        var ticket = await f.StartAsync(doctor, await f.WalkInAsync(reception, patientId: family.SecondChildId));
        var order = await AddAsync(f, doctor, ticket.GetProperty("medicalEncounterId").GetGuid(), kind, "Dependent investigation");
        var requestId = order.GetProperty("requestId").GetGuid();
        await CompleteAsync(f, doctor, ticket, kind == "lab" ? order : null, kind == "radiology" ? order : null);
        var path = $"/api/v1/families/mine/dependents/{family.SecondChildId}/{kind}-requests/{requestId}/submissions";
        var key = Guid.NewGuid().ToString("N");
        var receipt = await OkAsync(await UploadAsync(family.Father, path, kind, key: key));
        Assert.Equal("PendingReview", receipt.GetProperty("status").GetString());
        Assert.False(receipt.TryGetProperty("request", out _)); Assert.False(receipt.TryGetProperty("result", out _));
        var submission = receipt.GetProperty("submissionId").GetGuid();
        var replay = await OkAsync(await UploadAsync(family.Father, path, kind, key: key)); Assert.Equal(submission, replay.GetProperty("submissionId").GetGuid());
        using var mismatch = await UploadAsync(family.Father, path.Replace(family.SecondChildId.ToString(), family.ChildId.ToString(), StringComparison.Ordinal), kind);
        Assert.Equal(HttpStatusCode.NotFound, mismatch.StatusCode);
        using var own = await family.Father.GetAsync($"/api/v1/{kind}-requests/mine/{requestId}", TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.NotFound, own.StatusCode);
        using var otherParent = await PostAsync(family.Mother, path + $"/{submission}/withdraw", new { rowVersion = receipt.GetProperty("rowVersion").GetString() }); Assert.Equal(HttpStatusCode.NotFound, otherParent.StatusCode);
        await f.WithDbAsync(async db => {
            var actor = await db.PatientAccountLinks.SingleAsync(l => l.PatientId == f.PatientId);
            if (kind == "lab") { var s = await db.PatientLabResultSubmissions.SingleAsync(s => s.Id == submission); Assert.Equal(family.SecondChildId, s.PatientId); Assert.Equal(actor.ApplicationUserId, s.UploadedByUserId); Assert.Empty(await db.LabResults.ToArrayAsync()); }
            else { var s = await db.PatientRadiologyResultSubmissions.SingleAsync(s => s.Id == submission); Assert.Equal(family.SecondChildId, s.PatientId); Assert.Equal(actor.ApplicationUserId, s.UploadedByUserId); Assert.Empty(await db.RadiologyResults.ToArrayAsync()); }
        });
        var ids = order.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("itemId").GetGuid()).ToArray();
        object accept = kind == "lab" ? new { coveredLabRequestItemIds = ids, rowVersion = receipt.GetProperty("rowVersion").GetString() } : new { coveredRadiologyRequestItemIds = ids, rowVersion = receipt.GetProperty("rowVersion").GetString() };
        var accepted = await OkAsync(await PostAsync(doctor, Url(kind, $"-result-submissions/{submission}/accept"), accept));
        Assert.Equal("Completed", accepted.GetProperty("request").GetProperty("status").GetString());
        using var closed = await PostAsync(family.Father, path + $"/{submission}/withdraw", new { rowVersion = receipt.GetProperty("rowVersion").GetString() }); Assert.Equal(HttpStatusCode.Conflict, closed.StatusCode);
        f.Services.GetRequiredService<IOptions<MedicalRecordOptions>>().Value.GuardianSharingEnabled = false;
        using var disabled = await family.Father.GetAsync(path, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.Forbidden, disabled.StatusCode);
    }
}
