using Microsoft.EntityFrameworkCore;
using Wasla.Application.Persistence;
using Wasla.Domain.Patients;
using Wasla.Domain.Medications;
namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed class PatientIdentityStore(WaslaDbContext db) : IPatientIdentityStore
{
    public async Task<IReadOnlySet<Guid>> PatientsWithContactPhoneAsync(IReadOnlyList<Guid> patients, CancellationToken ct) =>
        (await db.PatientContacts.Where(c => patients.Contains(c.PatientId) && c.PhoneNumber != "").Select(c => c.PatientId).Distinct().ToArrayAsync(ct)).ToHashSet();
    public Task<Wasla.Domain.Security.ApplicationUser?> UserByEmailAsync(string email, CancellationToken ct) => db.ApplicationUsers.SingleOrDefaultAsync(u => u.Email == email, ct);
    public Task<bool> HasResendAsync(string key, CancellationToken ct) => db.PatientActivationInvitations.AnyAsync(i => i.ResendIdempotencyKey == key, ct);
    public async Task LockAsync(CancellationToken ct)
    {
        if (!db.Database.IsSqlServer()) return;
        // One transaction-owned identity gate establishes a fixed lock order across all identity flows.
        // Booking acquires this only after its existing operational locks; identity writes never acquire booking locks.
        await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r = sys.sp_getapplock @Resource='Wasla:PatientIdentity', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @r < 0 THROW 51005, 'Identity operation busy.', 1;", ct);
    }
    public Task<Patient?> PatientByEmailAsync(string email, CancellationToken ct) => db.Patients.SingleOrDefaultAsync(p => p.Email != null && EF.Property<string>(p, "NormalizedEmail") == email, ct);
    public Task<PatientAccountLink?> LinkByPatientAsync(Guid patient, CancellationToken ct) => db.PatientAccountLinks.SingleOrDefaultAsync(l => l.PatientId == patient, ct);
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1311", Justification = "ToUpper is translated to database UPPER, not evaluated under the CLR culture.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1304", Justification = "The provider translates UPPER; culture overloads cannot be translated.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1862", Justification = "StringComparison overloads cannot be translated by EF Core SQL Server.")]
    public async Task<(IReadOnlyList<Patient> Items, long Total)> MatchAsync(PatientMatchCriteria c, int page, int size, bool softOr, CancellationToken ct)
    {
        var q = db.Patients.AsQueryable();
        if (c.OnlyUnlinked) q = q.Where(p => !db.PatientAccountLinks.Any(l => l.PatientId == p.Id));
        var email = PatientIdentityEmail.Normalize(c.Email); var phone = c.PhoneNumber?.Trim(); var secondary = c.SecondaryPhoneNumber?.Trim(); var name = c.NameAr?.Trim().ToUpperInvariant();
        if (email is not null && !softOr) q = q.Where(p => p.Email != null && EF.Property<string>(p, "NormalizedEmail") == email);
        else if (softOr) q = q.Where(p => email != null && p.Email != null && EF.Property<string>(p, "NormalizedEmail") == email ||
            phone != null && phone != "" && (p.PhoneNumber == phone || db.PatientContacts.Any(c => c.PatientId == p.Id && c.PhoneNumber == phone)) ||
            secondary != null && secondary != "" && (p.PhoneNumber == secondary || db.PatientContacts.Any(c => c.PatientId == p.Id && c.PhoneNumber == secondary)) || name != null && name != "" && EF.Property<string>(p, "NormalizedSearchNameAr") == name &&
            p.DateOfBirth == c.DateOfBirth && (!c.Gender.HasValue || p.Gender == c.Gender));
        else
        {
            if (!string.IsNullOrEmpty(phone)) q = q.Where(p => p.PhoneNumber == phone || db.PatientContacts.Any(c => c.PatientId == p.Id && c.PhoneNumber == phone));
            if (!string.IsNullOrEmpty(name)) q = q.Where(p => EF.Property<string>(p, "NormalizedSearchNameAr").StartsWith(name) || p.NameEn != null && EF.Property<string>(p, "NormalizedSearchNameEn").StartsWith(name));
            if (c.DateOfBirth.HasValue) q = q.Where(p => p.DateOfBirth == c.DateOfBirth);
            if (c.Gender.HasValue) q = q.Where(p => p.Gender == c.Gender);
        }
        var total = await q.LongCountAsync(ct);
        return (await q.OrderBy(p => p.NameAr).ThenBy(p => p.Id).Skip((page - 1) * size).Take(size).ToListAsync(ct), total);
    }
    public Task<PatientOnboardingChallenge?> ChallengeAsync(Guid id, CancellationToken ct) => db.PatientOnboardingChallenges.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<PatientOnboardingChallenge?> MutationReplayAsync(string email, string key, CancellationToken ct) =>
        db.PatientOnboardingChallenges.Where(c => c.NormalizedEmail == email && c.MutationIdempotencyKey == key && c.MutationResponseJson != null).OrderByDescending(c => c.CreatedOnUtc).FirstOrDefaultAsync(ct);
    public Task<PatientOnboardingChallenge?> ContinuationAsync(string hash, CancellationToken ct) => db.PatientOnboardingChallenges.SingleOrDefaultAsync(x => x.ContinuationTokenHash == hash, ct);
    public async Task<IReadOnlyList<PatientOnboardingChallenge>> RecentChallengesAsync(string email, PatientOnboardingPurpose purpose, DateTime since, CancellationToken ct) =>
        await db.PatientOnboardingChallenges.Where(x => x.NormalizedEmail == email && x.Purpose == purpose && x.CreatedOnUtc >= since).OrderByDescending(x => x.CreatedOnUtc).Take(20).ToArrayAsync(ct);
    public Task<PatientClaimRequest?> ClaimAsync(Guid id, CancellationToken ct) => db.PatientClaimRequests.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<PatientClaimRequest?> ClaimReplayAsync(string email, string key, CancellationToken ct) => db.PatientClaimRequests.SingleOrDefaultAsync(x => x.ClaimantNormalizedEmail == email && x.IdempotencyKey == key, ct);
    public Task<bool> OpenClaimAsync(string email, Guid? patient, Guid? excluding, CancellationToken ct) => db.PatientClaimRequests.AnyAsync(x => x.Id != excluding &&
        (patient != null && x.TargetPatientId == patient || patient == null && x.TargetPatientId == null && x.ClaimantNormalizedEmail == email || x.Status == PatientClaimStatus.ApprovedPendingActivation && x.ClaimantNormalizedEmail == email) && (x.Status == PatientClaimStatus.PendingReview || x.Status == PatientClaimStatus.ModificationRequested || x.Status == PatientClaimStatus.ApprovedPendingActivation), ct);
    public async Task<(IReadOnlyList<PatientClaimRequest> Items, long Total)> ClaimsAsync(PatientClaimStatus? status, DateTime? from, DateTime? toUtc, string? email, int page, int size, CancellationToken ct)
    {
        var q = db.PatientClaimRequests.Where(x => (!status.HasValue || x.Status == status) && (!from.HasValue || x.SubmittedOnUtc >= from) &&
            (!toUtc.HasValue || x.SubmittedOnUtc <= toUtc) && (email == null || x.ClaimantNormalizedEmail == email));
        return (await q.OrderByDescending(x => x.SubmittedOnUtc).ThenBy(x => x.Id).Skip((page - 1) * size).Take(size).ToArrayAsync(ct), await q.LongCountAsync(ct));
    }
    public async Task<IReadOnlyList<PatientClaimEvidence>> EvidenceAsync(Guid claim, CancellationToken ct) => await db.PatientClaimEvidences.Where(x => x.ClaimRequestId == claim).OrderBy(x => x.RevisionNumber).ThenBy(x => x.Id).ToArrayAsync(ct);
    public async Task<IReadOnlyList<PatientClaimHistory>> HistoryAsync(Guid claim, CancellationToken ct) => await db.PatientClaimHistories.Where(x => x.ClaimRequestId == claim).OrderBy(x => x.OccurredOnUtc).ThenBy(x => x.Id).ToArrayAsync(ct);
    public Task<PatientActivationInvitation?> InvitationAsync(string hash, CancellationToken ct) => db.PatientActivationInvitations.SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
    public async Task<IReadOnlyList<PatientActivationInvitation>> LiveInvitationsAsync(Guid? claim, Guid? user, CancellationToken ct) => await db.PatientActivationInvitations.Where(x =>
        (claim != null && x.ClaimRequestId == claim || user != null && x.ApplicationUserId == user) && x.ConsumedOnUtc == null && x.RevokedOnUtc == null).ToArrayAsync(ct);
    public Task<PatientIdentityAttestation?> AttestationAsync(Guid id, CancellationToken ct) => db.PatientIdentityAttestations.SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task<bool> HasPracticeRelationshipAsync(Guid patient, Guid practice, CancellationToken ct) =>
        await db.Reservations.AnyAsync(x => x.PatientId == patient && x.DoctorPracticeId == practice, ct) ||
        await db.Set<Wasla.Domain.Tickets.Ticket>().AnyAsync(x => x.PatientId == patient && x.DoctorPracticeId == practice, ct);
    public Task<PatientDuplicateCase?> DuplicateAsync(Guid id, CancellationToken ct) => db.PatientDuplicateCases.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<PatientDuplicateCase?> OpenDuplicateAsync(Guid lower, Guid higher, CancellationToken ct) => db.PatientDuplicateCases.SingleOrDefaultAsync(x => x.LowerPatientId == lower && x.HigherPatientId == higher && x.Status == PatientDuplicateStatus.NeedsMoreEvidence, ct);
    public async Task<(IReadOnlyList<PatientDuplicateCase> Items, long Total)> DuplicatesAsync(PatientDuplicateStatus? status, int page, int size, CancellationToken ct)
    {
        var q = db.PatientDuplicateCases.Where(x => !status.HasValue || x.Status == status);
        return (await q.OrderByDescending(x => x.CreatedOnUtc).ThenBy(x => x.Id).Skip((page - 1) * size).Take(size).ToArrayAsync(ct), await q.LongCountAsync(ct));
    }
    public Task<MedicationIdempotencyRecord?> OperationAsync(Guid actor, string operation, string key, CancellationToken ct) => db.Set<MedicationIdempotencyRecord>().SingleOrDefaultAsync(x => x.ActorApplicationUserId == actor && x.Operation == operation && x.IdempotencyKey == key, ct);
}
