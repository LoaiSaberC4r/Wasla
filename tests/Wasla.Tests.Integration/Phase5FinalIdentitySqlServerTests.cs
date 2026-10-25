using System.Net;
using Microsoft.EntityFrameworkCore;
using Wasla.Domain.Common;
using Wasla.Domain.Patients;
using Wasla.Domain.Security;
using static Wasla.Tests.Integration.Phase13ApiFixture;
using BuildingBlock.Application.Exceptions;
using Microsoft.Extensions.DependencyInjection;
namespace Wasla.Tests.Integration;
public sealed class Phase5FinalIdentitySqlServerTests
{
    [Fact, Trait("Category", "SQLServerConcurrency")]
    public async Task Native_constraints_reject_double_live_invites_open_claims_and_links()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(true); var claim = Guid.NewGuid(); var user = Guid.Empty;
        await app.WithDbAsync(async db =>
        {
            user = (await db.ApplicationUsers.SingleAsync(u => u.UserName == "patient")).Id;
            var challenge = new PatientOnboardingChallenge(Guid.NewGuid(), PatientOnboardingPurpose.Claim, "constraints@example.test", null, null, "hash", app.Clock.UtcNow, app.Clock.UtcNow.AddMinutes(10), 5); db.PatientOnboardingChallenges.Add(challenge);
            db.PatientClaimRequests.Add(new(claim, null, "constraints@example.test", null, "مريض تجريبي", new(1990, 1, 1), Gender.Male, null, challenge.Id, Guid.NewGuid().ToString(), "fingerprint", app.Clock.UtcNow));
            db.PatientActivationInvitations.Add(new(Guid.NewGuid(), PatientActivationPurpose.ReceptionFull, user, null, "patient@example.test", "unique-invite-hash", app.Clock.UtcNow, app.Clock.UtcNow.AddHours(48)));
            await db.SaveChangesAsync();
        });
        await app.WithDbAsync(async db =>
        {
            db.PatientActivationInvitations.Add(new(Guid.NewGuid(), PatientActivationPurpose.ReceptionFull, user, null, "patient@example.test", "other-invite-hash", app.Clock.UtcNow, app.Clock.UtcNow.AddHours(48)));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
        });
        await app.WithDbAsync(async db =>
        {
            var original = await db.PatientClaimRequests.SingleAsync(c => c.Id == claim);
            db.PatientClaimRequests.Add(new(Guid.NewGuid(), null, original.ClaimantNormalizedEmail, null, original.ApplicantNameAr, original.ApplicantDateOfBirth, original.ApplicantGender, null, original.SourceChallengeId, Guid.NewGuid().ToString(), "fingerprint", app.Clock.UtcNow));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
        });
        await app.WithDbAsync(async db =>
        {
            var otherUser = (await db.ApplicationUsers.SingleAsync(u => u.UserName == "other-patient")).Id;
            db.PatientAccountLinks.Add(PatientAccountLink.Create(Guid.NewGuid(), otherUser, app.PatientId, PatientAccountLinkSource.PatientClaim, app.Clock.UtcNow).Value);
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
        });
    }
    [Fact] [Trait("Category", "SQLServerConcurrency")]
    public async Task Native_migration_normalized_unique_email_shared_contacts_and_concurrent_registration()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(true);
        var first = Guid.NewGuid(); var second = Guid.NewGuid();
        await app.WithDbAsync(async db =>
        {
            Assert.True(db.Database.IsSqlServer());
            Assert.Contains("20261018140120_Phase5FinalPatientIdentityClosure", await db.Database.GetAppliedMigrationsAsync());
            db.Patients.AddRange(Patient.Create(first, "مريض تجريبي أول", null, new(1990, 1, 1), Gender.Male, "01000000000", "native@example.test", null, null, null, app.Today).Value,
                Patient.Create(second, "مريض تجريبي ثان", null, new(1990, 1, 1), Gender.Male, "01000000000", null, null, null, null, app.Today).Value);
            await db.SaveChangesAsync();
            db.PatientContacts.AddRange(PatientContact.Create(Guid.NewGuid(), first, "والد تجريبي", null, "01000000000", PatientContactRelationshipType.Father, null, true, "parent@example.test").Value,
                PatientContact.Create(Guid.NewGuid(), second, "والد تجريبي", null, "01000000000", PatientContactRelationshipType.Father, null, true, "parent@example.test").Value);
            await db.SaveChangesAsync(); Assert.Equal(8, (await db.Patients.SingleAsync(p => p.Id == first)).RowVersion.Length);
            // Bypass application normalization to prove that SQL itself arbitrates whitespace/case variants.
            await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [Patients] SET [Email] = {" NATIVE@Example.Test "} WHERE [Id] = {second}", TestContext.Current.CancellationToken));
        });
        await app.WithDbAsync(async db =>
        {
            var profile = await db.Patients.SingleAsync(p => p.Id == first);
            Assert.True(profile.UpdateProfile(profile.NameAr, "Local pending edit", profile.PhoneNumber, profile.Email).IsSuccess);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [Patients] SET [NameEn] = {"Concurrent committed edit"} WHERE [Id] = {first}", TestContext.Current.CancellationToken);
            var exception = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
            await using var mapperScope = app.Services.CreateAsyncScope();
            var mapper = mapperScope.ServiceProvider.GetServices<IExceptionToErrorMapper>().Single(m => m.GetType().Name == "PatientIdentityPersistenceErrorMapper");
            Assert.True(mapper.TryMap(exception, out var error)); Assert.Equal("Patient.ConcurrencyConflict", error.Code);
        });
        using var staff = await app.ClientAsync("reception");
        var a = Phase5IdentityApi.Profile(app, "race@example.test"); a["phoneNumber"] = "01000000001";
        var b = new Dictionary<string, string>(a);
        var results = await Task.WhenAll(Phase5IdentityApi.FormAsync(staff, "/api/v1/patients", a), Phase5IdentityApi.FormAsync(staff, "/api/v1/patients", b));
        try { Assert.Single(results, r => r.StatusCode == HttpStatusCode.Created); Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict); }
        finally { foreach (var r in results) r.Dispose(); }
        await app.WithDbAsync(async db => Assert.Equal(1, await db.Patients.CountAsync(p => p.Email == "race@example.test")));
    }
}
