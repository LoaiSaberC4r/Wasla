using BuildingBlock.Application.Abstraction.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.PatientOnboarding;
using Wasla.Application.Persistence;
using Wasla.Domain.Common;
using Wasla.Domain.Patients;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

namespace Wasla.Tests.Integration;

public sealed class Phase5FinalFaultTests
{
    [Fact]
    public async Task Uncertain_commit_cleanup_preserves_committed_private_media_and_deletes_only_unreferenced_upload()
    {
        await using var app = await Phase13ApiFixture.CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var media = scope.ServiceProvider.GetRequiredService<IMediaService>();
        var compensation = scope.ServiceProvider.GetRequiredService<IPatientIdentityMediaCompensation>();
        var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=");
        using var first = new MemoryStream(bytes); using var second = new MemoryStream(bytes);
        var retained = await media.SaveAsync(new MediaUpload(first, "synthetic.png", "image/png"), new("PatientClaims/synthetic"), TestContext.Current.CancellationToken);
        var orphan = await media.SaveAsync(new MediaUpload(second, "synthetic.png", "image/png"), new("PatientClaims/synthetic"), TestContext.Current.CancellationToken);
        await app.WithDbAsync(async db =>
        {
            db.Patients.Add(Patient.Create(Guid.NewGuid(), "هوية تجريبية محفوظة", null, new(1990, 1, 1), Gender.Male, null, null, retained.Key, null, null, app.Today).Value);
            await db.SaveChangesAsync();
        });
        // Model a lost commit acknowledgement: the caller invokes compensation even
        // though another SQL scope can already see the successful reference.
        compensation.Track(retained.Key); compensation.Track(orphan.Key);
        await compensation.CleanAsync();
        Assert.True(File.Exists(Path.Combine(app.MedicalMediaRoot, retained.Key.Replace('/', Path.DirectorySeparatorChar))));
        Assert.False(File.Exists(Path.Combine(app.MedicalMediaRoot, orphan.Key.Replace('/', Path.DirectorySeparatorChar))));
        await compensation.CleanAsync();
        Assert.True(File.Exists(Path.Combine(app.MedicalMediaRoot, retained.Key.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact, Trait("Category", "SQLServerConcurrency")]
    public async Task Independent_retention_worker_waits_for_committed_legal_hold_and_never_deletes_held_evidence()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(true);
        var claimId = Guid.NewGuid(); Guid actor = default; var now = app.Clock.UtcNow;
        await app.WithDbAsync(async db =>
        {
            actor = (await db.ApplicationUsers.SingleAsync(u => u.UserName == "root")).Id;
            var challenge = new PatientOnboardingChallenge(Guid.NewGuid(), PatientOnboardingPurpose.Claim, "retention-lock@example.test", null, null, "synthetic-hash", now, now.AddMinutes(10), 5);
            var claim = new PatientClaimRequest(claimId, null, "retention-lock@example.test", null, "هوية تجريبية", new(1990, 1, 1), Gender.Male, null, challenge.Id, Guid.NewGuid().ToString(), "synthetic-fingerprint", now);
            Assert.True(claim.Decide(PatientClaimStatus.Rejected, actor, "Synthetic terminal review", null, now).IsSuccess);
            db.PatientOnboardingChallenges.Add(challenge); db.PatientClaimRequests.Add(claim);
            db.PatientClaimEvidences.Add(new(Guid.NewGuid(), claimId, 1, PatientClaimEvidenceType.GovernmentIdFront, "PatientClaims/synthetic-held.png", "image/png", 10, "synthetic-hash", PatientEvidenceUploaderType.Applicant, null, now));
            await db.SaveChangesAsync();
        });
        app.Clock.UtcNow = now.AddDays(90);
        await using var holdScope = app.Services.CreateAsyncScope();
        var holdDb = holdScope.ServiceProvider.GetRequiredService<WaslaDbContext>();
        await using var holdTransaction = await holdDb.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await holdScope.ServiceProvider.GetRequiredService<IPatientIdentityStore>().LockAsync(TestContext.Current.CancellationToken);
        var held = await holdDb.PatientClaimRequests.SingleAsync(c => c.Id == claimId, TestContext.Current.CancellationToken);
        held.SetLegalHold(true, actor, "Synthetic independently committed legal hold", app.Clock.UtcNow);
        await holdDb.SaveChangesAsync(TestContext.Current.CancellationToken);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var media = new ForbidDeletion();
        var worker = Task.Run(async () =>
        {
            await using var workerScope = app.Services.CreateAsyncScope();
            var db = workerScope.ServiceProvider.GetRequiredService<WaslaDbContext>();
            var store = workerScope.ServiceProvider.GetRequiredService<IPatientIdentityStore>();
            var retention = new PatientIdentityRetention(db, store, media, app.Clock, Options.Create(new PatientIdentityOptions { EvidencePurgingEnabled = true }));
            started.SetResult(); return await retention.RunAsync(TestContext.Current.CancellationToken);
        }, TestContext.Current.CancellationToken);
        await started.Task;
        Assert.False(worker.IsCompleted);
        await holdTransaction.CommitAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, await worker);
        await app.WithDbAsync(async db =>
        {
            Assert.True((await db.PatientClaimRequests.SingleAsync(c => c.Id == claimId)).IsUnderLegalHold);
            var evidence = await db.PatientClaimEvidences.SingleAsync(e => e.ClaimRequestId == claimId);
            Assert.Null(evidence.RetiredOnUtc); Assert.Null(evidence.PurgedOnUtc);
            Assert.Equal(2, await db.Patients.CountAsync()); Assert.Equal(2, await db.PatientAccountLinks.CountAsync());
        });
    }

    private sealed class ForbidDeletion : IMediaService
    {
        public Task DeleteAsync(string key, CancellationToken ct = default) => throw new InvalidOperationException("Held evidence must not reach storage deletion.");
        public Task DeleteRangeAsync(IEnumerable<string> keys, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<StoredMedia> SaveAsync(MediaUpload upload, MediaStorageRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<StoredMedia>> SaveAsync(IEnumerable<MediaUpload> uploads, MediaStorageRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
