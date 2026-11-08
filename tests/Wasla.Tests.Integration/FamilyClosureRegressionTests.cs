using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.Families;
using Wasla.Domain.Common;
using Wasla.Domain.Families;
using Wasla.Domain.Patients;
using Wasla.Domain.Security;
using Wasla.Domain.Tickets;
using static Wasla.Tests.Integration.Phase13ApiFixture;

namespace Wasla.Tests.Integration;

[Trait("Category", "SQLServerConcurrency")]
public sealed class FamilyClosureRegressionTests(Xunit.ITestOutputHelper output)
{
    [Fact]
    public async Task Discovery_bounds_ambiguous_results_and_phone_only_ranks_exact_matches_with_private_review_continuation()
    {
        await using var f = await CreateAsync(true); Enable(f); using var parent = await f.ClientAsync("patient");
        var ids = new List<Guid>();
        await f.WithDbAsync(async db => {
            for (var i = 0; i < 7; i++)
            {
                var p = Patient.Create(Guid.NewGuid(), "هوية متشابهة", null, f.Today.AddYears(-9), Gender.Male, i == 6 ? "01011111111" : "01022222222", null, null, null, null, f.Today).Value;
                ids.Add(p.Id); db.Add(p);
            }
            db.Add(Patient.Create(Guid.NewGuid(), "اسم مختلف بنفس الهاتف", null, f.Today.AddYears(-9), Gender.Male, "01011111111", null, null, null, null, f.Today).Value);
            await db.SaveChangesAsync();
        });
        using var found = await PostAsync(parent, "/api/v1/families/patient-candidates/search", new { nameAr = "هوية متشابهة", dateOfBirth = f.Today.AddYears(-9), gender = "Male", phoneNumber = "01011111111" });
        Assert.Equal(HttpStatusCode.OK, found.StatusCode); var json = await JsonAsync(found);
        Assert.Equal("ManualReviewRequired", json.GetProperty("searchStatus").GetString()); Assert.True(json.GetProperty("hasMore").GetBoolean());
        Assert.Equal("SubmitChildOnboardingForManualReview", json.GetProperty("nextAction").GetString());
        Assert.Equal(5, json.GetProperty("candidates").GetArrayLength());
        foreach (var id in ids) Assert.DoesNotContain(id.ToString(), json.GetRawText(), StringComparison.OrdinalIgnoreCase);
        foreach (var forbidden in new[] { "patientId", "familyId", "phoneNumber", "dateOfBirth", "email", "accountLink" }) Assert.DoesNotContain(forbidden, json.GetRawText(), StringComparison.OrdinalIgnoreCase);
        var token = json.GetProperty("candidates")[0].GetProperty("candidateReference").GetString()!;
        await f.WithDbAsync(async db => { var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(token))); Assert.Equal(ids[6], (await db.Set<FamilyCandidateSelection>().SingleAsync(s => s.TokenHash == hash)).TargetPatientId); });
        using var shared = await PostAsync(parent, "/api/v1/families/patient-candidates/search", new { nameAr = "هوية متشابهة", dateOfBirth = f.Today.AddYears(-9), gender = "Male", phoneNumber = "01022222222" });
        Assert.Equal(5, (await JsonAsync(shared)).GetProperty("candidates").GetArrayLength());
        using var noMatch = await PostAsync(parent, "/api/v1/families/patient-candidates/search", new { nameAr = "لا يوجد", dateOfBirth = f.Today.AddYears(-9), gender = "Male" });
        Assert.Empty((await JsonAsync(noMatch)).GetProperty("candidates").EnumerateArray());
        for (var i = 0; i < 17; i++) { using var result = await PostAsync(parent, "/api/v1/families/patient-candidates/search", new { nameAr = "لا يوجد", dateOfBirth = f.Today.AddYears(-9), gender = "Male" }); Assert.Equal(HttpStatusCode.OK, result.StatusCode); }
        using var limited = await PostAsync(parent, "/api/v1/families/patient-candidates/search", new { nameAr = "لا يوجد", dateOfBirth = f.Today.AddYears(-9), gender = "Male" });
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }

    [Fact]
    public async Task Existing_child_onboarding_conflict_retains_intake_and_private_case_without_creating_patient_or_changing_contacts()
    {
        await using var f = await CreateAsync(true); Enable(f);
        using var parent = await f.ClientAsync("patient"); using var other = await f.ClientAsync("other-patient"); using var admin = await f.ClientAsync("root");
        _ = await Child(f, parent, admin, "طفل الأسرة الأولى");
        var target = await Child(f, other, admin, "طفل الأسرة الثانية");
        int count = 0, contacts = 0; await f.WithDbAsync(async db => { count = await db.Patients.CountAsync(); contacts = await db.PatientContacts.CountAsync(); });
        using var submitted = await Submit(parent, f.Today.AddYears(-8), "طفل الأسرة الثانية"); var intake = await JsonAsync(submitted);
        var id = intake.GetProperty("requestId").GetGuid(); var key = Guid.NewGuid().ToString("N");
        var decision = new { rowVersion = intake.GetProperty("rowVersion").GetString(), confirmedExistingPatientId = target };
        using var reviewed = await PostAsync(admin, $"/api/v1/admin/child-onboarding-requests/{id}/approve", decision, key);
        Assert.Equal(HttpStatusCode.OK, reviewed.StatusCode); var receipt = await JsonAsync(reviewed);
        Assert.Equal("ConflictReview", receipt.GetProperty("status").GetString()); var caseId = receipt.GetProperty("conflictCaseId").GetGuid();
        Assert.Equal(JsonValueKind.Null, receipt.GetProperty("approvedPatientId").ValueKind);
        using var replay = await PostAsync(admin, $"/api/v1/admin/child-onboarding-requests/{id}/approve", decision, key);
        Assert.Equal(caseId, (await JsonAsync(replay)).GetProperty("conflictCaseId").GetGuid());
        var own = await GetAsync(parent, $"/api/v1/families/mine/child-onboarding-requests/{id}");
        Assert.DoesNotContain(target.ToString(), own.GetRawText(), StringComparison.OrdinalIgnoreCase);
        await f.WithDbAsync(async db => { Assert.Equal(count, await db.Patients.CountAsync()); Assert.Equal(contacts, await db.PatientContacts.CountAsync()); Assert.Equal(2, await db.Families.CountAsync()); Assert.Single(await db.Set<FamilyGovernanceCase>().ToArrayAsync()); Assert.Single(await db.Set<FamilyGovernanceEvidence>().Where(e => e.CaseId == caseId).ToArrayAsync()); });
        var workspace = await GetAsync(parent, "/api/v1/families/mine/workspace");
        Assert.Contains(workspace.GetProperty("pendingWorkflows").EnumerateArray(), r => r.GetProperty("reference").GetGuid() == caseId);
    }
    [Fact]
    public async Task Candidate_expiration_replay_wrong_actor_and_identity_reassignment_never_grant_access()
    {
        await using var f = await CreateAsync(true); Enable(f); using var parent = await f.ClientAsync("patient"); using var other = await f.ClientAsync("other-patient");
        await f.WithDbAsync(async db => { for (var i = 0; i < 2; i++) db.Add(Patient.Create(Guid.NewGuid(), "طفل اكتشاف", null, f.Today.AddYears(-8), Gender.Male, "01011111111", null, null, null, null, f.Today).Value); await db.SaveChangesAsync(); });
        async Task<JsonElement> Search()
        { using var response = await PostAsync(parent, "/api/v1/families/patient-candidates/search", new { nameAr = "طفل اكتشاف", dateOfBirth = f.Today.AddYears(-8), gender = "Male" }); Assert.Equal(HttpStatusCode.OK, response.StatusCode); return await JsonAsync(response); }
        async Task<HttpResponseMessage> Select(HttpClient actor, string token, string key)
        {
            using var form = Evidence(); form.Add(new StringContent(token), "candidateReference"); form.Add(new StringContent("Father"), "relationshipType");
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/family-relationship-requests/from-candidate") { Content = form }; request.Headers.Add("Idempotency-Key", key);
            return await actor.SendAsync(request, TestContext.Current.CancellationToken);
        }
        var candidates = (await Search()).GetProperty("candidates"); var token = candidates[0].GetProperty("candidateReference").GetString()!; var next = candidates[1].GetProperty("candidateReference").GetString()!;
        using var stolen = await Select(other, token, Guid.NewGuid().ToString("N")); Assert.Equal(HttpStatusCode.NotFound, stolen.StatusCode);
        var key = Guid.NewGuid().ToString("N"); using var submitted = await Select(parent, token, key); Assert.Equal(HttpStatusCode.Accepted, submitted.StatusCode); var requestId = (await JsonAsync(submitted)).GetProperty("requestId").GetGuid();
        using var replay = await Select(parent, token, key); Assert.Equal(requestId, (await JsonAsync(replay)).GetProperty("requestId").GetGuid());
        using var reused = await Select(parent, token, Guid.NewGuid().ToString("N")); Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);
        using var reassignedKey = await Select(parent, next, key); Assert.Equal(HttpStatusCode.Conflict, reassignedKey.StatusCode);
        f.Clock.UtcNow = f.Clock.UtcNow.AddMinutes(11); using var expired = await Select(parent, next, Guid.NewGuid().ToString("N")); Assert.Equal(HttpStatusCode.NotFound, expired.StatusCode);
        var fresh = (await Search()).GetProperty("candidates")[0].GetProperty("candidateReference").GetString()!;
        await f.WithDbAsync(async db => {
            var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(fresh))); var selection = await db.Set<FamilyCandidateSelection>().SingleAsync(s => s.TokenHash == hash);
            var patient = await db.Patients.SingleAsync(p => p.Id == selection.TargetPatientId); Assert.True(patient.CorrectIdentity("هوية مصححة بالمراجعة", null, patient.DateOfBirth, patient.Gender, f.Today).IsSuccess); await db.SaveChangesAsync();
        });
        using var staleIdentity = await Select(parent, fresh, Guid.NewGuid().ToString("N")); Assert.Equal(HttpStatusCode.NotFound, staleIdentity.StatusCode);
        await f.WithDbAsync(async db => { Assert.Empty(await db.FamilyMembers.ToArrayAsync()); Assert.Empty(await db.Set<ParentChildOperationalAuthority>().ToArrayAsync()); });
    }
    [Fact]
    public async Task Ambiguous_manual_review_pages_all_identities_and_explicitly_reuses_the_selected_existing_patient()
    {
        await using var f = await CreateAsync(true); Enable(f); using var parent = await f.ClientAsync("patient"); using var admin = await f.ClientAsync("root");
        int before = 0;
        await f.WithDbAsync(async db => { for (var i = 0; i < 7; i++) db.Add(Patient.Create(Guid.NewGuid(), "طفل مراجعة متشابه", null, f.Today.AddYears(-8), Gender.Male, "01011111111", null, null, null, null, f.Today).Value); await db.SaveChangesAsync(); before = await db.Patients.CountAsync(); });
        using var submitted = await Submit(parent, f.Today.AddYears(-8), "طفل مراجعة متشابه"); var intake = await JsonAsync(submitted); var id = intake.GetProperty("requestId").GetGuid();
        var firstPage = await GetAsync(admin, $"/api/v1/admin/child-onboarding-requests/{id}?matchPageSize=2"); var first = Assert.Single(firstPage.EnumerateArray());
        Assert.True(first.GetProperty("hasMorePossibleMatches").GetBoolean()); Assert.Equal(2, first.GetProperty("possibleMatches").GetArrayLength());
        var finalPage = await GetAsync(admin, $"/api/v1/admin/child-onboarding-requests/{id}?matchPageNumber=4&matchPageSize=2"); var last = Assert.Single(finalPage.EnumerateArray());
        Assert.False(last.GetProperty("hasMorePossibleMatches").GetBoolean()); var selected = Assert.Single(last.GetProperty("possibleMatches").EnumerateArray()).GetProperty("patientId").GetGuid();
        using var noSelection = await PostAsync(admin, $"/api/v1/admin/child-onboarding-requests/{id}/approve", new { rowVersion = intake.GetProperty("rowVersion").GetString() }); Assert.Equal(HttpStatusCode.Conflict, noSelection.StatusCode);
        using var approved = await PostAsync(admin, $"/api/v1/admin/child-onboarding-requests/{id}/approve", new { rowVersion = intake.GetProperty("rowVersion").GetString(), confirmedExistingPatientId = selected });
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode); Assert.Equal(selected, (await JsonAsync(approved)).GetProperty("approvedPatientId").GetGuid());
        await f.WithDbAsync(async db => { Assert.Equal(before, await db.Patients.CountAsync()); Assert.Single(await db.Set<ParentChildOperationalAuthority>().Where(p => p.ChildPatientId == selected && p.IsActive).ToArrayAsync()); });
    }

    [Theory]
    [InlineData(true, true, true)] [InlineData(true, false, true)] [InlineData(false, true, true)] [InlineData(false, false, false)]
    public async Task Revised_intake_uses_conditional_contact_rule(bool childPhone, bool guardianPhone, bool valid)
    {
        await using var f = await CreateAsync(true); Enable(f); using var parent = await f.ClientAsync("patient"); using var admin = await f.ClientAsync("root");
        using var submitted = await Submit(parent, f.Today.AddYears(-8), "طلب تصحيح"); var intake = await JsonAsync(submitted); var id = intake.GetProperty("requestId").GetGuid();
        using var modification = await PostAsync(admin, $"/api/v1/admin/child-onboarding-requests/{id}/request-modification", new { rowVersion = intake.GetProperty("rowVersion").GetString(), reason = "Synthetic evidence revision" });
        var revision = await JsonAsync(modification); using var form = Evidence();
        form.Add(new StringContent(revision.GetProperty("rowVersion").GetString()!), "rowVersion");
        form.Add(new StringContent("طلب مصحح"), "nameAr"); form.Add(new StringContent(f.Today.AddYears(-8).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)), "dateOfBirth");
        form.Add(new StringContent("Male"), "gender"); form.Add(new StringContent("Father"), "relationToChild");
        if (childPhone) form.Add(new StringContent("01011111111"), "phoneNumber");
        if (guardianPhone) { form.Add(new StringContent("ولي الأمر"), "primaryContactNameAr"); form.Add(new StringContent("01022222222"), "primaryContactPhone"); }
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/families/mine/child-onboarding-requests/{id}/resubmit") { Content = form }; request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        using var result = await parent.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(valid ? HttpStatusCode.OK : HttpStatusCode.UnprocessableEntity, result.StatusCode);
        await f.WithDbAsync(async db => Assert.Equal(valid ? 2 : 1, (await db.Set<ChildOnboardingRequest>().SingleAsync(r => r.Id == id)).Revision));
    }

    [Fact]
    public async Task Workspace_queries_remain_bounded_when_children_grow_and_financial_permissions_are_independent()
    {
        var counter = new Reads(); await using var f = await CreateAsync(true, counter); Enable(f);
        using var parent = await f.ClientAsync("patient"); using var admin = await f.ClientAsync("root");
        var child = await Child(f, parent, admin, "طفل أول");
        counter.Count = 0; counter.Enabled = true;
        _ = await GetAsync(parent, "/api/v1/families/mine/workspace"); var first = counter.Count; counter.Enabled = false;
        await f.WithDbAsync(async db => {
            var family = await db.Families.Include(r => r.Members).SingleAsync(); var reviewer = await db.ApplicationUsers.SingleAsync(u => u.UserName == "root");
            var actor = await db.PatientAccountLinks.SingleAsync(l => l.PatientId == f.PatientId);
            for (var i = 0; i < 12; i++)
            {
                var p = Patient.Create(Guid.NewGuid(), "أخ " + i, null, f.Today.AddYears(-6), Gender.Male, "01011111111", null, null, null, null, f.Today).Value; db.Add(p);
                var source = FamilyRelationshipRequest.Create(Guid.NewGuid(), FamilyRelationshipRequestType.AddFamilyMember, family.Id, f.PatientId, p.Id, FamilyMemberRole.Father, FamilyMemberRole.Child, actor.ApplicationUserId, f.Clock.UtcNow).Value;
                source.Approve(reviewer.Id, f.Clock.UtcNow); db.Add(source); db.Add(family.AddMember(Guid.NewGuid(), p.Id, FamilyMemberRole.Child, f.Clock.UtcNow, reviewer.Id, source.Id).Value);
            }
            await db.SaveChangesAsync();
            await db.RolePermissions.Where(r => r.RoleId == SystemRoleIds.Patient && r.PermissionId == SystemPermissionIds.For(FamilyExtensionPermissionNames.Receipts)).ExecuteDeleteAsync();
        });
        counter.Count = 0; counter.Enabled = true;
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var expanded = await GetAsync(parent, "/api/v1/families/mine/workspace"); var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(started); var second = counter.Count; counter.Enabled = false;
        output.WriteLine($"Workspace SQL reads: one child={first}; thirteen children={second}; expanded response={elapsed.TotalMilliseconds:F0}ms.");
        Assert.InRange(second, 1, first + 3); Assert.InRange(second, 1, 90); Assert.Equal(13, expanded.GetProperty("dependents").GetArrayLength());
        var own = expanded.GetProperty("dependents").EnumerateArray().Single(p => p.GetProperty("patientId").GetGuid() == child);
        Assert.True(own.GetProperty("canViewTransactions").GetBoolean()); Assert.False(own.GetProperty("canViewReceipts").GetBoolean());
        Assert.Equal("DependentAccess.PermissionRequired", own.GetProperty("capabilityReasons").GetProperty("canViewReceipts").GetString());
        using var receipt = await parent.GetAsync($"/api/v1/families/mine/dependents/{child}/payments/{Guid.NewGuid()}/receipt", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, receipt.StatusCode);
    }

    [Fact]
    public async Task Parent_reservation_ticket_navigation_uses_exact_pair_and_hides_reference_when_ticket_permission_is_revoked()
    {
        await using var f = await CreateAsync(true); Enable(f); using var parent = await f.ClientAsync("patient"); using var admin = await f.ClientAsync("root");
        var child = await Child(f, parent, admin, "طفل حجز");
        using var created = await PostAsync(parent, "/api/v1/reservations", new { patientId = child, doctorPracticeId = f.PracticeId, businessDate = f.Today, slotStartTime = "18:00", segmentId = f.SegmentId, visitTypeId = f.ConsultationTypeId });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode); var booking = await JsonAsync(created); var id = booking.GetProperty("reservationId").GetGuid(); var ticketId = Guid.NewGuid();
        await f.WithDbAsync(async db => {
            db.Add(Ticket.CreateFromReservation(new(ticketId, f.DoctorId, f.PracticeId, child, id, f.Today, 100, TicketSource.Reservation, f.SegmentId,
                "Synthetic", "Synthetic", 0, f.ConsultationTypeId, "NewConsultation", "Synthetic", "Synthetic", 300, "Africa/Cairo", f.Clock.UtcNow, f.Clock.UtcNow, CheckInMode.Normal, f.DoctorUserId, null)).Value);
            await db.SaveChangesAsync();
        });
        var detail = await GetAsync(parent, $"/api/v1/reservations/mine/{id}"); Assert.True(detail.GetProperty("canViewLinkedTicket").GetBoolean()); Assert.Equal(ticketId, detail.GetProperty("linkedTicketId").GetGuid());
        using var ticket = await parent.GetAsync($"/api/v1/tickets/dependents/{child}/{ticketId}", TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.OK, ticket.StatusCode);
        await f.WithDbAsync(db => db.RolePermissions.Where(r => r.RoleId == SystemRoleIds.Patient && r.PermissionId == SystemPermissionIds.For(FamilySelfServicePermissionNames.Tickets)).ExecuteDeleteAsync());
        var denied = await GetAsync(parent, $"/api/v1/reservations/mine/{id}"); Assert.False(denied.GetProperty("canViewLinkedTicket").GetBoolean()); Assert.Equal(JsonValueKind.Null, denied.GetProperty("linkedTicketId").ValueKind);
    }
    [Fact]
    public async Task Parent_reschedules_and_cancels_child_reservation_and_loses_all_new_reads_after_pair_revocation()
    {
        await using var f = await CreateAsync(true); Enable(f); using var parent = await f.ClientAsync("patient"); using var admin = await f.ClientAsync("root"); var child = await Child(f, parent, admin, "طفل رحلة الحجز");
        var bookable = await GetAsync(parent, "/api/v1/reservations/bookable-patients"); Assert.Contains(bookable.EnumerateArray(), p => p.GetProperty("patientId").GetGuid() == child);
        using var created = await PostAsync(parent, "/api/v1/reservations", new { patientId = child, doctorPracticeId = f.PracticeId, businessDate = f.Today, slotStartTime = "18:00", segmentId = f.SegmentId, visitTypeId = f.ConsultationTypeId });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode); var booking = await JsonAsync(created); var id = booking.GetProperty("reservationId").GetGuid();
        var list = await GetAsync(parent, $"/api/v1/reservations/mine?patientId={child}"); Assert.Contains(list.GetProperty("items").EnumerateArray(), p => p.GetProperty("reservationId").GetGuid() == id);
        using var moved = await PostAsync(parent, $"/api/v1/reservations/mine/{id}/reschedule", new { businessDate = f.Today.AddDays(1), slotStartTime = "18:00", rowVersion = booking.GetProperty("rowVersion").GetString() });
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode); var current = await JsonAsync(moved);
        using var cancelled = await PostAsync(parent, $"/api/v1/reservations/mine/{id}/cancel", new { reasonCode = "PatientChangedPlans", rowVersion = current.GetProperty("rowVersion").GetString() });
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        await f.WithDbAsync(async db => { var proof = await db.Set<ParentChildOperationalAuthority>().SingleAsync(p => p.ParentPatientId == f.PatientId && p.ChildPatientId == child && p.IsActive); var reviewer = await db.ApplicationUsers.SingleAsync(u => u.UserName == "root"); proof.Revoke(reviewer.Id, f.Clock.UtcNow, "Synthetic reviewed revocation", Guid.NewGuid().ToString("N")); db.Add(new OperationalAuthorityHistory(proof.Id, reviewer.Id, "Revoked", f.Clock.UtcNow)); await db.SaveChangesAsync(); });
        using var denied = await parent.GetAsync($"/api/v1/reservations/mine/{id}", TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.DoesNotContain((await GetAsync(parent, "/api/v1/reservations/bookable-patients")).EnumerateArray(), p => p.GetProperty("patientId").GetGuid() == child);
    }

    internal static void Enable(Phase13ApiFixture f) => f.Services.GetRequiredService<IOptions<DependentManagementOptions>>().Value.Enabled = true;
    [Fact]
    public async Task Concurrent_second_parent_reviews_preserve_one_active_role_and_occupied_role_is_rejected_at_submission()
    {
        await using var f = await CreateAsync(true); Enable(f); using var parent = await f.ClientAsync("patient"); using var admin = await f.ClientAsync("root");
        var child = await Child(f, parent, admin, "طفل التنافس");
        await f.WithDbAsync(async db => {
            var template = await db.ApplicationUsers.SingleAsync(u => u.UserName == "other-patient");
            var account = ApplicationUser.Create(Guid.NewGuid(), "contender", "contender@example.test", null, template.PasswordHash, UserType.Patient, false, f.Clock.UtcNow).Value;
            var person = Patient.Create(Guid.NewGuid(), "والد آخر", null, f.Today.AddYears(-35), Gender.Female, "01011111111", null, null, null, null, f.Today).Value;
            db.AddRange(account, person, new UserRole(Guid.NewGuid(), account.Id, SystemRoleIds.Patient), PatientAccountLink.Create(Guid.NewGuid(), account.Id, person.Id, PatientAccountLinkSource.SelfRegistration, f.Clock.UtcNow).Value);
            await db.SaveChangesAsync();
        });
        using var first = await f.ClientAsync("other-patient"); using var second = await f.ClientAsync("contender");
        async Task<HttpResponseMessage> Apply(HttpClient actor)
        {
            using var search = await PostAsync(actor, "/api/v1/families/patient-candidates/search", new { nameAr = "طفل التنافس", dateOfBirth = f.Today.AddYears(-8), gender = "Male" });
            var token = (await JsonAsync(search)).GetProperty("candidates")[0].GetProperty("candidateReference").GetString()!;
            using var form = Evidence(); form.Add(new StringContent(token), "candidateReference"); form.Add(new StringContent("Mother"), "relationshipType");
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/family-relationship-requests/from-candidate") { Content = form }; request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            return await actor.SendAsync(request, TestContext.Current.CancellationToken);
        }
        using var one = await Apply(first); using var two = await Apply(second); Assert.Equal(HttpStatusCode.Accepted, one.StatusCode); Assert.Equal(HttpStatusCode.Accepted, two.StatusCode);
        var firstId = (await JsonAsync(one)).GetProperty("requestId").GetGuid(); var secondId = (await JsonAsync(two)).GetProperty("requestId").GetGuid();
        var firstDetail = await GetAsync(admin, $"/api/v1/admin/family-relationship-requests/{firstId}"); var secondDetail = await GetAsync(admin, $"/api/v1/admin/family-relationship-requests/{secondId}");
        using var otherAdmin = await f.ClientAsync("root");
        var results = await Task.WhenAll(PostAsync(admin, $"/api/v1/admin/family-relationship-requests/{firstId}/approve", new { rowVersion = firstDetail.GetProperty("request").GetProperty("rowVersion").GetString() }),
            PostAsync(otherAdmin, $"/api/v1/admin/family-relationship-requests/{secondId}/approve", new { rowVersion = secondDetail.GetProperty("request").GetProperty("rowVersion").GetString() }));
        try { Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict); }
        finally { foreach (var result in results) result.Dispose(); }
        await f.WithDbAsync(async db => { Assert.Equal(1, await db.FamilyMembers.CountAsync(m => m.IsActive && m.Role == FamilyMemberRole.Mother)); Assert.Equal(2, await db.Set<ParentChildOperationalAuthority>().CountAsync(p => p.ChildPatientId == child && p.IsActive)); });
        var losingActor = (await GetAsync(first, "/api/v1/families/mine/workspace")).GetProperty("hasFamily").GetBoolean() ? second : first;
        using var occupied = await Apply(losingActor); Assert.Equal(HttpStatusCode.Conflict, occupied.StatusCode);
    }
    internal static MultipartFormDataContent Evidence()
    {
        var form = new MultipartFormDataContent(); var bytes = new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.4\nsynthetic review evidence\n%%EOF")); bytes.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(bytes, "evidenceFiles", "proof.pdf"); form.Add(new StringContent("BirthCertificate"), "documentTypes"); return form;
    }
    internal static async Task<HttpResponseMessage> Submit(HttpClient parent, DateOnly birth, string name)
    {
        using var form = Evidence(); form.Add(new StringContent(name), "nameAr"); form.Add(new StringContent(birth.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)), "dateOfBirth");
        form.Add(new StringContent("Male"), "gender"); form.Add(new StringContent("Father"), "relationToChild"); form.Add(new StringContent("ولي الأمر"), "primaryContactNameAr"); form.Add(new StringContent("01000000000"), "primaryContactPhone");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/families/mine/child-onboarding") { Content = form }; request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        return await parent.SendAsync(request, TestContext.Current.CancellationToken);
    }
    internal static async Task<Guid> Child(Phase13ApiFixture f, HttpClient parent, HttpClient admin, string name)
    {
        using var submitted = await Submit(parent, f.Today.AddYears(-8), name); Assert.Equal(HttpStatusCode.Accepted, submitted.StatusCode); var intake = await JsonAsync(submitted);
        using var approved = await PostAsync(admin, $"/api/v1/admin/child-onboarding-requests/{intake.GetProperty("requestId").GetGuid()}/approve", new { rowVersion = intake.GetProperty("rowVersion").GetString() });
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode); return (await JsonAsync(approved)).GetProperty("approvedPatientId").GetGuid();
    }
    private sealed class Reads : DbCommandInterceptor
    {
        internal bool Enabled; internal int Count;
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        { if (Enabled) Interlocked.Increment(ref Count); return ValueTask.FromResult(result); }
    }
}
