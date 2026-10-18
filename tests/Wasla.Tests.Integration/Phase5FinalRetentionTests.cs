using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using BuildingBlock.Application.Abstraction.Media;
using Wasla.Application.Features.PatientOnboarding;
using Wasla.Application.Persistence;
using Wasla.Application.Features.PatientClaims;
using MediatR;
using Wasla.Domain.Common;
using Wasla.Domain.Patients;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;
namespace Wasla.Tests.Integration;
public sealed class Phase5FinalRetentionTests
{
    [Fact] public async Task Evidence_delete_failure_preserves_durable_tombstone_and_retries_before_audit_retirement()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(); var now = app.Clock.UtcNow; var id = Guid.NewGuid(); var evidenceId = Guid.NewGuid();
        await app.WithDbAsync(async db =>
        {
            var challenge = new PatientOnboardingChallenge(Guid.NewGuid(), PatientOnboardingPurpose.Claim, "purge@example.test", null, null, "synthetic-hash", now, now.AddMinutes(10), 5); db.PatientOnboardingChallenges.Add(challenge);
            var claim = new PatientClaimRequest(id, null, "purge@example.test", null, "مريض تجريبي", new(1990, 1, 1), Gender.Male, null, challenge.Id, Guid.NewGuid().ToString(), "fingerprint", now);
            var root = (await db.ApplicationUsers.SingleAsync(u => u.UserName == "root")).Id;
            Assert.True(claim.Decide(PatientClaimStatus.Rejected, root, "Synthetic terminal decision", null, now).IsSuccess); db.PatientClaimRequests.Add(claim);
            db.PatientClaimEvidences.Add(new(evidenceId, id, 1, PatientClaimEvidenceType.GovernmentIdFront, "PatientClaims/synthetic.png", "image/png", 10, "hash", PatientEvidenceUploaderType.Applicant, null, now)); await db.SaveChangesAsync();
        });
        var media = new RetryMedia(); var options = Options.Create(new PatientIdentityOptions { EvidencePurgingEnabled = true, AuditPurgingEnabled = true });
        async Task Run()
        {
            await using var scope = app.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<WaslaDbContext>();
            await new PatientIdentityRetention(db, scope.ServiceProvider.GetRequiredService<IPatientIdentityStore>(), media, app.Clock, options).RunAsync(TestContext.Current.CancellationToken);
        }
        app.Clock.UtcNow = now.AddDays(90).AddTicks(-1); await Run(); Assert.Equal(0, media.Attempts);
        app.Clock.UtcNow = now.AddDays(90); await Assert.ThrowsAsync<IOException>(Run);
        await app.WithDbAsync(async db => { var e = await db.PatientClaimEvidences.SingleAsync(e => e.Id == evidenceId); Assert.NotNull(e.RetiredOnUtc); Assert.Null(e.PurgedOnUtc); Assert.Equal("PatientClaims/synthetic.png", e.PrivateMediaKey); });
        await Run(); Assert.Equal(2, media.Attempts);
        await app.WithDbAsync(async db => Assert.NotNull((await db.PatientClaimEvidences.SingleAsync(e => e.Id == evidenceId)).PurgedOnUtc));
        app.Clock.UtcNow = now.AddMonths(24).AddTicks(-1); await Run();
        await app.WithDbAsync(async db => Assert.True(await db.PatientClaimRequests.AnyAsync(c => c.Id == id)));
        app.Clock.UtcNow = now.AddMonths(24); await Run();
        await app.WithDbAsync(async db => { Assert.False(await db.PatientClaimRequests.AnyAsync(c => c.Id == id)); Assert.Equal(2, await db.PatientAccountLinks.CountAsync()); Assert.Equal(2, await db.Patients.CountAsync()); });
    }
    [Fact] public async Task Worker_expires_at_90_day_boundary_without_purging_held_evidence_or_live_patient_records()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(); var now = app.Clock.UtcNow; var id = Guid.NewGuid();
        await app.WithDbAsync(async db =>
        {
            var challenge = new PatientOnboardingChallenge(Guid.NewGuid(), PatientOnboardingPurpose.Claim, "retention@example.test", null, null, "synthetic-hash", now, now.AddMinutes(10), 5); db.PatientOnboardingChallenges.Add(challenge);
            var c = new PatientClaimRequest(id, null, "retention@example.test", null, "مريض تجريبي", new(1990, 1, 1), Gender.Male, null, challenge.Id, Guid.NewGuid().ToString(), "fingerprint", now); db.PatientClaimRequests.Add(c);
            db.PatientClaimEvidences.Add(new(Guid.NewGuid(), id, 1, PatientClaimEvidenceType.GovernmentIdFront, "PatientClaims/synthetic.png", "image/png", 10, "hash", PatientEvidenceUploaderType.Applicant, null, now));
            c.SetLegalHold(true, (await db.ApplicationUsers.SingleAsync(u => u.UserName == "root")).Id, "Synthetic legal hold", now); await db.SaveChangesAsync();
        });
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            Assert.True((await sender.Send(new SetPatientClaimLegalHoldCommand("root", "wrong-synthetic-password", id, false, "Synthetic release request"), TestContext.Current.CancellationToken)).IsFailure);
            Assert.True((await sender.Send(new SetPatientClaimLegalHoldCommand("root", "Phase13TestPass123!", id, false, "Synthetic reviewed release"), TestContext.Current.CancellationToken)).IsSuccess);
            Assert.True((await sender.Send(new SetPatientClaimLegalHoldCommand("root", "Phase13TestPass123!", id, true, "Synthetic renewed legal hold"), TestContext.Current.CancellationToken)).IsSuccess);
        }
        app.Clock.UtcNow = now.AddDays(90).AddTicks(-1); await RunAsync(app);
        await app.WithDbAsync(async db => Assert.Equal(PatientClaimStatus.PendingReview, (await db.PatientClaimRequests.SingleAsync(c => c.Id == id)).Status));
        app.Clock.UtcNow = now.AddDays(90); await RunAsync(app);
        app.Clock.UtcNow = now.AddDays(181); await RunAsync(app);
        await app.WithDbAsync(async db => { var c = await db.PatientClaimRequests.SingleAsync(c => c.Id == id); Assert.Equal(PatientClaimStatus.Expired, c.Status); Assert.True(c.IsUnderLegalHold); Assert.Null((await db.PatientClaimEvidences.SingleAsync(e => e.ClaimRequestId == id)).PurgedOnUtc); Assert.Equal(2, await db.Patients.CountAsync()); Assert.Equal(2, await db.PatientAccountLinks.CountAsync()); });
    }
    private static async Task RunAsync(Phase13ApiFixture app) { await using var scope = app.Services.CreateAsyncScope(); await scope.ServiceProvider.GetRequiredService<PatientIdentityRetention>().RunAsync(TestContext.Current.CancellationToken); }
    private sealed class RetryMedia : IMediaService
    {
        internal int Attempts { get; private set; }
        public Task DeleteAsync(string key, CancellationToken ct = default) { Assert.StartsWith("PatientClaims/", key); if (++Attempts == 1) throw new IOException("Synthetic transient deletion failure"); return Task.CompletedTask; }
        public Task DeleteRangeAsync(IEnumerable<string> keys, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<StoredMedia> SaveAsync(MediaUpload upload, MediaStorageRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<StoredMedia>> SaveAsync(IEnumerable<MediaUpload> uploads, MediaStorageRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
