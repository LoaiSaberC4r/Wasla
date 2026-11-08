using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.Families;
using Wasla.Domain.Families;
using Wasla.Domain.Patients;
using static Wasla.Tests.Integration.Phase13ApiFixture;
namespace Wasla.Tests.Integration;

public sealed class FamilyGovernanceApiTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Identity_review_preserves_global_identity_and_retains_requested_revisions(bool sql)
    {
        await using var f = await CreateAsync(sql); Enable(f);
        using var parent = await f.ClientAsync("patient"); using var admin = await f.ClientAsync("root"); using var other = await f.ClientAsync("other-patient");
        var child = await Child(f, parent, admin, "هوية طفل أصلية"); var birth = f.Today.AddYears(-8);
        var beforeCount = 0; await f.WithDbAsync(async db => beforeCount = await db.Patients.CountAsync());
        var key = Guid.NewGuid().ToString("N");
        using var submitted = await Correction(parent, child, birth, "هوية مقترحة", key);
        Assert.True(submitted.StatusCode == HttpStatusCode.Accepted, await submitted.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var row = await JsonAsync(submitted); var id = row.GetProperty("caseId").GetGuid();
        using var replay = await Correction(parent, child, birth, "هوية مقترحة", key); Assert.Equal(id, (await JsonAsync(replay)).GetProperty("caseId").GetGuid());
        await f.WithDbAsync(async db => Assert.Equal("هوية طفل أصلية", (await db.Patients.SingleAsync(p => p.Id == child)).NameAr));
        using var denied = await other.GetAsync($"/api/v1/families/mine/governance-cases/{id}", TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        var own = await GetAsync(parent, $"/api/v1/families/mine/governance-cases/{id}"); var safe = Assert.Single(own.GetProperty("items").EnumerateArray());
        Assert.Empty(safe.GetProperty("revisions").EnumerateArray()); Assert.Empty(safe.GetProperty("evidence").EnumerateArray()); Assert.Empty(safe.GetProperty("history").EnumerateArray());
        using var modified = await Decision(admin, id, "request-modification", row.GetProperty("rowVersion").GetString()!);
        Assert.True(modified.IsSuccessStatusCode, await modified.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        using var revised = await Correction(parent, child, birth, "الهوية المصححة", Guid.NewGuid().ToString("N"), id, (await JsonAsync(modified)).GetProperty("rowVersion").GetString());
        Assert.True(revised.IsSuccessStatusCode, await revised.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)); var revision = await JsonAsync(revised); Assert.Equal(2, revision.GetProperty("revision").GetInt32());
        var approveKey = Guid.NewGuid().ToString("N"); var version = revision.GetProperty("rowVersion").GetString()!;
        using var approved = await Decision(admin, id, "approve", version, approveKey); Assert.True(approved.IsSuccessStatusCode, await approved.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("Approved", (await JsonAsync(approved)).GetProperty("status").GetString());
        using var decisionReplay = await Decision(admin, id, "approve", version, approveKey); Assert.Equal(HttpStatusCode.OK, decisionReplay.StatusCode);
        using var stale = await Decision(admin, id, "approve", version); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        await f.WithDbAsync(async db => {
            Assert.Equal(beforeCount, await db.Patients.CountAsync()); var patient = await db.Patients.SingleAsync(p => p.Id == child); Assert.Equal("الهوية المصححة", patient.NameAr);
            Assert.False(await db.PatientAccountLinks.AnyAsync(l => l.PatientId == child)); Assert.Single(await db.PatientContacts.Where(c => c.PatientId == child).ToArrayAsync());
            var versions = await db.Set<FamilyGovernanceRevision>().Where(r => r.CaseId == id).OrderBy(r => r.Revision).ToArrayAsync(); Assert.Equal(2, versions.Length);
            Assert.Contains("هوية طفل أصلية", JsonSerializer.Deserialize<DependentIdentityCorrectionInput>(versions[0].BeforeSnapshot)!.NameAr, StringComparison.Ordinal);
            Assert.Equal("هوية مقترحة", JsonSerializer.Deserialize<DependentIdentityCorrectionInput>(versions[0].AfterSnapshot)!.NameAr);
        });
        var details = await GetAsync(admin, $"/api/v1/admin/family-governance-cases/{id}"); Assert.Equal(2, Assert.Single(details.GetProperty("items").EnumerateArray()).GetProperty("revisions").GetArrayLength());
    }
    [Fact]
    public async Task Identity_correction_cannot_bypass_medical_birthday_or_current_proof()
    {
        await using var f = await CreateAsync(); Enable(f); using var parent = await f.ClientAsync("patient"); using var admin = await f.ClientAsync("root");
        var child = await Child(f, parent, admin, "طفل حد العمر");
        using var submitted = await Correction(parent, child, f.Today.AddYears(-17), "تصحيح حساس", Guid.NewGuid().ToString("N"));
        Assert.True(submitted.IsSuccessStatusCode, await submitted.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)); var row = await JsonAsync(submitted); var id = row.GetProperty("caseId").GetGuid();
        using var approval = await Decision(admin, id, "approve", row.GetProperty("rowVersion").GetString()!);
        Assert.True(approval.IsSuccessStatusCode, await approval.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)); Assert.Equal("PendingGovernanceDecision", (await JsonAsync(approval)).GetProperty("status").GetString());
        await f.WithDbAsync(async db => Assert.Equal(f.Today.AddYears(-8), (await db.Patients.SingleAsync(p => p.Id == child)).DateOfBirth));
        using var closed = await Decision(admin, id, "reject", (await JsonAsync(approval)).GetProperty("rowVersion").GetString()!); Assert.True(closed.IsSuccessStatusCode);
        using var fresh = await Correction(parent, child, f.Today.AddYears(-9), "تصحيح آخر", Guid.NewGuid().ToString("N")); Assert.True(fresh.IsSuccessStatusCode);
        var pending = await JsonAsync(fresh);
        await RevokeProof(f, child);
        using var revoked = await Decision(admin, pending.GetProperty("caseId").GetGuid(), "approve", pending.GetProperty("rowVersion").GetString()!); Assert.Equal(HttpStatusCode.Conflict, revoked.StatusCode);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Invitation_is_one_use_private_and_requires_review_before_exact_second_parent_authority(bool sql)
    {
        await using var f = await CreateAsync(sql); Enable(f); using var father = await f.ClientAsync("patient"); using var mother = await f.ClientAsync("other-patient"); using var admin = await f.ClientAsync("root");
        var child = await Child(f, father, admin, "طفل دعوة");
        var key = Guid.NewGuid().ToString("N"); using var issued = await PostAsync(father, "/api/v1/families/mine/join-invitations", new { childPatientId = child, intendedParentRole = "Mother" }, key);
        Assert.True(issued.IsSuccessStatusCode, await issued.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)); var invitation = await JsonAsync(issued); var token = invitation.GetProperty("invitationReference").GetString()!;
        Assert.Equal(64, token.Length); Assert.DoesNotContain(child.ToString(), invitation.GetRawText(), StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("familyId", invitation.GetRawText(), StringComparison.OrdinalIgnoreCase);
        using var issueReplay = await PostAsync(father, "/api/v1/families/mine/join-invitations", new { childPatientId = child, intendedParentRole = "Mother" }, key); Assert.Equal(token, (await JsonAsync(issueReplay)).GetProperty("invitationReference").GetString());
        using var self = await Redeem(father, token, Guid.NewGuid().ToString("N")); Assert.Equal(HttpStatusCode.NotFound, self.StatusCode);
        var redeemKey = Guid.NewGuid().ToString("N"); using var redeemed = await Redeem(mother, token, redeemKey); Assert.True(redeemed.StatusCode == HttpStatusCode.Accepted, await redeemed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)); var request = await JsonAsync(redeemed); var id = request.GetProperty("requestId").GetGuid();
        using var replay = await Redeem(mother, token, redeemKey); Assert.Equal(id, (await JsonAsync(replay)).GetProperty("requestId").GetGuid());
        using var reused = await Redeem(mother, token, Guid.NewGuid().ToString("N")); Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);
        using var before = await mother.GetAsync($"/api/v1/families/mine/dependents/{child}/profile", TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.NotFound, before.StatusCode);
        var details = await GetAsync(admin, $"/api/v1/admin/family-relationship-requests/{id}");
        using var approved = await PostAsync(admin, $"/api/v1/admin/family-relationship-requests/{id}/approve", new { rowVersion = details.GetProperty("request").GetProperty("rowVersion").GetString() }); Assert.True(approved.IsSuccessStatusCode, await approved.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        using var after = await mother.GetAsync($"/api/v1/families/mine/dependents/{child}/profile", TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        await f.WithDbAsync(async db => { Assert.Single(await db.Families.ToArrayAsync()); Assert.Equal(3, await db.FamilyMembers.CountAsync()); Assert.Equal(2, await db.Set<ParentChildOperationalAuthority>().CountAsync(p => p.ChildPatientId == child && p.IsActive)); Assert.DoesNotContain(token, (await db.Set<FamilyJoinInvitation>().SingleAsync()).TokenHash, StringComparison.Ordinal); });
    }
    [Fact]
    public async Task Invitation_stops_when_issuing_proof_is_revoked()
    {
        await using var f = await CreateAsync(); Enable(f); using var father = await f.ClientAsync("patient"); using var mother = await f.ClientAsync("other-patient"); using var admin = await f.ClientAsync("root");
        var child = await Child(f, father, admin, "طفل سحب الدعوة"); using var issued = await PostAsync(father, "/api/v1/families/mine/join-invitations", new { childPatientId = child, intendedParentRole = "Mother" }, Guid.NewGuid().ToString("N")); var token = (await JsonAsync(issued)).GetProperty("invitationReference").GetString()!;
        var revokeKey = Guid.NewGuid().ToString("N");
        using var revoked = await PostAsync(father, "/api/v1/families/mine/join-invitations/revoke", new { invitationReference = token }, revokeKey); Assert.True(revoked.IsSuccessStatusCode);
        using var revokeReplay = await PostAsync(father, "/api/v1/families/mine/join-invitations/revoke", new { invitationReference = token }, revokeKey); Assert.True(revokeReplay.IsSuccessStatusCode);
        using var explicitlyDenied = await Redeem(mother, token, Guid.NewGuid().ToString("N")); Assert.Equal(HttpStatusCode.NotFound, explicitlyDenied.StatusCode);
        using var next = await PostAsync(father, "/api/v1/families/mine/join-invitations", new { childPatientId = child, intendedParentRole = "Mother" }, Guid.NewGuid().ToString("N")); var nextToken = (await JsonAsync(next)).GetProperty("invitationReference").GetString()!;
        f.Clock.UtcNow = f.Clock.UtcNow.AddMinutes(31); using var expired = await Redeem(mother, nextToken, Guid.NewGuid().ToString("N")); Assert.Equal(HttpStatusCode.Conflict, expired.StatusCode);
        using var finalInvite = await PostAsync(father, "/api/v1/families/mine/join-invitations", new { childPatientId = child, intendedParentRole = "Mother" }, Guid.NewGuid().ToString("N")); var finalToken = (await JsonAsync(finalInvite)).GetProperty("invitationReference").GetString()!;
        await RevokeProof(f, child); using var denied = await Redeem(mother, finalToken, Guid.NewGuid().ToString("N")); Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Different_family_selection_persists_private_case_without_transferring_membership(bool sql)
    {
        await using var f = await CreateAsync(sql); Enable(f); using var a = await f.ClientAsync("patient"); using var b = await f.ClientAsync("other-patient"); using var admin = await f.ClientAsync("root");
        _ = await Child(f, a, admin, "طفل الأسرة الأولى"); var target = await Child(f, b, admin, "طفل الأسرة الثانية");
        using var search = await PostAsync(a, "/api/v1/families/patient-candidates/search", new { nameAr = "طفل الأسرة الثانية", dateOfBirth = f.Today.AddYears(-8), gender = "Male" }); var token = Assert.Single((await JsonAsync(search)).GetProperty("candidates").EnumerateArray()).GetProperty("candidateReference").GetString()!;
        var key = Guid.NewGuid().ToString("N"); using var selected = await Candidate(a, token, key); Assert.Equal(HttpStatusCode.Accepted, selected.StatusCode); var receipt = await JsonAsync(selected); var id = receipt.GetProperty("conflictCaseId").GetGuid(); Assert.Equal(JsonValueKind.Null, receipt.GetProperty("requestId").ValueKind);
        Assert.DoesNotContain(target.ToString(), receipt.GetRawText(), StringComparison.OrdinalIgnoreCase);
        using var replay = await Candidate(a, token, key); Assert.Equal(id, (await JsonAsync(replay)).GetProperty("conflictCaseId").GetGuid());
        var own = await GetAsync(a, $"/api/v1/families/mine/governance-cases/{id}"); Assert.DoesNotContain(target.ToString(), own.GetRawText(), StringComparison.OrdinalIgnoreCase);
        var details = await GetAsync(admin, $"/api/v1/admin/family-governance-cases/{id}"); var row = Assert.Single(details.GetProperty("items").EnumerateArray()).GetProperty("request");
        using var attempted = await Decision(admin, id, "approve", row.GetProperty("rowVersion").GetString()!); Assert.True(attempted.IsSuccessStatusCode); Assert.Equal("PendingGovernanceDecision", (await JsonAsync(attempted)).GetProperty("status").GetString());
        await f.WithDbAsync(async db => { Assert.Equal(2, await db.Families.CountAsync()); Assert.Equal(4, await db.FamilyMembers.CountAsync()); Assert.Equal(2, await db.Set<ParentChildOperationalAuthority>().CountAsync()); Assert.Equal(FamilyGovernanceCaseStatus.PendingGovernanceDecision, (await db.Set<FamilyGovernanceCase>().SingleAsync()).Status); });
    }
    private static void Enable(Phase13ApiFixture f) => f.Services.GetRequiredService<IOptions<DependentManagementOptions>>().Value.Enabled = true;
    private static async Task<Guid> Child(Phase13ApiFixture f, HttpClient parent, HttpClient admin, string name)
    {
        using var form = Evidence(); form.Add(new StringContent(name), "nameAr"); form.Add(new StringContent(f.Today.AddYears(-8).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)), "dateOfBirth"); form.Add(new StringContent("Male"), "gender"); form.Add(new StringContent("Father"), "relationToChild"); form.Add(new StringContent("ولي الأمر"), "primaryContactNameAr"); form.Add(new StringContent("01000000000"), "primaryContactPhone");
        using var submitted = await Send(parent, "/api/v1/families/mine/child-onboarding", form, Guid.NewGuid().ToString("N")); Assert.True(submitted.IsSuccessStatusCode, await submitted.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)); var intake = await JsonAsync(submitted); var id = intake.GetProperty("requestId").GetGuid();
        using var approved = await PostAsync(admin, $"/api/v1/admin/child-onboarding-requests/{id}/approve", new { rowVersion = intake.GetProperty("rowVersion").GetString() }, Guid.NewGuid().ToString("N")); Assert.True(approved.IsSuccessStatusCode, await approved.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)); return (await JsonAsync(approved)).GetProperty("approvedPatientId").GetGuid();
    }
    private static async Task RevokeProof(Phase13ApiFixture f, Guid child) => await f.WithDbAsync(async db => { var proof = await db.Set<ParentChildOperationalAuthority>().SingleAsync(p => p.ChildPatientId == child && p.IsActive); var admin = await db.ApplicationUsers.SingleAsync(u => u.UserName == "root"); proof.Revoke(admin.Id, f.Clock.UtcNow, "Revoked test proof", Guid.NewGuid().ToString("N")); db.Add(new OperationalAuthorityHistory(proof.Id, admin.Id, "Revoked", f.Clock.UtcNow)); await db.SaveChangesAsync(); });
    private static Task<HttpResponseMessage> Decision(HttpClient admin, Guid id, string operation, string version, string? key = null) => PostAsync(admin, $"/api/v1/admin/family-governance-cases/{id}/{operation}", new { rowVersion = version, reason = "Private identity review" }, key ?? Guid.NewGuid().ToString("N"));
    private static Task<HttpResponseMessage> Correction(HttpClient client, Guid child, DateOnly birth, string name, string key, Guid? id = null, string? version = null)
    {
        var form = Evidence(); form.Add(new StringContent(name), "nameAr"); form.Add(new StringContent(birth.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)), "dateOfBirth"); form.Add(new StringContent("Male"), "gender"); form.Add(new StringContent("Evidence based correction"), "reason"); if (id.HasValue) form.Add(new StringContent(id.Value.ToString()), "caseId"); if (version is not null) form.Add(new StringContent(version), "rowVersion"); return Send(client, $"/api/v1/families/mine/dependents/{child}/identity-corrections", form, key);
    }
    private static Task<HttpResponseMessage> Redeem(HttpClient client, string token, string key) { var form = Evidence(); form.Add(new StringContent(token), "invitationReference"); form.Add(new StringContent("Mother"), "intendedParentRole"); return Send(client, "/api/v1/families/mine/join-requests", form, key); }
    private static Task<HttpResponseMessage> Candidate(HttpClient client, string token, string key) { var form = Evidence(); form.Add(new StringContent(token), "candidateReference"); form.Add(new StringContent("Father"), "relationshipType"); return Send(client, "/api/v1/family-relationship-requests/from-candidate", form, key); }
    private static async Task<HttpResponseMessage> Send(HttpClient client, string path, MultipartFormDataContent form, string key) { using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = form }; request.Headers.Add("Idempotency-Key", key); return await client.SendAsync(request, TestContext.Current.CancellationToken); }
    private static MultipartFormDataContent Evidence() { var form = new MultipartFormDataContent(); var file = new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.4\nreview evidence\n%%EOF")); file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf"); form.Add(file, "evidenceFiles", "proof.pdf"); form.Add(new StringContent("BirthCertificate"), "documentTypes"); return form; }
}
