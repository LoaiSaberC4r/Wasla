using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BuildingBlock.Application.Abstraction.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.Clinical;
using Wasla.Application.Features.Families;
using Wasla.Domain.Common;
using Wasla.Domain.Families;
using Wasla.Domain.Patients;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;
using static Wasla.Tests.Integration.Phase13ApiFixture;

namespace Wasla.Tests.Integration;

internal sealed class Phase16FamilyFixture : IAsyncDisposable
{
    public Phase13ApiFixture App { get; }
    public Guid FamilyId { get; private set; }
    public Guid ChildId { get; } = Guid.NewGuid();
    public Guid SecondChildId { get; } = Guid.NewGuid();
    public HttpClient Father { get; private set; } = null!;
    public HttpClient Mother { get; private set; } = null!;
    public HttpClient Admin { get; private set; } = null!;
    public static string Own => "/api/v1/families/mine/medical-access-requests";
    public static string Inbox => "/api/v1/admin/family-medical-access-requests";
    public static string Eligibility => "/api/v1/admin/family-medical-eligibilities";
    public string Record(Guid? child = null) => $"/api/v1/families/mine/dependents/{child ?? ChildId}/medical-record";
    public string Status(Guid? child = null) => $"/api/v1/families/mine/dependents/{child ?? ChildId}/medical-access";
    private Phase16FamilyFixture(Phase13ApiFixture app) => App = app;
    public static void ConfigurePolicy(FamilyMedicalPolicyOptions p)
    {
        p.Approved = true; p.MasterSourceOfTruthAdopted = true; p.PolicyVersion = "TEST-ONLY-v1"; p.ApprovalReference = "TEST fixture: no legal approval";
        p.Jurisdiction = "TEST"; p.TimeZoneId = "Africa/Cairo"; p.BasisReferences = ["TEST-reviewed-proof"];
        p.AllowedDocumentTypes = Enum.GetValues<FamilyRelationshipDocumentType>();
    }
    public static async Task<Phase16FamilyFixture> CreateAsync(bool sql = false, bool approve = true, FamilyMemberRole adultRole = FamilyMemberRole.Father)
    {
        var app = await Phase15DiagnosticApiTests.CreateAsync(sql); var f = new Phase16FamilyFixture(app);
        ConfigurePolicy(app.Services.GetRequiredService<IOptions<FamilyMedicalPolicyOptions>>().Value);
        app.Services.GetRequiredService<IOptions<MedicalRecordOptions>>().Value.GuardianSharingEnabled = true;
        var ct = TestContext.Current.CancellationToken;
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WaslaDbContext>();
            var admin = await db.ApplicationUsers.SingleAsync(u => u.UserName == "admin", ct);
            var father = await db.PatientAccountLinks.SingleAsync(l => l.PatientId == app.PatientId, ct);
            db.Patients.AddRange(Patient.Create(f.ChildId, "طفل", "Child", app.Today.AddYears(-15).AddDays(-360), Gender.Male, null, null, null, null, null, app.Today).Value,
                Patient.Create(f.SecondChildId, "طفلة", "Second child", app.Today.AddYears(-8), Gender.Female, null, null, null, null, null, app.Today).Value);
            var family = Family.Create(Guid.NewGuid(), admin.Id).Value; f.FamilyId = family.Id;
            var r = FamilyRelationshipRequest.Create(Guid.NewGuid(), FamilyRelationshipRequestType.CreateFamily, null, app.PatientId, f.ChildId,
                adultRole, FamilyMemberRole.Child, father.ApplicationUserId, app.Clock.UtcNow).Value;
            r.AssignCreatedFamily(family.Id); r.Approve(admin.Id, app.Clock.UtcNow); db.Add(r);
            family.AddMember(Guid.NewGuid(), app.PatientId, adultRole, app.Clock.UtcNow, admin.Id, r.Id);
            family.AddMember(Guid.NewGuid(), f.ChildId, FamilyMemberRole.Child, app.Clock.UtcNow, admin.Id, r.Id);
            foreach (var pair in new[] { (Patient: app.OtherPatientId, Role: adultRole == FamilyMemberRole.Mother ? FamilyMemberRole.Father : FamilyMemberRole.Mother), (Patient: f.SecondChildId, Role: FamilyMemberRole.Child) })
            {
                var add = FamilyRelationshipRequest.Create(Guid.NewGuid(), FamilyRelationshipRequestType.AddFamilyMember, family.Id, app.PatientId, pair.Patient,
                    adultRole, pair.Role, father.ApplicationUserId, app.Clock.UtcNow).Value;
                add.Approve(admin.Id, app.Clock.UtcNow); db.Add(add);
                family.AddMember(Guid.NewGuid(), pair.Patient, pair.Role, app.Clock.UtcNow, admin.Id, add.Id);
            }
            db.Add(family); await db.SaveChangesAsync(ct);
            foreach (var proof in await db.FamilyRelationshipRequests.Where(rq => rq.FamilyId == family.Id).ToArrayAsync(ct))
            {
                using var bytes = new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.4\nfixture proof\n%%EOF"));
                var stored = await scope.ServiceProvider.GetRequiredService<IMediaService>().SaveAsync(new MediaUpload(bytes, "proof.pdf", "application/pdf", bytes.Length), new("FamilyFixture"), ct);
                db.Add(new FamilyRelationshipDocument(Guid.NewGuid(), proof.Id, 1, Enum.GetValues<FamilyRelationshipDocumentType>()[0], stored.Key,
                    "proof.pdf", stored.ContentType, stored.Length, admin.Id, app.Clock.UtcNow));
            }
            await db.SaveChangesAsync(ct);
        }
        f.Father = await app.ClientAsync("patient"); f.Mother = await app.ClientAsync("other-patient"); f.Admin = await app.ClientAsync("admin");
        if (approve)
        {
            var reviews = (await GetAsync(f.Admin, Eligibility + "/reviews")).GetProperty("items");
            Assert.Equal(4, reviews.GetArrayLength());
            foreach (var review in reviews.EnumerateArray())
            {
                var approved = await f.DecideEligibilityAsync(review.GetProperty("reviewId").GetGuid(), "approve", review.GetProperty("rowVersion").GetString()!);
                Assert.Equal("Approved", approved.GetProperty("status").GetString());
            }
        }
        return f;
    }
    public async Task<JsonElement> DecideEligibilityAsync(Guid id, string action, string rowVersion)
    {
        using var response = await PostAsync(Admin, Eligibility + $"/{id}/{action}", new { rowVersion, policyVersion = "TEST-ONLY-v1", basisReference = "TEST-reviewed-proof", reason = "Proof correction" });
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)); return await JsonAsync(response);
    }
    public static Task<HttpResponseMessage> SubmitAsync(HttpClient client, Guid child, Guid affected, string type = "Restriction", string? key = null,
        string reason = "Private justification", bool file = false, Guid? resubmit = null, string? rowVersion = null)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(type), "requestType"); form.Add(new StringContent(child.ToString()), "childPatientId"); form.Add(new StringContent(affected.ToString()), "affectedAdultPatientId");
        form.Add(new StringContent(reason), "reason"); if (rowVersion is not null) form.Add(new StringContent(rowVersion), "rowVersion");
        if (file)
        {
            var content = new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.4\nprivate evidence\n%%EOF")); content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            form.Add(content, "evidenceFiles", "proof.pdf"); form.Add(new StringContent(Enum.GetValues<FamilyRelationshipDocumentType>()[0].ToString()), "documentTypes");
        }
        var request = new HttpRequestMessage(HttpMethod.Post, resubmit is { } id ? Own + $"/{id}/resubmit" : Own) { Content = form };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString("N")); return client.SendAsync(request, TestContext.Current.CancellationToken);
    }
    public async Task<JsonElement> DecideAsync(Guid id, string action, string rowVersion)
    {
        using var r = await PostAsync(Admin, Inbox + $"/{id}/{action}", new { rowVersion, reason = "Rejected justification", message = "Please supply evidence" });
        Assert.True(r.StatusCode == HttpStatusCode.OK, await r.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)); return await JsonAsync(r);
    }
    public async ValueTask DisposeAsync() { Father.Dispose(); Mother.Dispose(); Admin.Dispose(); await App.DisposeAsync(); }
}
