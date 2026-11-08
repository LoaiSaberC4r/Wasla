using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
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

public sealed class FamilySelfServiceApiTests
{
    private static void Enable(Phase13ApiFixture f)
    {
        f.Services.GetRequiredService<IOptions<DependentManagementOptions>>().Value.Enabled = true;
        f.Services.GetRequiredService<IOptions<MedicalRecordOptions>>().Value.GuardianSharingEnabled = false;
    }
    [Fact]
    public async Task Empty_workspace_is_success_and_legacy_family_contract_remains_not_found()
    {
        await using var f = await CreateAsync(); Enable(f); using var patient = await f.ClientAsync("patient");
        using var response = await patient.GetAsync("/api/v1/families/mine/workspace", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await JsonAsync(response); Assert.False(json.GetProperty("hasFamily").GetBoolean());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("family").ValueKind);
        Assert.True(json.GetProperty("canStartChildOnboarding").GetBoolean());
        using var legacy = await patient.GetAsync("/api/v1/families/mine", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, legacy.StatusCode);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Child_intake_has_no_patient_until_atomic_approval_and_medical_off_does_not_block_operational_authority(bool sql)
    {
        await using var f = await CreateAsync(sql); Enable(f);
        using var patient = await f.ClientAsync("patient"); using var admin = await f.ClientAsync("root");
        var before = 0; await f.WithDbAsync(async db => before = await db.Patients.CountAsync());
        var key = Guid.NewGuid().ToString("N");
        using var submitted = await SubmitChild(patient, f.Today.AddYears(-8), key);
        Assert.True(submitted.StatusCode == HttpStatusCode.Accepted, await submitted.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var intake = await JsonAsync(submitted); var request = intake.GetProperty("requestId").GetGuid();
        await f.WithDbAsync(async db => { Assert.Equal(before, await db.Patients.CountAsync()); Assert.Empty(await db.Set<ParentChildOperationalAuthority>().ToArrayAsync()); });
        using var replay = await SubmitChild(patient, f.Today.AddYears(-8), key);
        Assert.Equal(request, (await JsonAsync(replay)).GetProperty("requestId").GetGuid());
        var approvalKey = Guid.NewGuid().ToString("N");
        using var approvalRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/child-onboarding-requests/{request}/approve")
            { Content = System.Net.Http.Json.JsonContent.Create(new { rowVersion = intake.GetProperty("rowVersion").GetString() }) };
        approvalRequest.Headers.Add("Idempotency-Key", approvalKey);
        using var approved = await admin.SendAsync(approvalRequest, TestContext.Current.CancellationToken);
        Assert.True(approved.IsSuccessStatusCode, await approved.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var child = (await JsonAsync(approved)).GetProperty("approvedPatientId").GetGuid();
        await f.WithDbAsync(async db => {
            Assert.Equal(before + 1, await db.Patients.CountAsync());
            Assert.False(await db.PatientAccountLinks.AnyAsync(l => l.PatientId == child));
            Assert.Single(await db.Set<ParentChildOperationalAuthority>().Where(p => p.ChildPatientId == child && p.IsActive).ToArrayAsync());
        });
        var bookable = await GetAsync(patient, "/api/v1/reservations/bookable-patients");
        Assert.Contains(bookable.EnumerateArray(), p => p.GetProperty("patientId").GetGuid() == child);
        using var profile = await patient.GetAsync($"/api/v1/families/mine/dependents/{child}/profile", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        using var queue = await patient.GetAsync($"/api/v1/tickets/dependents/{child}/active", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, queue.StatusCode);
        using var transactions = await patient.GetAsync($"/api/v1/families/mine/dependents/{child}/financial-transactions?patientId={f.OtherPatientId}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, transactions.StatusCode);
        Assert.Empty((await JsonAsync(transactions)).GetProperty("items").EnumerateArray());
        using var missingReceipt = await patient.GetAsync($"/api/v1/families/mine/dependents/{child}/payments/{Guid.NewGuid()}/receipt", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, missingReceipt.StatusCode);
        var managedProfile = await JsonAsync(profile);
        var originalContact = Assert.Single(managedProfile.GetProperty("contacts").EnumerateArray()).GetProperty("contactId").GetGuid();
        using var deleteLastRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/families/mine/dependents/{child}/contacts/{originalContact}")
        { Content = System.Net.Http.Json.JsonContent.Create(new { rowVersion = managedProfile.GetProperty("rowVersion").GetString() }) };
        deleteLastRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var deleteLast = await patient.SendAsync(deleteLastRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, deleteLast.StatusCode);
        var contactBody = new { rowVersion = managedProfile.GetProperty("rowVersion").GetString(), contact = new { nameAr = "جهة اتصال إضافية", phoneNumber = "01000000000", relationshipType = "Other", isPrimary = true } };
        var contactKey = Guid.NewGuid().ToString("N");
        using var addContact = await PostAsync(patient, $"/api/v1/families/mine/dependents/{child}/contacts", contactBody, contactKey);
        Assert.True(addContact.IsSuccessStatusCode, await addContact.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var change = await JsonAsync(addContact);
        using var contactReplay = await PostAsync(patient, $"/api/v1/families/mine/dependents/{child}/contacts", contactBody, contactKey);
        Assert.Equal(change.GetProperty("changeId").GetGuid(), (await JsonAsync(contactReplay)).GetProperty("changeId").GetGuid());
        var currentProfile = await GetAsync(patient, $"/api/v1/families/mine/dependents/{child}/profile");
        Assert.Equal(2, currentProfile.GetProperty("contacts").GetArrayLength());
        using var staleContact = await PostAsync(patient, $"/api/v1/families/mine/dependents/{child}/contacts", contactBody, Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.Conflict, staleContact.StatusCode);
        using var clinical = await patient.GetAsync($"/api/v1/families/mine/dependents/{child}/medical-record/overview", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, clinical.StatusCode);
        var sibling = Guid.NewGuid();
        await f.WithDbAsync(async db => {
            var family = await db.Families.Include(p => p.Members).SingleAsync();
            var reviewer = await db.ApplicationUsers.SingleAsync(u => u.UserName == "root");
            var parentLink = await db.PatientAccountLinks.SingleAsync(l => l.PatientId == f.PatientId);
            db.Add(Patient.Create(sibling, "أخت دون إثبات", null, f.Today.AddYears(-6), Gender.Female, null, null, null, null, null, f.Today).Value);
            var historicalMembership = FamilyRelationshipRequest.Create(Guid.NewGuid(), FamilyRelationshipRequestType.AddFamilyMember, family.Id,
                f.PatientId, sibling, FamilyMemberRole.Father, FamilyMemberRole.Child, parentLink.ApplicationUserId, f.Clock.UtcNow).Value;
            historicalMembership.Approve(reviewer.Id, f.Clock.UtcNow); db.Add(historicalMembership);
            db.Add(family.AddMember(Guid.NewGuid(), sibling, FamilyMemberRole.Child, f.Clock.UtcNow, reviewer.Id, historicalMembership.Id).Value);
            await db.SaveChangesAsync();
        });
        var after = await GetAsync(patient, "/api/v1/reservations/bookable-patients");
        Assert.DoesNotContain(after.EnumerateArray(), p => p.GetProperty("patientId").GetGuid() == sibling);
        using var siblingQueue = await patient.GetAsync($"/api/v1/tickets/dependents/{sibling}/active", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, siblingQueue.StatusCode);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Search_is_AND_masked_actor_bound_and_legacy_raw_identity_cannot_bootstrap_a_relationship(bool sql)
    {
        await using var f = await CreateAsync(sql); Enable(f);
        var child = Guid.NewGuid(); var birth = f.Today.AddYears(-8);
        await f.WithDbAsync(async db => { db.Add(Patient.Create(child, "أَحْمَد طفل البحث", null, birth, Gender.Male, null, null, null, null, null, f.Today).Value); await db.SaveChangesAsync(); });
        using var patient = await f.ClientAsync("patient"); using var other = await f.ClientAsync("other-patient");
        using var search = await PostAsync(patient, "/api/v1/families/patient-candidates/search", new { nameAr = "احمد طفل البحث", dateOfBirth = birth, gender = "Male" });
        Assert.True(search.IsSuccessStatusCode, await search.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var json = await JsonAsync(search); var candidate = Assert.Single(json.GetProperty("candidates").EnumerateArray());
        var raw = json.GetRawText(); Assert.DoesNotContain(child.ToString(), raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("patientId", raw, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("phone", raw, StringComparison.OrdinalIgnoreCase);
        using var wrongDob = await PostAsync(patient, "/api/v1/families/patient-candidates/search", new { nameAr = "احمد طفل البحث", dateOfBirth = birth.AddDays(1), gender = "Male", phoneNumber = "01000000000" });
        Assert.Empty((await JsonAsync(wrongDob)).GetProperty("candidates").EnumerateArray());
        var reference = candidate.GetProperty("candidateReference").GetString()!;
        using var stolen = await SubmitCandidate(other, reference);
        Assert.Equal(HttpStatusCode.NotFound, stolen.StatusCode);
        using var selected = await SubmitCandidate(patient, reference);
        Assert.Equal(HttpStatusCode.Accepted, selected.StatusCode);
        var requestId = (await JsonAsync(selected)).GetProperty("requestId").GetGuid();
        var details = await GetAsync(patient, $"/api/v1/family-relationship-requests/{requestId}");
        Assert.Equal(JsonValueKind.Null, details.GetProperty("target").ValueKind);
        Assert.Empty(details.GetProperty("familyMembers").EnumerateArray());
        Assert.DoesNotContain(child.ToString(), details.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    [Trait("Category", "SQLServerConcurrency")]
    public async Task Sql_parallel_approval_has_one_identity_one_authority_and_replay_is_durable()
    {
        await using var f = await CreateAsync(true); Enable(f);
        using var parent = await f.ClientAsync("patient"); using var admin = await f.ClientAsync("root"); using var secondAdmin = await f.ClientAsync("root");
        var before = 0; await f.WithDbAsync(async db => before = await db.Patients.CountAsync());
        using var submitted = await SubmitChild(parent, f.Today.AddYears(-8), Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.Accepted, submitted.StatusCode);
        var request = await JsonAsync(submitted); var id = request.GetProperty("requestId").GetGuid(); var version = request.GetProperty("rowVersion").GetString()!;
        var keys = new[] { Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N") };
        var results = await Task.WhenAll(Approve(admin, id, version, keys[0]), Approve(secondAdmin, id, version, keys[1]));
        try
        {
            Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK);
            Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict);
            var winner = Array.FindIndex(results, r => r.StatusCode == HttpStatusCode.OK);
            using var replay = await Approve(admin, id, version, keys[winner]);
            Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
            var approved = await JsonAsync(replay);
            await f.WithDbAsync(async db => {
                Assert.Equal(before + 1, await db.Patients.CountAsync()); Assert.Single(await db.Families.ToArrayAsync());
                Assert.Equal(2, await db.FamilyMembers.CountAsync()); Assert.Single(await db.Set<ParentChildOperationalAuthority>().ToArrayAsync());
                Assert.Equal(2, await db.Set<ChildOnboardingHistory>().Where(h => h.RequestId == id).CountAsync());
            });
            var relationship = approved.GetProperty("familyRelationshipRequestId").GetGuid();
            var authorities = await GetAsync(admin, $"/api/v1/admin/family-operational-authorities?relationshipRequestId={relationship}");
            var authority = Assert.Single(authorities.EnumerateArray());
            using var revoke = await PostAsync(admin, $"/api/v1/admin/family-operational-authorities/{authority.GetProperty("authorityId").GetGuid()}/revoke",
                new { reason = "Independent reviewed revocation", rowVersion = authority.GetProperty("rowVersion").GetString() }, Guid.NewGuid().ToString("N"));
            Assert.True(revoke.IsSuccessStatusCode, await revoke.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            var child = approved.GetProperty("approvedPatientId").GetGuid();
            using var denied = await parent.GetAsync($"/api/v1/tickets/dependents/{child}/active", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
            await f.WithDbAsync(async db => { Assert.Equal(before + 1, await db.Patients.CountAsync()); Assert.False((await db.Set<ParentChildOperationalAuthority>().SingleAsync()).IsActive); });
        }
        finally { foreach (var response in results) response.Dispose(); }
    }
    private static Task<HttpResponseMessage> Approve(HttpClient client, Guid requestId, string version, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/child-onboarding-requests/{requestId}/approve")
            { Content = System.Net.Http.Json.JsonContent.Create(new { rowVersion = version }) };
        request.Headers.Add("Idempotency-Key", key); return client.SendAsync(request, TestContext.Current.CancellationToken);
    }
    [Fact]
    public async Task Applicant_status_never_echoes_confidential_reviewer_text()
    {
        await using var f = await CreateAsync(); Enable(f);
        using var parent = await f.ClientAsync("patient"); using var admin = await f.ClientAsync("root");
        using var submitted = await SubmitChild(parent, f.Today.AddYears(-8), Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.Accepted, submitted.StatusCode);
        var request = await JsonAsync(submitted); var id = request.GetProperty("requestId").GetGuid();
        var confidential = "PRIVATE_REVIEW_ONLY_" + Guid.NewGuid().ToString("N");
        using var rejected = await PostAsync(admin, $"/api/v1/admin/child-onboarding-requests/{id}/reject",
            new { rowVersion = request.GetProperty("rowVersion").GetString(), reason = confidential }, Guid.NewGuid().ToString("N"));
        Assert.True(rejected.IsSuccessStatusCode, await rejected.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var own = await GetAsync(parent, $"/api/v1/families/mine/child-onboarding-requests/{id}");
        Assert.DoesNotContain(confidential, own.GetRawText(), StringComparison.Ordinal);
        var review = await GetAsync(admin, $"/api/v1/admin/child-onboarding-requests/{id}");
        Assert.Contains(confidential, review.GetRawText(), StringComparison.Ordinal);
        await f.WithDbAsync(async db => { Assert.Null((await db.Set<ChildOnboardingRequest>().SingleAsync()).ApprovedPatientId); });
    }
    private static Task<HttpResponseMessage> SubmitChild(HttpClient client, DateOnly birth, string key)
    {
        var form = Evidence(); form.Add(new StringContent("طفل جديد"), "nameAr"); form.Add(new StringContent(birth.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)), "dateOfBirth");
        form.Add(new StringContent("Male"), "gender"); form.Add(new StringContent("Father"), "relationToChild");
        form.Add(new StringContent("ولي الأمر"), "primaryContactNameAr"); form.Add(new StringContent("01000000000"), "primaryContactPhone");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/families/mine/child-onboarding") { Content = form };
        request.Headers.Add("Idempotency-Key", key); return client.SendAsync(request, TestContext.Current.CancellationToken);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Security_seed_replay_keeps_custom_permissions_roles_grants_and_all_stable_ids(bool sql)
    {
        await using var f = await CreateAsync(sql);
        var customRole = Guid.NewGuid(); var customPermission = Guid.NewGuid(); var customGrant = Guid.NewGuid();
        Dictionary<string, Guid> before = [];
        await f.WithDbAsync(async db => {
            db.Add(Wasla.Domain.Security.Role.Create(customRole, "TestCustomFamilyRole", false).Value);
            db.Add(Wasla.Domain.Security.Permission.Create(customPermission, "Test.CustomFamilyPermission", false).Value);
            db.Add(new Wasla.Domain.Security.RolePermission(customGrant, customRole, customPermission)); await db.SaveChangesAsync();
            before = await db.Permissions.ToDictionaryAsync(p => p.Name, p => p.Id);
        });
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var type = typeof(WaslaDbContext).Assembly.GetType("Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence.WaslaSecuritySeeder")!;
            var seed = type.GetMethod("SeedAsync")!; var service = scope.ServiceProvider.GetRequiredService(type);
            await (Task)seed.Invoke(service, [TestContext.Current.CancellationToken])!;
            await (Task)seed.Invoke(service, [TestContext.Current.CancellationToken])!;
        }
        await f.WithDbAsync(async db => {
            var after = await db.Permissions.ToDictionaryAsync(p => p.Name, p => p.Id); Assert.Equal(before.Count, after.Count);
            foreach (var pair in before) Assert.Equal(pair.Value, after[pair.Key]);
            Assert.True(await db.Roles.AnyAsync(r => r.Id == customRole));
            Assert.True(await db.RolePermissions.AnyAsync(r => r.Id == customGrant && r.RoleId == customRole && r.PermissionId == customPermission));
            foreach (var name in Wasla.Domain.Security.PermissionNames.All) Assert.Equal(Wasla.Domain.Security.SystemPermissionIds.For(name), after[name]);
        });
    }
    [Fact]
    public async Task Requested_intake_corrections_keep_old_snapshot_and_replay_original_revision()
    {
        await using var f = await CreateAsync(); Enable(f); using var parent = await f.ClientAsync("patient"); using var admin = await f.ClientAsync("root");
        using var first = await SubmitChild(parent, f.Today.AddYears(-8), Guid.NewGuid().ToString("N")); var original = await JsonAsync(first);
        var id = original.GetProperty("requestId").GetGuid();
        using var modified = await PostAsync(admin, $"/api/v1/admin/child-onboarding-requests/{id}/request-modification", new { rowVersion = original.GetProperty("rowVersion").GetString(), reason = "Private review" });
        Assert.True(modified.IsSuccessStatusCode); var version = (await JsonAsync(modified)).GetProperty("rowVersion").GetString()!;
        var key = Guid.NewGuid().ToString("N");
        async Task<HttpResponseMessage> Resubmit()
        {
            using var form = Evidence(); form.Add(new StringContent(version), "rowVersion"); form.Add(new StringContent("الاسم المصحح"), "nameAr");
            form.Add(new StringContent(f.Today.AddYears(-9).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)), "dateOfBirth");
            form.Add(new StringContent("Male"), "gender"); form.Add(new StringContent("Father"), "relationToChild"); form.Add(new StringContent("ولي الأمر"), "primaryContactNameAr"); form.Add(new StringContent("01000000000"), "primaryContactPhone");
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/families/mine/child-onboarding-requests/{id}/resubmit") { Content = form }; request.Headers.Add("Idempotency-Key", key);
            return await parent.SendAsync(request, TestContext.Current.CancellationToken);
        }
        using var resubmitted = await Resubmit(); Assert.True(resubmitted.IsSuccessStatusCode, await resubmitted.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var revised = await JsonAsync(resubmitted); Assert.Equal(2, revised.GetProperty("revision").GetInt32());
        using var approved = await Approve(admin, id, revised.GetProperty("rowVersion").GetString()!, Guid.NewGuid().ToString("N")); Assert.True(approved.IsSuccessStatusCode);
        using var replay = await Resubmit(); var replayed = await JsonAsync(replay); Assert.Equal(2, replayed.GetProperty("revision").GetInt32()); Assert.Equal("PendingReview", replayed.GetProperty("status").GetString());
        await f.WithDbAsync(async db => {
            var snapshots = await db.Set<ChildOnboardingRevision>().Where(v => v.RequestId == id).OrderBy(v => v.Revision).ToArrayAsync(); Assert.Equal(2, snapshots.Length);
            Assert.Equal("طفل جديد", snapshots[0].NameAr); Assert.Equal("الاسم المصحح", snapshots[1].NameAr);
            var request = await db.Set<ChildOnboardingRequest>().SingleAsync(r => r.Id == id); Assert.Equal("الاسم المصحح", (await db.Patients.SingleAsync(p => p.Id == request.ApprovedPatientId)).NameAr);
        });
    }
    private static Task<HttpResponseMessage> SubmitCandidate(HttpClient client, string reference)
    {
        var form = Evidence(); form.Add(new StringContent(reference), "candidateReference"); form.Add(new StringContent("Father"), "relationshipType");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/family-relationship-requests/from-candidate") { Content = form };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N")); return client.SendAsync(request, TestContext.Current.CancellationToken);
    }
    private static MultipartFormDataContent Evidence()
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.4\nreview evidence\n%%EOF")); file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "evidenceFiles", "proof.pdf"); form.Add(new StringContent("BirthCertificate"), "documentTypes"); return form;
    }
}
