using System.Security.Cryptography;
using System.Text;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.Families;
using Wasla.Application.Features.Tickets.Common;
using Wasla.Domain.Archives;
using Wasla.Domain.Families;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed class FamilyCandidateStore(WaslaDbContext db, ITicketQueueLock locks, IDateTimeProvider clock,
    IOptions<DependentManagementOptions> options) : IFamilyCandidateStore
{
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static Error Unavailable => Error.NotFound("FamilyCandidate.ReferenceInvalid", Wasla.Domain.Resources.FamilySelfServiceText.Get("FamilyCandidate_ReferenceInvalid"));
    public async Task<Result<FamilyCandidateSearchResponse>> SearchAsync(Guid actor, Guid parent, FamilyCandidateSearch criteria, CancellationToken ct)
    {
        await locks.AcquireIdempotencyAsync(actor, "FamilyCandidate:Search", "RateWindow", ct);
        var since = clock.UtcNow.AddMinutes(-15);
        // Durable actor limit supplements the HTTP IP limiter and works across nodes.
        if (await db.Set<ArchiveAuditEvent>().CountAsync(e => e.ActorId == actor && e.Action == "FamilyCandidateSearch" && e.OccurredAtUtc >= since, ct) >= 20)
            return Result<FamilyCandidateSearchResponse>.Fail(Error.RateLimit("FamilyCandidate.RateLimited", Wasla.Domain.Resources.FamilySelfServiceText.Get("FamilyCandidate_RateLimited"), TimeSpan.FromMinutes(15)));
        var name = FamilyIdentitySearch.Normalize(criteria.NameAr);
        var eligible = db.Patients.AsNoTracking().Where(p => p.Id != parent && p.DateOfBirth == criteria.DateOfBirth && p.Gender == criteria.Gender);
        var found = await FamilyIdentitySearch.Exact(eligible, name).OrderBy(p => p.Id).Select(p => new { p.Id, p.DateOfBirth }).Take(options.Value.SearchMaxCandidates + 1).ToArrayAsync(ct);
        db.Add(new ArchiveAuditEvent("FamilyCandidateSearch", "FamilyCandidate", null, null, actor, clock.UtcNow));
        if (found.Length > 1)
        {
            await db.SaveChangesAsync(ct);
            return Result<FamilyCandidateSearchResponse>.Ok(new("ManualReviewRequired", []));
        }
        var results = new List<FamilyCandidateResponse>();
        foreach (var p in found)
        {
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var expires = clock.UtcNow.AddMinutes(options.Value.CandidateReferenceTtlMinutes);
            db.Add(new FamilyCandidateSelection(actor, parent, p.Id, Hash(token), Hash($"{name}:{criteria.DateOfBirth:yyyy-MM-dd}:{criteria.Gender}"), clock.UtcNow, expires));
            results.Add(new(token, "***", p.DateOfBirth.Year, expires));
        }
        await db.SaveChangesAsync(ct);
        return Result<FamilyCandidateSearchResponse>.Ok(new(found.Length == 0 ? "NoPotentialMatch" : "PotentialMatches", results));
    }
    public async Task<Result<CandidateRedemption>> ResolveAsync(Guid actor, Guid parent, string reference, string key, string fingerprint, CancellationToken ct)
    {
        if (reference.Length != 64 || !reference.All(Uri.IsHexDigit)) return Result<CandidateRedemption>.Fail(Unavailable);
        await locks.AcquireIdempotencyAsync(actor, "FamilyCandidate:Idempotency", key, ct);
        await locks.AcquireIdempotencyAsync(actor, "FamilyCandidate:Redeem", Hash(reference), ct);
        var hash = Hash(reference);
        var row = await db.Set<FamilyCandidateSelection>().SingleOrDefaultAsync(s => s.TokenHash == hash && s.ActorApplicationUserId == actor && s.ParentPatientId == parent && s.Purpose == "ParentChildRelationship", ct);
        if (row is null) return Result<CandidateRedemption>.Fail(Unavailable);
        if (await db.Set<FamilyCandidateSelection>().AnyAsync(s => s.ActorApplicationUserId == actor && s.IdempotencyKey == key && s.Id != row.Id, ct))
            return Result<CandidateRedemption>.Fail(Error.Conflict("FamilyCandidate.ReferenceConsumed", Wasla.Domain.Resources.FamilySelfServiceText.Get("FamilyCandidate_ReferenceConsumed")));
        if (row.ConsumedAtUtc is not null)
            return row.IdempotencyKey == key && row.SubmissionFingerprint == fingerprint && (row.RelationshipRequestId is not null || row.GovernanceCaseId is not null) ?
                Result<CandidateRedemption>.Ok(new(row.TargetPatientId, row.RelationshipRequestId, row.GovernanceCaseId)) :
                Result<CandidateRedemption>.Fail(Error.Conflict("FamilyCandidate.ReferenceConsumed", Wasla.Domain.Resources.FamilySelfServiceText.Get("FamilyCandidate_ReferenceConsumed")));
        return row.ExpiresAtUtc <= clock.UtcNow ? Result<CandidateRedemption>.Fail(Unavailable) : Result<CandidateRedemption>.Ok(new(row.TargetPatientId, null));
    }
    public async Task CompleteConflictAsync(Guid actor, string reference, string key, string fingerprint, Guid caseId, CancellationToken ct)
    {
        var hash = Hash(reference);
        var row = await db.Set<FamilyCandidateSelection>().SingleAsync(s => s.TokenHash == hash && s.ActorApplicationUserId == actor, ct);
        row.ConsumeConflict(key, fingerprint, caseId, clock.UtcNow);
    }
    public async Task CompleteAsync(Guid actor, string reference, string key, string fingerprint, Guid request, CancellationToken ct)
    {
        var hash = Hash(reference);
        var row = await db.Set<FamilyCandidateSelection>().SingleAsync(s => s.TokenHash == hash && s.ActorApplicationUserId == actor, ct);
        row.Consume(key, fingerprint, request, clock.UtcNow);
    }
}
