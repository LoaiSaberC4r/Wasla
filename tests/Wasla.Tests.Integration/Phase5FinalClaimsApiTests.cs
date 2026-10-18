using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Wasla.Domain.Patients;
using Wasla.Domain.Security;
using Wasla.Domain.Common;
using static Wasla.Tests.Integration.Phase13ApiFixture;
namespace Wasla.Tests.Integration;
public sealed class Phase5FinalClaimsApiTests
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Verified_basic_claim_review_and_activation_preserve_original_patient(bool sql)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(sql); using var staff = await app.ClientAsync("reception"); using var applicant = app.CreateClient();
        await app.WithDbAsync(async db =>
        {
            var root = await db.ApplicationUsers.SingleAsync(u => u.UserName == "root");
            var reviewer = ApplicationUser.Create(Guid.NewGuid(), "identity-reviewer", "reviewer@example.test", null, root.PasswordHash, UserType.SuperAdmin, false, app.Clock.UtcNow, root.Id).Value;
            db.ApplicationUsers.Add(reviewer); db.SuperAdmins.Add(SuperAdmin.Create(Guid.NewGuid(), reviewer.Id, "مراجع هوية تجريبي", null, false, root.Id).Value); db.UserRoles.Add(new(Guid.NewGuid(), reviewer.Id, SystemRoleIds.SuperAdmin)); await db.SaveChangesAsync();
        });
        using var admin = await app.ClientAsync("identity-reviewer");
        const string email = "claim-basic@example.test";
        using var basic = await Phase5IdentityApi.FormAsync(staff, "/api/v1/patients", Phase5IdentityApi.Profile(app, email)); Assert.Equal(HttpStatusCode.Created, basic.StatusCode);
        var patient = (await JsonAsync(basic)).GetProperty("patientId").GetGuid();
        var proof = await Phase5IdentityApi.VerifyAsync(app, applicant, email, "Register"); // Register routes existing Basic to Claim without a second OTP.
        var fields = Phase5IdentityApi.Profile(app, email); fields["emailVerificationToken"] = proof;
        using (var invalidForm = new MultipartFormDataContent())
        {
            foreach (var (key, value) in fields) invalidForm.Add(new StringContent(value), key);
            invalidForm.Add(new StringContent("GovernmentIdFront"), "evidenceType");
            var invalidImage = new ByteArrayContent(new byte[] { 1, 2, 3, 4 }); invalidImage.Headers.ContentType = new("image/png"); invalidForm.Add(invalidImage, "identityFront", "synthetic.png");
            using var invalidRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/patient-onboarding/claims") { Content = invalidForm }; invalidRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            using var invalid = await applicant.SendAsync(invalidRequest, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            await app.WithDbAsync(async db => Assert.False(await db.PatientClaimRequests.AnyAsync()));
        }
        using var claimResponse = await Phase5IdentityApi.FormAsync(applicant, "/api/v1/patient-onboarding/claims", fields, evidence: true); Assert.Equal(HttpStatusCode.Created, claimResponse.StatusCode);
        var claim = (await JsonAsync(claimResponse)).GetProperty("claimRequestId").GetGuid();
        using var ownedRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/patient-onboarding/claims/{claim}"); ownedRequest.Headers.Add("X-Onboarding-Token", proof);
        using var owned = await applicant.SendAsync(ownedRequest, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.OK, owned.StatusCode); Assert.Equal("PendingReview", (await JsonAsync(owned)).GetProperty("status").GetString());
        var list = await GetAsync(admin, "/api/v1/admin/patient-claims?status=PendingReview&pageSize=20"); Assert.Single(list.GetProperty("items").EnumerateArray());
        using var guessed = await applicant.GetAsync($"/api/v1/patient-onboarding/claims/{claim}", TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.NotFound, guessed.StatusCode);
        var details = await GetAsync(admin, $"/api/v1/admin/patient-claims/{claim}"); var evidence = details.GetProperty("evidence")[0].GetProperty("evidenceId").GetGuid();
        using var document = await admin.GetAsync($"/api/v1/admin/patient-claims/{claim}/evidence/{evidence}", TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.OK, document.StatusCode); Assert.True(document.Headers.CacheControl!.NoStore);
        using var approved = await PostAsync(admin, $"/api/v1/admin/patient-claims/{claim}/approve", new { targetPatientId = patient, rowVersion = details.GetProperty("rowVersion").GetString(), reviewedEvidenceIds = new[] { evidence }, reviewNote = "Synthetic identity evidence reviewed" }); Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        await app.WithDbAsync(async db => Assert.False(await db.PatientAccountLinks.AnyAsync(l => l.PatientId == patient)));
        var invite = await Phase5IdentityApi.InvitationAsync(app, email);
        app.Clock.UtcNow = app.Clock.UtcNow.AddHours(48);
        using var expired = await applicant.PostAsJsonAsync("/api/v1/patient-onboarding/activate", new { invitationToken = invite, password = "SyntheticPass123!", confirmPassword = "SyntheticPass123!" }, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.Conflict, expired.StatusCode);
        var newProof = await Phase5IdentityApi.VerifyAsync(app, applicant, email, "Claim");
        using var resent = await PostAsync(applicant, "/api/v1/patient-onboarding/activation/resend", new { email, claimRequestId = claim, verificationToken = newProof }); Assert.Equal(HttpStatusCode.Accepted, resent.StatusCode);
        var replacement = await Phase5IdentityApi.InvitationAsync(app, email); Assert.NotEqual(invite, replacement); invite = replacement;
        using var activated = await applicant.PostAsJsonAsync("/api/v1/patient-onboarding/activate", new { invitationToken = invite, password = "SyntheticPass123!", confirmPassword = "SyntheticPass123!" }, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.OK, activated.StatusCode);
        Assert.Equal(patient, (await JsonAsync(activated)).GetProperty("patientId").GetGuid());
        Assert.Equal(3, (await GetAsync(admin, $"/api/v1/admin/patient-claims/{claim}/history")).GetProperty("items").GetArrayLength());
        await app.WithDbAsync(async db => { Assert.Equal(PatientClaimStatus.Linked, (await db.PatientClaimRequests.SingleAsync(c => c.Id == claim)).Status); Assert.Equal(PatientAccountLinkSource.PatientClaim, (await db.PatientAccountLinks.SingleAsync(l => l.PatientId == patient)).Source); Assert.Equal(3, await db.PatientClaimHistories.CountAsync(h => h.ClaimRequestId == claim)); });
    }
}
