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
        p.RestrictionGroundsReferences = ["TEST-ONLY-restriction-mandate"];
    }
    public static async Task<Phase16FamilyFixture> CreateAsync(bool sql = false, bool approve = true, FamilyMemberRole adultRole = FamilyMemberRole.Father,
        FamilyMemberRole? otherAdultRole = null, bool verifyParents = true)
    {
        var app = await Phase15DiagnosticApiTests.CreateAsync(sql); var f = new Phase16FamilyFixture(app);
        try
        {
            ConfigurePolicy(app.Services.GetRequiredService<IOptions<FamilyMedicalPolicyOptions>>().Value);
            app.Services.GetRequiredService<IOptions<MedicalRecordOptions>>().Value.GuardianSharingEnabled = true;
            var ct = TestContext.Current.CancellationToken;
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<WaslaDbContext>();
                await using var setupTransaction = await db.Database.BeginTransactionAsync(ct);
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
                foreach (var pair in new[] { (Patient: app.OtherPatientId, Role: otherAdultRole ?? (adultRole == FamilyMemberRole.Mother ? FamilyMemberRole.Father : FamilyMemberRole.Mother)), (Patient: f.SecondChildId, Role: FamilyMemberRole.Child) })
                {
                    var add = FamilyRelationshipRequest.Create(Guid.NewGuid(), FamilyRelationshipRequestType.AddFamilyMember, family.Id, app.PatientId, pair.Patient,
                        adultRole, pair.Role, father.ApplicationUserId, app.Clock.UtcNow).Value;
                    add.Approve(admin.Id, app.Clock.UtcNow); db.Add(add);
                    family.AddMember(Guid.NewGuid(), pair.Patient, pair.Role, app.Clock.UtcNow, admin.Id, add.Id);
                }
                db.Add(family); await db.SaveChangesAsync(ct);
                // The second adult's membership request proves an adult-adult relationship only.
                // These separate approved cases explicitly establish each mother-child pair for this fixture.
                var other = family.Members.Single(m => m.PatientId == app.OtherPatientId);
                foreach (var child in new[] { f.ChildId, f.SecondChildId })
                {
                    var proof = FamilyRelationshipRequest.Create(Guid.NewGuid(), other.Role is FamilyMemberRole.Father or FamilyMemberRole.Mother ?
                        FamilyRelationshipRequestType.VerifyParentChild : FamilyRelationshipRequestType.VerifyGuardianChild, family.Id,
                        app.OtherPatientId, child, other.Role, FamilyMemberRole.Child, father.ApplicationUserId, app.Clock.UtcNow).Value;
                    proof.Approve(admin.Id, app.Clock.UtcNow); db.Add(proof);
                }
                await db.SaveChangesAsync(ct);
                foreach (var proof in await db.FamilyRelationshipRequests.Where(rq => rq.FamilyId == family.Id).ToArrayAsync(ct))
                {
                    using var bytes = new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.4\nfixture proof\n%%EOF"));
                    var stored = await scope.ServiceProvider.GetRequiredService<IMediaService>().SaveAsync(new MediaUpload(bytes, "proof.pdf", "application/pdf", bytes.Length), new("FamilyFixture"), ct);
                    db.Add(new FamilyRelationshipDocument(Guid.NewGuid(), proof.Id, 1, Enum.GetValues<FamilyRelationshipDocumentType>()[0], stored.Key,
                        "proof.pdf", stored.ContentType, stored.Length, admin.Id, app.Clock.UtcNow));
                }
                await db.SaveChangesAsync(ct);
                if (verifyParents)
                    foreach (var proof in await db.FamilyRelationshipRequests.Where(rq => rq.FamilyId == family.Id).ToArrayAsync(ct))
                        Assert.True((await scope.ServiceProvider.GetRequiredService<IFamilyRelationshipMedicalVerification>().RecordAsync(proof, family, ct)).IsSuccess);
                await db.SaveChangesAsync(ct);
                await setupTransaction.CommitAsync(ct);
            }
            f.Father = await app.ClientAsync("patient"); f.Mother = await app.ClientAsync("other-patient"); f.Admin = await app.ClientAsync("admin");
            if (approve)
            {
                var reviews = (await GetAsync(f.Admin, Eligibility + "/reviews")).GetProperty("items");
                Assert.Equal((adultRole is FamilyMemberRole.Guardian or FamilyMemberRole.LegalGuardian ? 2 : 0) +
                    (otherAdultRole is FamilyMemberRole.Guardian or FamilyMemberRole.LegalGuardian ? 2 : 0), reviews.GetArrayLength());
                foreach (var review in reviews.EnumerateArray())
                {
                    var approved = await f.DecideEligibilityAsync(review.GetProperty("reviewId").GetGuid(), "approve", review.GetProperty("rowVersion").GetString()!);
                    Assert.Equal("Approved", approved.GetProperty("status").GetString());
                }
            }
            return f;
        }
        catch { await f.DisposeAsync(); throw; }
    }
    public async Task<JsonElement> DecideEligibilityAsync(Guid id, string action, string rowVersion)
    {
        var authority = await AuthorityAsync(id);
        using var response = await PostAsync(Admin, Eligibility + $"/{id}/{action}", new { rowVersion, policyVersion = "TEST-ONLY-v1", basisReference = "TEST-reviewed-proof", authorityRelationshipRequestId = authority, reason = "Proof correction" });
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)); return await JsonAsync(response);
    }
    public async Task<Guid> AuthorityAsync(Guid reviewId)
    {
        Guid id = default;
        await App.WithDbAsync(async db => {
            var r = await db.Set<FamilyMedicalEligibilityReview>().SingleAsync(r => r.Id == reviewId);
            id = await db.FamilyRelationshipRequests.Where(p => p.FamilyId == r.FamilyId && p.RequesterPatientId == r.AdultPatientId &&
                p.TargetPatientId == r.ChildPatientId && p.Status == FamilyRelationshipRequestStatus.Approved).Select(p => p.Id).SingleAsync();
        });
        return id;
    }
    public static Task<HttpResponseMessage> SubmitAsync(HttpClient client, Guid child, Guid affected, string type = "Restriction", string? key = null,
        string reason = "Private justification", bool file = true, Guid? resubmit = null, string? rowVersion = null)
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
        using var r = await PostAsync(Admin, Inbox + $"/{id}/{action}", new { rowVersion, basisReference = "TEST-ONLY-restriction-mandate", reason = "Rejected justification", message = "Please supply evidence" });
        Assert.True(r.StatusCode == HttpStatusCode.OK, await r.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)); return await JsonAsync(r);
    }
    public async ValueTask DisposeAsync() { Father?.Dispose(); Mother?.Dispose(); Admin?.Dispose(); await App.DisposeAsync(); }
}
