using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Wasla.Domain.Patients;
using Wasla.Domain.Security;
using Wasla.Domain.Common;
using static Wasla.Tests.Integration.Phase13ApiFixture;
using BuildingBlock.Application.Abstraction.Encryption;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
namespace Wasla.Tests.Integration;
public sealed class Phase5FinalRegistrationApiTests
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Reception_full_is_inactive_until_one_time_activation_and_idempotent(bool sql)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(sql); using var staff = await app.ClientAsync("reception"); using var anonymous = app.CreateClient();
        var email = "full-new@example.test"; var key = Guid.NewGuid().ToString();
        using var response = await Phase5IdentityApi.FormAsync(staff, "/api/v1/patients/full", Phase5IdentityApi.Profile(app, email, true), key);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); var created = await JsonAsync(response);
        Assert.Equal("PendingActivation", created.GetProperty("accountStatus").GetString());
        var patient = created.GetProperty("patientId").GetGuid(); var user = created.GetProperty("applicationUserId").GetGuid();
        using var replay = await Phase5IdentityApi.FormAsync(staff, "/api/v1/patients/full", Phase5IdentityApi.Profile(app, email, true), key);
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        var changedFields = Phase5IdentityApi.Profile(app, email, true); changedFields["nameAr"] = "بيانات مختلفة تجريبية";
        using var changed = await Phase5IdentityApi.FormAsync(staff, "/api/v1/patients/full", changedFields, key); Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        using var missingPermission = await app.ClientAsync("doctor");
        using var denied = await Phase5IdentityApi.FormAsync(missingPermission, "/api/v1/patients/full", Phase5IdentityApi.Profile(app, "denied@example.test", true)); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var token = await Phase5IdentityApi.InvitationAsync(app, email);
        using var loginBefore = await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { identifier = email, password = "SyntheticPass123!" }, TestContext.Current.CancellationToken); Assert.NotEqual(HttpStatusCode.OK, loginBefore.StatusCode);
        var beforeHash = app.Clock.UtcNow;
        await using (var expiringNode = app.WithWebHostBuilder(b => b.ConfigureServices(s => { s.RemoveAll<IPasswordService>(); s.AddSingleton<IPasswordService>(new ExpiringPasswordService(app.Services.GetRequiredService<IPasswordService>(), app.Clock)); })))
        {
            using var expiring = expiringNode.CreateClient();
            using var expiredDuringHash = await expiring.PostAsJsonAsync("/api/v1/patient-onboarding/activate", new { invitationToken = token, password = "SyntheticPass123!", confirmPassword = "SyntheticPass123!" }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Conflict, expiredDuringHash.StatusCode);
            await app.WithDbAsync(async db => { Assert.False((await db.ApplicationUsers.SingleAsync(u => u.Id == user)).IsActive); Assert.Null((await db.PatientActivationInvitations.SingleAsync(i => i.ApplicationUserId == user)).ConsumedOnUtc); });
        }
        app.Clock.UtcNow = beforeHash;
        using var activate = await anonymous.PostAsJsonAsync("/api/v1/patient-onboarding/activate", new { invitationToken = token, password = "SyntheticPass123!", confirmPassword = "SyntheticPass123!" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, activate.StatusCode); Assert.Equal(patient, (await JsonAsync(activate)).GetProperty("patientId").GetGuid());
        using var used = await anonymous.PostAsJsonAsync("/api/v1/patient-onboarding/activate", new { invitationToken = token, password = "SyntheticPass123!", confirmPassword = "SyntheticPass123!" }, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.Conflict, used.StatusCode);
        using var login = await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { identifier = email, password = "SyntheticPass123!" }, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        await app.WithDbAsync(async db => { Assert.Equal(1, await db.PatientAccountLinks.CountAsync(l => l.PatientId == patient && l.ApplicationUserId == user)); Assert.True((await db.ApplicationUsers.SingleAsync(u => u.Id == user)).IsActive); Assert.Equal(1, await db.PatientActivationInvitations.CountAsync(i => i.ApplicationUserId == user)); });
        await app.WithDbAsync(db => db.ReceptionPracticeAssignmentPermissions.Where(p => p.AssignmentId == app.ReceptionAssignmentId && p.PermissionId == SystemPermissionIds.For(PermissionNames.PatientsRegisterFull)).ExecuteDeleteAsync(TestContext.Current.CancellationToken));
        using var undelegated = await Phase5IdentityApi.FormAsync(staff, "/api/v1/patients/full", Phase5IdentityApi.Profile(app, "undelegated@example.test", true)); Assert.Equal(HttpStatusCode.Forbidden, undelegated.StatusCode);

    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Verified_self_registration_and_legacy_alias_share_proof_and_uniqueness(bool sql)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(sql); using var client = app.CreateClient(); var email = "self-new@example.test";
        await app.WithDbAsync(async db => { db.Patients.Add(Patient.Create(Guid.NewGuid(), "ملف محتمل تجريبي", null, new(1980, 1, 1), Gender.Male, "01000000000", "soft-existing@example.test", null, null, null, app.Today).Value); await db.SaveChangesAsync(); });
        var fields = Phase5IdentityApi.Profile(app, email); fields["userName"] = "self-new"; fields["password"] = fields["confirmPassword"] = "SyntheticPass123!";
        using var legacyUnverified = await Phase5IdentityApi.FormAsync(client, "/api/v1/auth/patients/register", fields); Assert.Equal(HttpStatusCode.Conflict, legacyUnverified.StatusCode);
        fields["emailVerificationToken"] = await Phase5IdentityApi.VerifyAsync(app, client, email, "Register"); var key = Guid.NewGuid().ToString();
        using var created = await Phase5IdentityApi.FormAsync(client, "/api/v1/patient-onboarding/register", fields, key); Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await JsonAsync(created)).GetProperty("patientId").GetGuid();
        using var replay = await Phase5IdentityApi.FormAsync(client, "/api/v1/patient-onboarding/register", fields, key); Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        using var stolen = await Phase5IdentityApi.FormAsync(client, "/api/v1/auth/patients/register", fields); Assert.Equal(HttpStatusCode.Conflict, stolen.StatusCode);
        await app.WithDbAsync(async db => { Assert.Equal(email, (await db.Patients.SingleAsync(p => p.Id == id)).Email); Assert.Equal(1, await db.PatientAccountLinks.CountAsync(l => l.PatientId == id)); Assert.Equal(1, await db.PatientDuplicateCases.CountAsync(c => c.FirstPatientId == id || c.SecondPatientId == id)); });
    }
    private sealed class ExpiringPasswordService(IPasswordService inner, Phase13Clock clock) : IPasswordService
    {
        public string Hash(string password) => inner.Hash(password);
        public bool Verify(string password, string passwordHash) => inner.Verify(password, passwordHash);
        public PasswordVerification VerifyDetailed(string password, string passwordHash) => inner.VerifyDetailed(password, passwordHash);
        public bool IsStrongPassword(string password) => inner.IsStrongPassword(password);
        public Task<bool> VerifyAsync(string password, string passwordHash, CancellationToken cancellationToken = default) => inner.VerifyAsync(password, passwordHash, cancellationToken);
        public async Task<string> HashAsync(string password, CancellationToken cancellationToken = default) { var hash = await inner.HashAsync(password, cancellationToken); clock.UtcNow = clock.UtcNow.AddHours(48); return hash; }
    }
}
internal static class Phase5IdentityApi
{
    internal static Dictionary<string, string> Profile(Phase13ApiFixture app, string email, bool full = false) => new() { ["doctorPracticeId"] = app.PracticeId.ToString(), ["nameAr"] = "هوية تجريبية " + email.Split('@')[0], ["dateOfBirth"] = "1990-02-03", ["gender"] = "Male", ["phoneNumber"] = "01000000000", ["email"] = email, ["verifiedInPerson"] = "true", ["identityVerificationBasis"] = "GovernmentIdSeen" };
    internal static Task<HttpResponseMessage> FormAsync(HttpClient client, string url, Dictionary<string, string> fields, string? key = null, bool evidence = false, string? onboarding = null)
    {
        var form = new MultipartFormDataContent(); foreach (var (k, v) in fields) form.Add(new StringContent(v), k);
        if (evidence) { var image = new ByteArrayContent(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=")); image.Headers.ContentType = new("image/png"); form.Add(image, "identityFront", "synthetic.png"); form.Add(new StringContent("GovernmentIdFront"), "evidenceType"); }
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = form }; request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString()); if (onboarding is not null) request.Headers.Add("X-Onboarding-Token", onboarding);
        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }
    internal static async Task<string> VerifyAsync(Phase13ApiFixture app, HttpClient client, string email, string purpose, Guid? patientReference = null)
    {
        using var start = await client.PostAsJsonAsync("/api/v1/patient-onboarding/start", new { email, purpose, patientReference }, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.Accepted, start.StatusCode);
        var id = (await JsonAsync(start)).GetProperty("requestId").GetGuid(); string text = "";
        await app.WithDbAsync(async db => text = (await db.EmailOutboxMessages.SingleAsync(m => m.IdempotencyKey == "patient-otp:" + id.ToString("N"))).TextBody!);
        var otp = Regex.Match(text, @"Verification code: (\d+)", RegexOptions.CultureInvariant).Groups[1].Value;
        using var verified = await client.PostAsJsonAsync("/api/v1/patient-onboarding/verify-email", new { requestId = id, otp }, TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        return (await JsonAsync(verified)).GetProperty("verificationToken").GetString()!;
    }
    internal static async Task<string> InvitationAsync(Phase13ApiFixture app, string email)
    {
        string text = ""; await app.WithDbAsync(async db => text = (await db.EmailOutboxMessages.Where(m => m.RecipientEmail == email && m.IdempotencyKey.StartsWith("patient-full-invite:")).OrderByDescending(m => m.CreatedOnUtc).FirstAsync()).TextBody!);
        return text.Split('\n')[^1].Trim();
    }
}
