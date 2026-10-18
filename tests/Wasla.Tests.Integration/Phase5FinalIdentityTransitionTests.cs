using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Wasla.Domain.Common;
using Wasla.Domain.Patients;
using static Wasla.Tests.Integration.Phase13ApiFixture;
namespace Wasla.Tests.Integration;

public sealed class Phase5FinalIdentityTransitionTests
{
    [Fact, Trait("Category", "SQLServerConcurrency")]
    public async Task Unresolved_intakes_require_review_and_concurrent_approvals_and_activation_have_one_winner()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(true);
        using var applicant = app.CreateClient(); using var admin = await app.ClientAsync("root");
        var target = Guid.NewGuid();
        await app.WithDbAsync(async db => { db.Patients.Add(Patient.Create(target, "هوية تجريبية", null, new(1990, 2, 3), Gender.Male, "01000000009", null, null, null, null, app.Today).Value); await db.SaveChangesAsync(); });
        var claims = new List<(Guid Id, string Email, string Version, Guid Evidence)>();
        foreach (var email in new[] { "intake-one@example.test", "intake-two@example.test" })
        {
            var proof = await Phase5IdentityApi.VerifyAsync(app, applicant, email, "Claim");
            var fields = Phase5IdentityApi.Profile(app, email); fields["nameAr"] = "هوية تجريبية"; fields["phoneNumber"] = "01000000009"; fields["emailVerificationToken"] = proof;
            using var submitted = await Phase5IdentityApi.FormAsync(applicant, "/api/v1/patient-onboarding/claims", fields, evidence: true);
            Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);
            var id = (await JsonAsync(submitted)).GetProperty("claimRequestId").GetGuid();
            var details = await GetAsync(admin, $"/api/v1/admin/patient-claims/{id}");
            claims.Add((id, email, details.GetProperty("rowVersion").GetString()!, details.GetProperty("evidence")[0].GetProperty("evidenceId").GetGuid()));
            await app.WithDbAsync(async db => Assert.Null((await db.PatientClaimRequests.SingleAsync(c => c.Id == id)).TargetPatientId));
        }
        var decisions = await Task.WhenAll(claims.Select(c => PostAsync(admin, $"/api/v1/admin/patient-claims/{c.Id}/approve", new { targetPatientId = target, rowVersion = c.Version, reviewedEvidenceIds = new[] { c.Evidence }, reviewNote = "Synthetic proof reviewed" })));
        int winner;
        try { Assert.Single(decisions, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(decisions, r => r.StatusCode == HttpStatusCode.Conflict); winner = Array.FindIndex(decisions, r => r.StatusCode == HttpStatusCode.OK); }
        finally { foreach (var response in decisions) response.Dispose(); }
        var token = await Phase5IdentityApi.InvitationAsync(app, claims[winner].Email);
        var activations = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => applicant.PostAsJsonAsync("/api/v1/patient-onboarding/activate", new { invitationToken = token, password = "SyntheticPass123!", confirmPassword = "SyntheticPass123!" }, TestContext.Current.CancellationToken)));
        try { Assert.Single(activations, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(activations, r => r.StatusCode == HttpStatusCode.Conflict); }
        finally { foreach (var response in activations) response.Dispose(); }
        await app.WithDbAsync(async db => { Assert.Equal(1, await db.PatientAccountLinks.CountAsync(l => l.PatientId == target)); Assert.Equal(1, await db.PatientClaimRequests.CountAsync(c => c.Status == PatientClaimStatus.Linked)); Assert.Equal(3, await db.Patients.CountAsync()); });
    }

    [Fact]
    public async Task Modification_resubmission_versions_evidence_and_rejection_stays_terminal()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(); using var staff = await app.ClientAsync("reception"); using var admin = await app.ClientAsync("root"); using var applicant = app.CreateClient();
        const string email = "revision@example.test";
        using var basic = await Phase5IdentityApi.FormAsync(staff, "/api/v1/patients", Phase5IdentityApi.Profile(app, email)); Assert.Equal(HttpStatusCode.Created, basic.StatusCode);
        var proof = await Phase5IdentityApi.VerifyAsync(app, applicant, email, "Claim"); var fields = Phase5IdentityApi.Profile(app, email); fields["emailVerificationToken"] = proof;
        using var submitted = await Phase5IdentityApi.FormAsync(applicant, "/api/v1/patient-onboarding/claims", fields, evidence: true); Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);
        var id = (await JsonAsync(submitted)).GetProperty("claimRequestId").GetGuid(); var url = $"/api/v1/admin/patient-claims/{id}"; var details = await GetAsync(admin, url);
        using var modify = await PostAsync(admin, url + "/request-modification", new { rowVersion = details.GetProperty("rowVersion").GetString(), reason = "Provide a clearer synthetic identity image" }); Assert.Equal(HttpStatusCode.OK, modify.StatusCode);
        app.Clock.UtcNow = app.Clock.UtcNow.AddSeconds(61); proof = await Phase5IdentityApi.VerifyAsync(app, applicant, email, "Claim");
        fields["emailVerificationToken"] = proof;
        fields["rowVersion"] = (await JsonAsync(modify)).GetProperty("rowVersion").GetString()!; var key = Guid.NewGuid().ToString();
        using var resubmit = await Phase5IdentityApi.FormAsync(applicant, $"/api/v1/patient-onboarding/claims/{id}/resubmit", fields, key, true, proof); Assert.Equal(HttpStatusCode.OK, resubmit.StatusCode); Assert.Equal(2, (await JsonAsync(resubmit)).GetProperty("revisionNumber").GetInt32());
        using var replay = await Phase5IdentityApi.FormAsync(applicant, $"/api/v1/patient-onboarding/claims/{id}/resubmit", fields, key, true, proof); Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        details = await GetAsync(admin, url); Assert.Equal(2, details.GetProperty("evidence").GetArrayLength());
        using var rejected = await PostAsync(admin, url + "/reject", new { rowVersion = details.GetProperty("rowVersion").GetString(), reason = "Synthetic review rejected" }); Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        app.Clock.UtcNow = app.Clock.UtcNow.AddSeconds(61); proof = await Phase5IdentityApi.VerifyAsync(app, applicant, email, "Claim");
        fields["rowVersion"] = (await JsonAsync(rejected)).GetProperty("rowVersion").GetString()!;
        using var terminal = await Phase5IdentityApi.FormAsync(applicant, $"/api/v1/patient-onboarding/claims/{id}/resubmit", fields, evidence: true, onboarding: proof); Assert.Equal(HttpStatusCode.Conflict, terminal.StatusCode);
        await app.WithDbAsync(async db => { Assert.Equal(2, await db.PatientAccountLinks.CountAsync()); Assert.Equal(4, await db.PatientClaimHistories.CountAsync(h => h.ClaimRequestId == id)); });
    }

    [Fact]
    public async Task Verified_email_change_is_atomic_and_conflict_preserves_both_original_emails()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(); using var patient = await app.ClientAsync("patient");
        await app.WithDbAsync(async db => { var p = await db.Patients.SingleAsync(p => p.Id == app.PatientId); Assert.True(p.UpdateProfile(p.NameAr, p.NameEn, "01000000000", "patient@example.test").IsSuccess); db.Patients.Add(Patient.Create(Guid.NewGuid(), "حساب آخر تجريبي", null, new(1990, 1, 1), Gender.Male, "01000000001", "occupied@example.test", null, null, null, app.Today).Value); await db.SaveChangesAsync(); });
        await app.WithDbAsync(async db => { await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [Patients] SET [Email] = {" PATIENT@Example.Test "} WHERE [Id] = {app.PatientId}", TestContext.Current.CancellationToken); });
        var unchanged = await GetAsync(patient, "/api/v1/patients/me");
        using var unchangedForm = new MultipartFormDataContent();
        foreach (var (key, value) in new Dictionary<string, string> { ["nameAr"] = unchanged.GetProperty("nameAr").GetString()!, ["phoneNumber"] = "01000000000", ["email"] = "patient@example.test", ["rowVersion"] = unchanged.GetProperty("rowVersion").GetString()! }) unchangedForm.Add(new StringContent(value), key);
        using var normalizedOnly = await patient.PutAsync("/api/v1/patients/me", unchangedForm, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.OK, normalizedOnly.StatusCode);
        foreach (var destination in new[] { "occupied@example.test", "changed@example.test" })
        {
            var proof = await Phase5IdentityApi.VerifyAsync(app, patient, destination, "EmailChange"); var profile = await GetAsync(patient, "/api/v1/patients/me");
            using var form = new MultipartFormDataContent(); foreach (var (key, value) in new Dictionary<string, string> { ["nameAr"] = profile.GetProperty("nameAr").GetString()!, ["phoneNumber"] = "01000000000", ["email"] = destination, ["rowVersion"] = profile.GetProperty("rowVersion").GetString()!, ["emailVerificationToken"] = proof }) form.Add(new StringContent(value), key);
            using var response = await patient.PutAsync("/api/v1/patients/me", form, TestContext.Current.CancellationToken); Assert.Equal(destination.StartsWith("occupied", StringComparison.Ordinal) ? HttpStatusCode.Conflict : HttpStatusCode.OK, response.StatusCode);
            await app.WithDbAsync(async db => { var expected = destination.StartsWith("occupied", StringComparison.Ordinal) ? "patient@example.test" : destination; Assert.Equal(expected, (await db.Patients.SingleAsync(p => p.Id == app.PatientId)).Email); Assert.Equal(expected, (await db.ApplicationUsers.SingleAsync(u => u.UserName == "patient")).Email); });
        }
    }
}
