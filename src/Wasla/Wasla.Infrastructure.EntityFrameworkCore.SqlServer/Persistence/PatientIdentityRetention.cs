using BuildingBlock.Application.Abstraction.Media;
using BuildingBlock.Application.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.PatientOnboarding;
using Wasla.Application.Persistence;
using Wasla.Domain.Archives;
using Wasla.Domain.Patients;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

public sealed class PatientIdentityRetention(WaslaDbContext db, IPatientIdentityStore store, IMediaService media,
    IDateTimeProvider clock, IOptions<PatientIdentityOptions> options)
{
    public async Task<int> RunAsync(CancellationToken ct = default)
    {
        var o = options.Value; var now = clock.UtcNow; var count = 0;
        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            await store.LockAsync(ct);
            var cutoff = now.AddDays(-o.ClaimInactivityExpiryDays);
            var claims = await db.PatientClaimRequests.Where(c => (c.Status == PatientClaimStatus.PendingReview || c.Status == PatientClaimStatus.ModificationRequested || c.Status == PatientClaimStatus.ApprovedPendingActivation) && c.LastActivityOnUtc <= cutoff)
                .OrderBy(c => c.LastActivityOnUtc).Take(o.WorkerBatchSize).ToArrayAsync(ct);
            foreach (var c in claims)
            {
                var old = c.Status; if (!c.Expire(now, o.ClaimInactivityExpiryDays)) continue;
                db.PatientClaimHistories.Add(new(Guid.NewGuid(), c.Id, c.EvidenceRevision, old, c.Status, PatientClaimAction.Expired, null, now));
                foreach (var invite in await store.LiveInvitationsAsync(c.Id, null, ct)) invite.Revoke(now);
                count++;
            }
            if (o.EvidencePurgingEnabled)
            {
                var evidenceCutoff = now.AddDays(-o.EvidenceAfterClosureRetentionDays);
                var evidence = await (from e in db.PatientClaimEvidences join c in db.PatientClaimRequests on e.ClaimRequestId equals c.Id
                    where !c.IsUnderLegalHold && c.ClosedOnUtc != null && c.ClosedOnUtc <= evidenceCutoff && e.RetiredOnUtc == null && e.PurgedOnUtc == null
                    orderby c.ClosedOnUtc, e.Id select e).Take(o.WorkerBatchSize).ToArrayAsync(ct);
                foreach (var e in evidence) e.Retire(now);
            }
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        }
        db.ChangeTracker.Clear();
        if (o.EvidencePurgingEnabled)
        {
            // Tombstones above are already durable before storage deletion begins. The second transaction
            // serializes the final legal-hold recheck and delete with privileged hold changes on every instance.
            await using var transaction = await db.Database.BeginTransactionAsync(ct); await store.LockAsync(ct);
            var retired = await (from e in db.PatientClaimEvidences join c in db.PatientClaimRequests on e.ClaimRequestId equals c.Id
                where e.RetiredOnUtc != null && e.PurgedOnUtc == null && !c.IsUnderLegalHold orderby e.RetiredOnUtc, e.Id select e).Take(o.WorkerBatchSize).ToArrayAsync(ct);
            foreach (var e in retired)
            {
                if (e.PrivateMediaKey is { } key) await media.DeleteAsync(key, ct);
                e.MarkPurged(now);
                var c = (await store.ClaimAsync(e.ClaimRequestId, ct))!;
                db.PatientClaimHistories.Add(new(Guid.NewGuid(), c.Id, e.RevisionNumber, c.Status, c.Status, PatientClaimAction.EvidencePurged, null, now)); count++;
            }
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); db.ChangeTracker.Clear();
        }
        if (o.AuditPurgingEnabled)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct); await store.LockAsync(ct);
            var cutoff = now.AddMonths(-o.AuditAfterClosureRetentionMonths);
            var claims = await db.PatientClaimRequests.Where(c => !c.IsUnderLegalHold && c.ClosedOnUtc != null && c.ClosedOnUtc <= cutoff &&
                !db.PatientClaimEvidences.Any(e => e.ClaimRequestId == c.Id && e.PurgedOnUtc == null)).OrderBy(c => c.ClosedOnUtc).Take(o.WorkerBatchSize).ToArrayAsync(ct);
            foreach (var c in claims)
            {
                // Delete the expired audit aggregate only. Authentication provenance and Patient links survive.
                await db.PatientActivationInvitations.Where(i => i.ClaimRequestId == c.Id).ExecuteDeleteAsync(ct);
                await db.PatientClaimEvidences.Where(e => e.ClaimRequestId == c.Id).ExecuteDeleteAsync(ct);
                await db.PatientClaimHistories.Where(h => h.ClaimRequestId == c.Id).ExecuteDeleteAsync(ct);
                await db.PatientClaimRequests.Where(x => x.Id == c.Id && !x.IsUnderLegalHold).ExecuteDeleteAsync(ct);
                db.Set<ArchiveAuditEvent>().Add(new("PatientClaimAuditRetired", "PatientClaim", c.Id, null, null, now)); count++;
            }
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); db.ChangeTracker.Clear();
        }
        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            await store.LockAsync(ct); var cutoff = now.AddHours(-o.ExpiredChallengeCleanupHours);
            var ids = await db.PatientOnboardingChallenges.Where(c => c.OtpExpiresOnUtc < cutoff && (c.ContinuationTokenExpiresOnUtc == null || c.ContinuationTokenExpiresOnUtc < cutoff) &&
                !db.PatientClaimRequests.Any(r => r.SourceChallengeId == c.Id)).OrderBy(c => c.OtpExpiresOnUtc).Take(o.WorkerBatchSize).Select(c => c.Id).ToArrayAsync(ct);
            await db.PatientOnboardingChallenges.Where(c => ids.Contains(c.Id)).ExecuteDeleteAsync(ct); await transaction.CommitAsync(ct);
        }
        return count;
    }
}
internal sealed class PatientIdentityRetentionBackgroundService(IServiceScopeFactory scopes, IOptions<PatientIdentityOptions> options, ILogger<PatientIdentityRetentionBackgroundService> logger) : BackgroundService
{
    private static readonly Action<ILogger, string, Exception?> Failed = LoggerMessage.Define<string>(LogLevel.Error, new(5002, "PatientIdentityRetentionFailed"), "Patient identity retention stopped with {ExceptionType}; durable work will retry.");
    private static readonly System.Diagnostics.Metrics.Meter Meter = new("Wasla.PatientIdentity");
    private static readonly System.Diagnostics.Metrics.Counter<long> Processed = Meter.CreateCounter<long>("patient_identity_retention_items");
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.WorkerIntervalSeconds));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await using var scope = scopes.CreateAsyncScope(); Processed.Add(await scope.ServiceProvider.GetRequiredService<PatientIdentityRetention>().RunAsync(stoppingToken)); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception e) { Failed(logger, e.GetType().Name, null); }
        }
    }
}
