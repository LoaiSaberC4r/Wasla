using System.Net;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Wasla.Domain.Patients;
using Wasla.Domain.Common;
using Wasla.Domain.Security;
using static Wasla.Tests.Integration.Phase13ApiFixture;

namespace Wasla.Tests.Integration;

public sealed class Phase5FinalBoundaryTests
{
    [Fact, Trait("Category", "SQLServerConcurrency")]
    public async Task Full_creation_racing_verified_email_change_has_one_identity_and_no_partial_account()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(true);
        using var patient = await app.ClientAsync("patient"); using var staff = await app.ClientAsync("reception");
        await app.WithDbAsync(async db =>
        {
            var p = await db.Patients.SingleAsync(p => p.Id == app.PatientId);
            Assert.True(p.UpdateProfile(p.NameAr, p.NameEn, "01000000001", "patient@example.test").IsSuccess);
            await db.SaveChangesAsync();
        });
        const string email = "identity-race@example.test";
        var proof = await Phase5IdentityApi.VerifyAsync(app, patient, email, "EmailChange");
        var profile = await GetAsync(patient, "/api/v1/patients/me");
        using var form = new MultipartFormDataContent();
        foreach (var (key, value) in new Dictionary<string, string>
        {
            ["nameAr"] = profile.GetProperty("nameAr").GetString()!, ["phoneNumber"] = "01000000001", ["email"] = email,
            ["rowVersion"] = profile.GetProperty("rowVersion").GetString()!, ["emailVerificationToken"] = proof
        }) form.Add(new StringContent(value), key);
        var fields = Phase5IdentityApi.Profile(app, email, true); fields["phoneNumber"] = "01000000009";
        var results = await Task.WhenAll(patient.PutAsync("/api/v1/patients/me", form, TestContext.Current.CancellationToken),
            Phase5IdentityApi.FormAsync(staff, "/api/v1/patients/full", fields));
        try
        {
            Assert.Single(results, r => r.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created);
            Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict);
            var fullWon = results[1].StatusCode == HttpStatusCode.Created;
            await app.WithDbAsync(async db =>
            {
                Assert.Equal(1, await db.Patients.CountAsync(p => p.Email == email));
                Assert.Equal(1, await db.ApplicationUsers.CountAsync(u => u.Email == email));
                Assert.Equal(fullWon ? 3 : 2, await db.PatientAccountLinks.CountAsync());
                Assert.Equal(fullWon ? 1 : 0, await db.PatientActivationInvitations.CountAsync());
                var originalPatient = await db.Patients.SingleAsync(p => p.Id == app.PatientId);
                var originalUser = await db.ApplicationUsers.SingleAsync(u => u.UserName == "patient");
                Assert.Equal(fullWon ? "patient@example.test" : email, originalPatient.Email);
                Assert.Equal(originalPatient.Email, originalUser.Email);
                Assert.False(await db.ApplicationUsers.AnyAsync(u => u.UserType == UserType.Patient &&
                    !db.PatientAccountLinks.Any(l => l.ApplicationUserId == u.Id)));
            });
        }
        finally { foreach (var result in results) result.Dispose(); }
    }

    [Fact]
    public async Task Otp_cooldown_expiry_and_continuation_expiry_use_server_boundaries()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(); using var client = app.CreateClient();
        const string email = "otp-boundary@example.test";
        async Task<Guid> Start()
        {
            using var response = await client.PostAsJsonAsync("/api/v1/patient-onboarding/start", new { email, purpose = "Register" }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode); return (await JsonAsync(response)).GetProperty("requestId").GetGuid();
        }
        var first = await Start(); var cooldown = await Start(); Assert.NotEqual(first, cooldown);
        await app.WithDbAsync(async db => { Assert.Equal(1, await db.PatientOnboardingChallenges.CountAsync()); Assert.Equal(1, await db.EmailOutboxMessages.CountAsync(m => m.RecipientEmail == email)); });
        var code = await Code(app, first); app.Clock.UtcNow = app.Clock.UtcNow.AddMinutes(10);
        using var expired = await client.PostAsJsonAsync("/api/v1/patient-onboarding/verify-email", new { requestId = first, otp = code }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, expired.StatusCode);
        var second = await Start(); code = await Code(app, second); app.Clock.UtcNow = app.Clock.UtcNow.AddMinutes(10).AddTicks(-1);
        using var verified = await client.PostAsJsonAsync("/api/v1/patient-onboarding/verify-email", new { requestId = second, otp = code }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode); var body = await JsonAsync(verified);
        app.Clock.UtcNow = body.GetProperty("expiresOnUtc").GetDateTime();
        var fields = Phase5IdentityApi.Profile(app, email); fields["userName"] = "otp-boundary"; fields["password"] = fields["confirmPassword"] = "SyntheticPass123!"; fields["emailVerificationToken"] = body.GetProperty("verificationToken").GetString()!;
        using var rejected = await Phase5IdentityApi.FormAsync(client, "/api/v1/patient-onboarding/register", fields); Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        await app.WithDbAsync(async db => Assert.False(await db.ApplicationUsers.AnyAsync(u => u.Email == email)));
    }

    [Fact]
    public async Task Minor_claim_requires_basic_and_manual_review_and_claim_attestation_is_email_and_expiry_bound()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(); using var staff = await app.ClientAsync("reception"); using var applicant = app.CreateClient(); using var admin = await app.ClientAsync("root");
        var dob = app.Today.AddYears(-17).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var direct = Phase5IdentityApi.Profile(app, "minor-direct@example.test", true); direct["dateOfBirth"] = dob;
        using var full = await Phase5IdentityApi.FormAsync(staff, "/api/v1/patients/full", direct); Assert.Equal(HttpStatusCode.UnprocessableEntity, full.StatusCode);
        direct["userName"] = "minor-direct"; direct["password"] = direct["confirmPassword"] = "SyntheticPass123!"; direct["emailVerificationToken"] = await Phase5IdentityApi.VerifyAsync(app, applicant, direct["email"], "Register");
        using var self = await Phase5IdentityApi.FormAsync(applicant, "/api/v1/patient-onboarding/register", direct); Assert.Equal(HttpStatusCode.UnprocessableEntity, self.StatusCode);
        var fields = Phase5IdentityApi.Profile(app, "minor-claim@example.test"); fields["dateOfBirth"] = dob;
        using var basic = await Phase5IdentityApi.FormAsync(staff, "/api/v1/patients", fields); Assert.Equal(HttpStatusCode.Created, basic.StatusCode);
        var patientId = (await JsonAsync(basic)).GetProperty("patientId").GetGuid(); fields["emailVerificationToken"] = await Phase5IdentityApi.VerifyAsync(app, applicant, fields["email"], "Claim");
        fields["evidenceType"] = "GovernmentIdFront";
        using var noProof = await Phase5IdentityApi.FormAsync(applicant, "/api/v1/patient-onboarding/claims", fields); Assert.Equal(HttpStatusCode.UnprocessableEntity, noProof.StatusCode);
        fields.Remove("evidenceType");
        using var claim = await Phase5IdentityApi.FormAsync(applicant, "/api/v1/patient-onboarding/claims", fields, evidence: true); Assert.Equal(HttpStatusCode.Created, claim.StatusCode);
        var claimId = (await JsonAsync(claim)).GetProperty("claimRequestId").GetGuid(); var details = await GetAsync(admin, $"/api/v1/admin/patient-claims/{claimId}");
        using var approve = await PostAsync(admin, $"/api/v1/admin/patient-claims/{claimId}/approve", new { targetPatientId = patientId, rowVersion = details.GetProperty("rowVersion").GetString(), reviewedEvidenceIds = new[] { details.GetProperty("evidence")[0].GetProperty("evidenceId").GetGuid() }, reviewNote = "Synthetic official age and identity evidence reviewed" }); Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
        await app.WithDbAsync(async db => Assert.False(await db.PatientAccountLinks.AnyAsync(l => l.PatientId == patientId)));
        var adult = Phase5IdentityApi.Profile(app, "attested@example.test"); adult["phoneNumber"] = "01000000002";
        using var adultBasic = await Phase5IdentityApi.FormAsync(staff, "/api/v1/patients", adult); Assert.Equal(HttpStatusCode.Created, adultBasic.StatusCode);
        var adultId = (await JsonAsync(adultBasic)).GetProperty("patientId").GetGuid();
        using var attested = await PostAsync(staff, "/api/v1/patients/registration-matches", new { mode = "ConfirmClaimIdentity", doctorPracticeId = app.PracticeId, patientId = adultId, claimantEmail = adult["email"], verifiedInPerson = true, verificationBasis = "GovernmentIdSeen", reason = "Synthetic physical identity verified" }); Assert.Equal(HttpStatusCode.OK, attested.StatusCode);
        var attestation = (await JsonAsync(attested)).GetProperty("attestationId").GetGuid(); adult["patientReference"] = adultId.ToString(); adult["inPersonIdentityAttestationId"] = attestation.ToString(); adult["evidenceType"] = "InPersonIdentityAttestation";
        adult["emailVerificationToken"] = await Phase5IdentityApi.VerifyAsync(app, applicant, "wrong-attestation@example.test", "Claim");
        using var wrongEmail = await Phase5IdentityApi.FormAsync(applicant, "/api/v1/patient-onboarding/claims", adult); Assert.Equal(HttpStatusCode.UnprocessableEntity, wrongEmail.StatusCode);
        app.Clock.UtcNow = app.Clock.UtcNow.AddHours(48); adult["emailVerificationToken"] = await Phase5IdentityApi.VerifyAsync(app, applicant, adult["email"], "Claim");
        using var stale = await Phase5IdentityApi.FormAsync(applicant, "/api/v1/patient-onboarding/claims", adult); Assert.Equal(HttpStatusCode.UnprocessableEntity, stale.StatusCode);
        using var renewed = await PostAsync(staff, "/api/v1/patients/registration-matches", new { mode = "ConfirmClaimIdentity", doctorPracticeId = app.PracticeId, patientId = adultId, claimantEmail = adult["email"], verifiedInPerson = true, verificationBasis = "GovernmentIdSeen", reason = "Synthetic physical identity reverified" }); Assert.Equal(HttpStatusCode.OK, renewed.StatusCode);
        adult["inPersonIdentityAttestationId"] = (await JsonAsync(renewed)).GetProperty("attestationId").GetGuid().ToString();
        using var valid = await Phase5IdentityApi.FormAsync(applicant, "/api/v1/patient-onboarding/claims", adult); Assert.Equal(HttpStatusCode.Created, valid.StatusCode);
        await app.WithDbAsync(async db => { Assert.Equal(2, await db.PatientClaimRequests.CountAsync()); Assert.False(await db.PatientAccountLinks.AnyAsync(l => l.PatientId == adultId)); });
    }

    private static async Task<string> Code(Phase13ApiFixture app, Guid id)
    {
        string text = ""; await app.WithDbAsync(async db => text = (await db.EmailOutboxMessages.SingleAsync(m => m.IdempotencyKey == "patient-otp:" + id.ToString("N"))).TextBody!);
        return Regex.Match(text, @"Verification code: (\d+)", RegexOptions.CultureInvariant).Groups[1].Value;
    }
}
