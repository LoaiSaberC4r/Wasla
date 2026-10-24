using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using MediatR;
using Wasla.Application.Features.Clinical;
using Wasla.Application.Media;
using Wasla.Application.Features.Families;
using Wasla.Domain.Families;
using Wasla.Domain.Patients;
using Wasla.Domain.Common;
using Wasla.Domain.Security;
using static Wasla.Tests.Integration.Phase13ApiFixture;
using static Wasla.Tests.Integration.Phase16FamilyFixture;

namespace Wasla.Tests.Integration;

public sealed class Phase16FinalGapTests
{
    private const string Dependents = "/api/v1/families/mine/medical-record-dependents";
    private const string Relationships = "/api/v1/family-relationship-requests";
    private const string ReviewRelationships = "/api/v1/admin/family-relationship-requests";

    [Fact]
    [Trait("Category", "SQLServerConcurrency")]
    public async Task Sql_relationship_approval_rolls_back_proof_when_reviewer_permission_is_removed_during_evidence_open()
    {
        await using var f = await CreateAsync(sql: true);
        using var submitted = await SubmitVerification(f.Father, f, f.ChildId, false);
        Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);
        var request = await JsonAsync(submitted); var id = request.GetProperty("requestId").GetGuid();
        var paused = new PausedMediaReader(f.App.Services.GetRequiredService<IPrivateMediaReader>());
        await using var node = f.App.WithWebHostBuilder(b => b.ConfigureServices(s => {
            s.PostConfigure<FamilyMedicalPolicyOptions>(ConfigurePolicy); s.AddSingleton<IPrivateMediaReader>(paused);
        }));
        using var admin = node.CreateClient(); admin.DefaultRequestHeaders.Authorization = f.Admin.DefaultRequestHeaders.Authorization;
        var approval = PostAsync(admin, ReviewRelationships + $"/{id}/approve", new { rowVersion = request.GetProperty("rowVersion").GetString() });
        try
        {
            await paused.Opened.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            await f.App.WithDbAsync(async db => {
                db.RolePermissions.Remove(await db.RolePermissions.SingleAsync(r => r.RoleId == SystemRoleIds.SuperAdmin &&
                    r.PermissionId == SystemPermissionIds.For(PermissionNames.FamilyRelationshipRequestsApprove)));
                await db.SaveChangesAsync();
            });
        }
        finally { paused.Release.TrySetResult(true); }
        using var denied = await approval; Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        await f.App.WithDbAsync(async db => {
            Assert.Equal(FamilyRelationshipRequestStatus.Pending, (await db.FamilyRelationshipRequests.SingleAsync(r => r.Id == id)).Status);
            Assert.Empty(await db.Set<VerifiedParentChildRelationship>().Where(p => p.ApprovedRelationshipRequestId == id).ToArrayAsync());
            Assert.Single(await db.Set<VerifiedParentChildRelationship>().Where(p => p.ParentPatientId == f.App.PatientId &&
                p.ChildPatientId == f.ChildId && p.Status == ParentChildVerificationStatus.Active).ToArrayAsync());
        });
    }

    [Theory]
    [InlineData(FamilyMemberRole.Guardian)]
    [InlineData(FamilyMemberRole.LegalGuardian)]
    public async Task Guardian_verification_operation_preserves_membership_and_still_requires_separate_medical_review(FamilyMemberRole role)
    {
        await using var f = await CreateAsync(approve: false, adultRole: role);
        using var submitted = await SubmitVerification(f.Father, f, f.ChildId, false, role: role);
        Assert.True(submitted.IsSuccessStatusCode, await submitted.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var request = await JsonAsync(submitted); var source = request.GetProperty("requestId").GetGuid();
        using var approved = await PostAsync(f.Admin, ReviewRelationships + $"/{source}/approve", new { rowVersion = request.GetProperty("rowVersion").GetString() });
        Assert.True(approved.IsSuccessStatusCode, await approved.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        using (var denied = await f.Father.GetAsync(f.Record() + "/overview", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        var review = (await GetAsync(f.Admin, Eligibility + "/reviews")).GetProperty("items").EnumerateArray().Single(r => r.GetProperty("childPatientId").GetGuid() == f.ChildId);
        await Phase15DiagnosticApiTests.OkAsync(await PostAsync(f.Admin, Eligibility + $"/{review.GetProperty("reviewId").GetGuid()}/approve", new {
            rowVersion = review.GetProperty("rowVersion").GetString(), policyVersion = "TEST-ONLY-v1", basisReference = "TEST-reviewed-proof", authorityRelationshipRequestId = source }));
        await GetAsync(f.Father, f.Record() + "/overview");
        await f.App.WithDbAsync(async db => {
            Assert.Equal(4, await db.FamilyMembers.CountAsync());
            Assert.Empty(await db.Set<VerifiedParentChildRelationship>().Where(p => p.ParentPatientId == f.App.PatientId).ToArrayAsync());
        });
    }

    [Theory]
    [InlineData(FamilyMemberRole.Father)]
    [InlineData(FamilyMemberRole.Guardian)]
    [Trait("Category", "SQLServerConcurrency")]
    public async Task Sql_second_host_rechecks_after_media_open_when_restriction_or_guardian_revocation_commits(FamilyMemberRole role)
    {
        await using var f = await CreateAsync(sql: true, adultRole: role);
        var routes = await Phase16GuardianActivationTests.SeedRecordAsync(f);
        var paused = new PausedMediaReader(f.App.Services.GetRequiredService<IPrivateMediaReader>());
        await using var node = f.App.WithWebHostBuilder(b => b.ConfigureServices(s => {
            s.PostConfigure<FamilyMedicalPolicyOptions>(ConfigurePolicy); s.PostConfigure<MedicalRecordOptions>(o => o.GuardianSharingEnabled = true);
            s.AddSingleton<IPrivateMediaReader>(paused);
        }));
        using var adult = node.CreateClient(); adult.DefaultRequestHeaders.Authorization = f.Father.DefaultRequestHeaders.Authorization;
        var transfer = adult.GetAsync(routes[^1], TestContext.Current.CancellationToken);
        try
        {
            await paused.Opened.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            if (role == FamilyMemberRole.Father)
            {
                using var pending = await SubmitAsync(f.Mother, f.ChildId, f.App.PatientId);
                Assert.Equal(HttpStatusCode.Created, pending.StatusCode);
                var request = await JsonAsync(pending);
                await f.DecideAsync(request.GetProperty("requestId").GetGuid(), "approve", request.GetProperty("rowVersion").GetString()!);
            }
            else
            {
                var review = (await GetAsync(f.Admin, Eligibility + "/reviews")).GetProperty("items").EnumerateArray().Single(r =>
                    r.GetProperty("adultPatientId").GetGuid() == f.App.PatientId && r.GetProperty("childPatientId").GetGuid() == f.ChildId);
                await f.DecideEligibilityAsync(review.GetProperty("reviewId").GetGuid(), "revoke", review.GetProperty("rowVersion").GetString()!);
            }
        }
        finally { paused.Release.TrySetResult(true); }
        using var denied = await transfer;
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.DoesNotContain("%PDF", await denied.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        using var nextRead = await adult.GetAsync(f.Record() + "/overview", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, nextRead.StatusCode);
    }

    private sealed class PausedMediaReader(IPrivateMediaReader inner) : IPrivateMediaReader
    {
        internal TaskCompletionSource<bool> Opened { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<PrivateMedia?> OpenAsync(string key, CancellationToken cancellationToken = default)
        {
            var file = await inner.OpenAsync(key, cancellationToken);
            if (file is null) return null;
            Opened.TrySetResult(true);
            try { await Release.Task.WaitAsync(cancellationToken); return file; }
            catch { await file.Content.DisposeAsync(); throw; }
        }
    }

    [Theory]
    [InlineData(FamilyMemberRole.Father, false)]
    [InlineData(FamilyMemberRole.Mother, false)]
    [InlineData(FamilyMemberRole.Father, true)]
    [InlineData(FamilyMemberRole.Mother, true)]
    public async Task Initial_family_and_direct_member_addition_atomically_record_exact_parent_proof(FamilyMemberRole role, bool sql)
    {
        await using var app = await Phase15DiagnosticApiTests.CreateAsync(sql);
        ConfigurePolicy(app.Services.GetRequiredService<IOptions<FamilyMedicalPolicyOptions>>().Value);
        app.Services.GetRequiredService<IOptions<MedicalRecordOptions>>().Value.GuardianSharingEnabled = true;
        var children = new[] { Guid.NewGuid(), Guid.NewGuid() };
        await app.WithDbAsync(async db => {
            foreach (var child in children) db.Add(Patient.Create(child, "TEST طفل", "TEST child", app.Today.AddYears(-8), Gender.Male, null, null, null, null, null, app.Today).Value);
            await db.SaveChangesAsync();
        });
        using var adult = await app.ClientAsync("patient"); using var admin = await app.ClientAsync("admin");
        Guid? familyId = null;
        foreach (var child in children)
        {
            var form = new MultipartFormDataContent();
            form.Add(new StringContent(familyId is null ? "CreateFamily" : "AddFamilyMember"), "requestType");
            if (familyId.HasValue) form.Add(new StringContent(familyId.Value.ToString()), "familyId");
            form.Add(new StringContent(child.ToString()), "targetPatientId"); form.Add(new StringContent(role.ToString()), "requesterClaimedRole");
            form.Add(new StringContent("Child"), "targetClaimedRole");
            var file = new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.4\nTEST-ONLY explicit parent proof\n%%EOF")); file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            form.Add(file, "evidenceFiles", "proof.pdf"); form.Add(new StringContent("BirthCertificate"), "documentTypes");
            using var submitted = await adult.PostAsync(Relationships, form, TestContext.Current.CancellationToken);
            Assert.True(submitted.IsSuccessStatusCode, await submitted.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            var request = await JsonAsync(submitted); var id = request.GetProperty("requestId").GetGuid();
            using var approved = await PostAsync(admin, ReviewRelationships + $"/{id}/approve", new { rowVersion = request.GetProperty("rowVersion").GetString() });
            Assert.True(approved.IsSuccessStatusCode, (familyId.HasValue ? "AddFamilyMember: " : "CreateFamily: ") + await approved.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            familyId = (await JsonAsync(approved)).GetProperty("familyId").GetGuid();
            await GetAsync(adult, $"/api/v1/families/mine/dependents/{child}/medical-record/overview");
        }
        await app.WithDbAsync(async db => {
            Assert.Equal(2, await db.Set<VerifiedParentChildRelationship>().CountAsync(p => p.FamilyId == familyId));
            Assert.Equal(3, await db.FamilyMembers.CountAsync()); Assert.Empty(await db.Set<FamilyMedicalEligibilityReview>().ToArrayAsync());
        });
    }

    [Fact]
    public async Task Sql_two_hosts_serialize_verification_approval_and_invalidate_old_cursors()
    {
        await using var f = await CreateAsync(sql: true);
        await Phase16GuardianActivationTests.SeedRecordAsync(f);
        var cursor = (await GetAsync(f.Father, f.Record() + "/timeline?pageSize=1")).GetProperty("nextCursor").GetString();
        Assert.NotNull(cursor);
        await using var node = f.App.WithWebHostBuilder(b => b.ConfigureServices(s => {
            s.PostConfigure<FamilyMedicalPolicyOptions>(ConfigurePolicy); s.PostConfigure<MedicalRecordOptions>(o => o.GuardianSharingEnabled = true);
        }));
        using var adult = node.CreateClient(); adult.DefaultRequestHeaders.Authorization = f.Father.DefaultRequestHeaders.Authorization;
        using var admin = node.CreateClient(); admin.DefaultRequestHeaders.Authorization = f.Admin.DefaultRequestHeaders.Authorization;
        var submissions = await Task.WhenAll(SubmitVerification(f.Father, f, f.ChildId, false), SubmitVerification(adult, f, f.ChildId, false));
        Assert.Single(submissions, r => r.StatusCode == HttpStatusCode.Created); Assert.Single(submissions, r => r.StatusCode == HttpStatusCode.Conflict);
        var request = await JsonAsync(submissions.Single(r => r.StatusCode == HttpStatusCode.Created)); foreach (var response in submissions) response.Dispose();
        var id = request.GetProperty("requestId").GetGuid(); var row = new { rowVersion = request.GetProperty("rowVersion").GetString() };
        var approvals = await Task.WhenAll(PostAsync(f.Admin, ReviewRelationships + $"/{id}/approve", row), PostAsync(admin, ReviewRelationships + $"/{id}/approve", row));
        Assert.Single(approvals, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(approvals, r => r.StatusCode == HttpStatusCode.Conflict);
        foreach (var response in approvals) response.Dispose();
        using var oldPage = await adult.GetAsync(f.Record() + "/timeline?pageSize=1&cursor=" + Uri.EscapeDataString(cursor), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, oldPage.StatusCode);
        await f.App.WithDbAsync(async db => {
            var proofs = await db.Set<VerifiedParentChildRelationship>().Where(p => p.ParentPatientId == f.App.PatientId && p.ChildPatientId == f.ChildId).ToArrayAsync();
            Assert.Single(proofs, p => p.Status == ParentChildVerificationStatus.Active);
            Assert.Single(proofs, p => p.Status == ParentChildVerificationStatus.Revoked);
            Assert.All(proofs, p => Assert.Equal(8, p.RowVersion.Length));
            var active = proofs.Single(p => p.Status == ParentChildVerificationStatus.Active);
            var family = await db.Families.Include(fam => fam.Members).SingleAsync();
            var duplicate = VerifiedParentChildRelationship.Verify(family.Members.Single(m => m.PatientId == f.App.PatientId),
                family.Members.Single(m => m.PatientId == f.ChildId), await db.FamilyRelationshipRequests.SingleAsync(r => r.Id == active.ApprovedRelationshipRequestId), "TEST-ONLY-v1");
            db.Add(duplicate);
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
        });
    }

    [Fact]
    public async Task Operator_reconciliation_is_authenticated_repeatable_and_never_resurrects_revoked_proof()
    {
        await using var f = await CreateAsync(verifyParents: false);
        using (var denied = await f.Father.GetAsync(f.Record() + "/overview", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        async Task<FamilyProofReconciliationReport> Run(bool apply)
        {
            await using var scope = f.App.Services.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ReconcileFamilyRelationshipProofsCommand("admin", "Phase13TestPass123!", apply), TestContext.Current.CancellationToken);
            Assert.True(result.IsSuccess); return result.Value;
        }
        await using (var scope = f.App.Services.CreateAsyncScope())
        {
            var denied = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ReconcileFamilyRelationshipProofsCommand("patient", "Phase13TestPass123!", true), TestContext.Current.CancellationToken);
            Assert.True(denied.IsFailure);
        }
        var preview = await Run(false); Assert.Equal(4, preview.TotalCandidatePairs); Assert.Equal(4, preview.SafelyVerifiedPairs); Assert.False(preview.Applied);
        await f.App.WithDbAsync(async db => Assert.Empty(await db.Set<VerifiedParentChildRelationship>().ToArrayAsync()));
        var applied = await Run(true); Assert.Equal(4, applied.SafelyVerifiedPairs); Assert.True(applied.Applied);
        var repeated = await Run(true); Assert.Equal(4, repeated.AlreadyVerifiedPairs); Assert.Equal(0, repeated.SafelyVerifiedPairs);
        await f.App.WithDbAsync(async db => { (await db.Set<VerifiedParentChildRelationship>().SingleAsync(p => p.ParentPatientId == f.App.PatientId && p.ChildPatientId == f.ChildId)).Revoke(f.App.Clock.UtcNow); await db.SaveChangesAsync(); });
        Assert.Equal(4, (await Run(true)).AlreadyVerifiedPairs);
        using var stillDenied = await f.Father.GetAsync(f.Record() + "/overview", TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.NotFound, stillDenied.StatusCode);
    }

    [Theory]
    [InlineData(FamilyMemberRole.Father)]
    [InlineData(FamilyMemberRole.Mother)]
    public async Task Parents_read_hidden_patient_safe_outputs_without_eligibility_and_ignore_legacy_review_states(FamilyMemberRole role)
    {
        await using var f = await CreateAsync(adultRole: role);
        var routes = await Phase16GuardianActivationTests.SeedRecordAsync(f);
        await f.App.WithDbAsync(async db => Assert.Empty(await db.Set<FamilyMedicalEligibilityReview>().ToArrayAsync()));
        await AssertAllowed(f, routes);
        Guid legacy = default;
        await f.App.WithDbAsync(async db => {
            var adult = await db.FamilyMembers.SingleAsync(m => m.PatientId == f.App.PatientId);
            var child = await db.FamilyMembers.SingleAsync(m => m.PatientId == f.ChildId);
            var r = FamilyMedicalEligibilityReview.Pending(adult, child, f.App.Clock.UtcNow); legacy = r.Id;
            db.Add(r); db.Add(FamilyMedicalEligibilityReviewHistory.Create(r.Id, "TEST-ONLY-Legacy", null, 1, null, f.App.Clock.UtcNow));
            await db.SaveChangesAsync();
        });
        foreach (var state in Enum.GetValues<FamilyMedicalEligibilityStatus>())
        {
            await f.App.WithDbAsync(async db => { db.Entry(await db.Set<FamilyMedicalEligibilityReview>().SingleAsync(r => r.Id == legacy)).Property(r => r.Status).CurrentValue = state; await db.SaveChangesAsync(); });
            await AssertAllowed(f, routes);
        }
        Assert.Empty((await GetAsync(f.Admin, Eligibility + "/reviews")).GetProperty("items").EnumerateArray());
        await GetAsync(f.Admin, Eligibility + $"/{legacy}"); // Historical detail/history remain available.
        var oldCursor = (await GetAsync(f.Father, f.Record() + "/timeline?pageSize=1")).GetProperty("nextCursor").GetString();
        Assert.NotNull(oldCursor);
        f.App.Services.GetRequiredService<IOptions<FamilyMedicalPolicyOptions>>().Value.PolicyVersion = "TEST-ONLY-v2";
        await AssertAllowed(f, routes); // A configured global version does not add a separate parental medical review.
        using (var stale = await f.Father.GetAsync(f.Record() + "/timeline?pageSize=1&cursor=" + Uri.EscapeDataString(oldCursor), TestContext.Current.CancellationToken))
            Assert.Equal(HttpStatusCode.UnprocessableEntity, stale.StatusCode);
        Guid proofSource = default, reviewer = default;
        await f.App.WithDbAsync(async db => {
            var proof = await db.Set<VerifiedParentChildRelationship>().SingleAsync(p => p.ParentPatientId == f.App.PatientId && p.ChildPatientId == f.ChildId);
            proofSource = proof.ApprovedRelationshipRequestId; reviewer = proof.VerifiedBySuperAdminApplicationUserId;
            db.Entry(await db.FamilyRelationshipRequests.SingleAsync(r => r.Id == proofSource)).Property(r => r.ReviewedBySuperAdminApplicationUserId).CurrentValue = f.App.DoctorUserId;
            await db.SaveChangesAsync();
        });
        await AssertDenied(f.Father, routes); // An approval field alone cannot fabricate SuperAdmin provenance.
        await f.App.WithDbAsync(async db => { db.Entry(await db.FamilyRelationshipRequests.SingleAsync(r => r.Id == proofSource)).Property(r => r.ReviewedBySuperAdminApplicationUserId).CurrentValue = reviewer; await db.SaveChangesAsync(); });
        await AssertAllowed(f, routes);
        await f.App.WithDbAsync(async db => {
            (await db.Set<VerifiedParentChildRelationship>().SingleAsync(p => p.ParentPatientId == f.App.PatientId && p.ChildPatientId == f.ChildId)).Revoke(f.App.Clock.UtcNow);
            await db.SaveChangesAsync();
            Assert.Single(await db.Set<FamilyMedicalEligibilityReviewHistory>().ToArrayAsync());
        });
        await AssertDenied(f.Father, routes);
        Assert.DoesNotContain((await GetAsync(f.Father, Dependents)).GetProperty("items").EnumerateArray(), p => p.GetProperty("patientId").GetGuid() == f.ChildId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Existing_verification_routes_review_exact_pair_without_adding_members_and_preserve_revoked_proof(bool assisted)
    {
        await using var f = await Phase16FamilyFixture.CreateAsync();
        await f.App.WithDbAsync(async db => { (await db.Set<VerifiedParentChildRelationship>().SingleAsync(p => p.ParentPatientId == f.App.PatientId && p.ChildPatientId == f.ChildId)).Revoke(f.App.Clock.UtcNow); await db.SaveChangesAsync(); });
        using var reception = await f.App.ClientAsync("reception");
        var submitter = assisted ? reception : f.Father;
        using var submitted = await SubmitVerification(submitter, f, f.ChildId, assisted);
        Assert.True(submitted.IsSuccessStatusCode, await submitted.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var request = await JsonAsync(submitted); var id = request.GetProperty("requestId").GetGuid();
        using (var duplicate = await SubmitVerification(submitter, f, f.ChildId, assisted)) Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        using (var denied = await f.Father.GetAsync(f.Record() + "/overview", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        using var modification = await PostAsync(f.Admin, ReviewRelationships + $"/{id}/request-modification", new { rowVersion = request.GetProperty("rowVersion").GetString(), message = "TEST-ONLY fresh proof needed" });
        Assert.True(modification.IsSuccessStatusCode);
        request = await JsonAsync(modification);
        using var resubmitted = await SubmitVerification(submitter, f, f.ChildId, assisted, id, request.GetProperty("rowVersion").GetString());
        Assert.True(resubmitted.IsSuccessStatusCode, await resubmitted.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        request = await JsonAsync(resubmitted);
        Assert.Equal(2, request.GetProperty("currentRevisionNumber").GetInt32());
        var row = new { rowVersion = request.GetProperty("rowVersion").GetString() };
        using var approved = await PostAsync(f.Admin, ReviewRelationships + $"/{id}/approve", row);
        Assert.True(approved.IsSuccessStatusCode, await approved.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        using (var stale = await PostAsync(f.Admin, ReviewRelationships + $"/{id}/approve", row)) Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        await GetAsync(f.Father, f.Record() + "/overview");
        await f.App.WithDbAsync(async db => {
            Assert.Equal(4, await db.FamilyMembers.CountAsync());
            var proofs = await db.Set<VerifiedParentChildRelationship>().Where(p => p.ParentPatientId == f.App.PatientId && p.ChildPatientId == f.ChildId).ToArrayAsync();
            Assert.Equal(2, proofs.Length); Assert.Single(proofs, p => p.Status == ParentChildVerificationStatus.Active && p.ApprovedRelationshipRequestId == id);
            Assert.Single(await db.FamilyRelationshipRequestHistories.Where(h => h.RequestId == id && h.Action == FamilyRelationshipRequestAction.Approved).ToArrayAsync());
        });
        using var unrelated = await SubmitVerification(submitter, f, Guid.NewGuid(), assisted);
        Assert.False(unrelated.IsSuccessStatusCode);
    }

    [Theory]
    [InlineData(FamilyMemberRole.Guardian)]
    [InlineData(FamilyMemberRole.LegalGuardian)]
    public async Task Guardian_approval_rejects_other_child_proof_and_requires_explicit_audited_reverification(FamilyMemberRole role)
    {
        await using var f = await CreateAsync(approve: false, adultRole: role);
        var reviews = (await GetAsync(f.Admin, Eligibility + "/reviews")).GetProperty("items").EnumerateArray().ToArray();
        var target = reviews.Single(r => r.GetProperty("childPatientId").GetGuid() == f.ChildId);
        var other = reviews.Single(r => r.GetProperty("childPatientId").GetGuid() == f.SecondChildId);
        var id = target.GetProperty("reviewId").GetGuid();
        using var wrong = await PostAsync(f.Admin, Eligibility + $"/{id}/approve", new {
            rowVersion = target.GetProperty("rowVersion").GetString(), policyVersion = "TEST-ONLY-v1", basisReference = "TEST-reviewed-proof",
            authorityRelationshipRequestId = await f.AuthorityAsync(other.GetProperty("reviewId").GetGuid()) });
        Assert.Equal(HttpStatusCode.NotFound, wrong.StatusCode);
        var approved = await f.DecideEligibilityAsync(id, "approve", target.GetProperty("rowVersion").GetString()!);
        await GetAsync(f.Father, f.Record() + "/overview");
        // A legacy approval lacking exact provenance is unavailable until explicit re-review.
        await f.App.WithDbAsync(async db => { db.Entry(await db.Set<FamilyMedicalEligibilityReview>().SingleAsync(r => r.Id == id)).Property(r => r.AuthorityRelationshipRequestId).CurrentValue = null; await db.SaveChangesAsync(); });
        using (var denied = await f.Father.GetAsync(f.Record() + "/overview", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        var detail = await GetAsync(f.Admin, Eligibility + $"/{id}");
        await f.DecideEligibilityAsync(id, "approve", detail.GetProperty("rowVersion").GetString()!);
        await GetAsync(f.Father, f.Record() + "/overview");
        var originalAuthority = await f.AuthorityAsync(id);
        await f.App.WithDbAsync(async db => {
            var decisions = await db.Set<FamilyMedicalEligibilityReviewHistory>().Where(h => h.ParentId == id && h.Action == "eligibility-approve").ToArrayAsync();
            Assert.Equal(2, decisions.Length);
            Assert.All(decisions, h => { Assert.Contains(originalAuthority.ToString("D"), h.ReasonOrMessage); Assert.Contains("PolicyVersion=TEST-ONLY-v1", h.ReasonOrMessage); });
        });
        using var childB = await f.Father.GetAsync(f.Record(f.SecondChildId) + "/overview", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, childB.StatusCode);
        await f.DecideEligibilityAsync(other.GetProperty("reviewId").GetGuid(), "reject", other.GetProperty("rowVersion").GetString()!);
        using (var rejected = await f.Father.GetAsync(f.Record(f.SecondChildId) + "/overview", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.NotFound, rejected.StatusCode);
        detail = await GetAsync(f.Admin, Eligibility + $"/{id}");
        var revoked = await f.DecideEligibilityAsync(id, "revoke", detail.GetProperty("rowVersion").GetString()!);
        using (var denied = await f.Father.GetAsync(f.Record() + "/overview", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        await f.DecideEligibilityAsync(id, "approve", revoked.GetProperty("rowVersion").GetString()!);
        await GetAsync(f.Father, f.Record() + "/overview");
    }

    [Fact]
    public async Task Membership_only_and_adult_to_adult_proof_do_not_grant_parent_access_and_dispute_alone_cannot_be_approved()
    {
        await using var f = await Phase16FamilyFixture.CreateAsync();
        await f.App.WithDbAsync(async db => {
            foreach (var proof in await db.Set<VerifiedParentChildRelationship>().Where(p => p.ParentPatientId == f.App.OtherPatientId).ToArrayAsync()) proof.Revoke(f.App.Clock.UtcNow);
            await db.SaveChangesAsync();
        });
        using (var denied = await f.Mother.GetAsync(f.Record() + "/overview", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        using var submitted = await SubmitAsync(f.Mother, f.ChildId, f.App.PatientId, file: false, reason: "TEST-ONLY ordinary dispute");
        Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);
        var request = await JsonAsync(submitted);
        await GetAsync(f.Father, f.Record() + "/overview");
        using var rejectApproval = await PostAsync(f.Admin, Inbox + $"/{request.GetProperty("requestId").GetGuid()}/approve", new {
            rowVersion = request.GetProperty("rowVersion").GetString(), basisReference = "TEST-ONLY-unapproved-dispute" });
        Assert.Equal(HttpStatusCode.NotFound, rejectApproval.StatusCode);
        await f.App.WithDbAsync(async db => Assert.Empty(await db.Set<FamilyMedicalAccessRestriction>().ToArrayAsync()));
    }

    private static async Task AssertAllowed(Phase16FamilyFixture f, string[] routes)
    {
        foreach (var route in routes) { using var r = await f.Father.GetAsync(route, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.OK, r.StatusCode); }
        Assert.True((await GetAsync(f.Father, f.Status())).GetProperty("canView").GetBoolean());
        Assert.Contains((await GetAsync(f.Father, Dependents)).GetProperty("items").EnumerateArray(), p => p.GetProperty("patientId").GetGuid() == f.ChildId);
    }
    private static async Task AssertDenied(HttpClient client, string[] routes)
    { foreach (var route in routes) { using var r = await client.GetAsync(route, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.NotFound, r.StatusCode); } }
    private static Task<HttpResponseMessage> SubmitVerification(HttpClient client, Phase16FamilyFixture f, Guid child, bool assisted, Guid? requestId = null, string? rowVersion = null,
        FamilyMemberRole role = FamilyMemberRole.Father)
    {
        var form = new MultipartFormDataContent();
        var guardian = role is FamilyMemberRole.Guardian or FamilyMemberRole.LegalGuardian;
        form.Add(new StringContent(guardian ? "VerifyGuardianChild" : "VerifyParentChild"), "requestType"); form.Add(new StringContent(f.FamilyId.ToString()), "familyId");
        form.Add(new StringContent(f.App.PatientId.ToString()), "requesterPatientId"); form.Add(new StringContent(child.ToString()), "targetPatientId");
        form.Add(new StringContent(role.ToString()), "requesterClaimedRole"); form.Add(new StringContent("Child"), "targetClaimedRole");
        var file = new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.4\nTEST-ONLY pair evidence\n%%EOF")); file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "evidenceFiles", "pair-proof.pdf"); form.Add(new StringContent(guardian ? "GuardianshipDocument" : "BirthCertificate"), "documentTypes");
        if (rowVersion is not null) form.Add(new StringContent(rowVersion), "rowVersion");
        return client.PostAsync(Relationships + (assisted ? "/assisted" : "") + (requestId is { } id ? $"/{id}/resubmit" : ""), form, TestContext.Current.CancellationToken);
    }
}
