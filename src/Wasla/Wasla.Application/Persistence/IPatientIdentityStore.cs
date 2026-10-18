using Wasla.Domain.Common;
using Wasla.Domain.Patients;
using Wasla.Domain.Medications;
namespace Wasla.Application.Persistence;

public sealed record PatientMatchCriteria(string? Email, string? PhoneNumber, string? NameAr, DateOnly? DateOfBirth, Gender? Gender = null, bool OnlyUnlinked = false, string? SecondaryPhoneNumber = null);
public interface IPatientIdentityStore
{
    Task<IReadOnlySet<Guid>> PatientsWithContactPhoneAsync(IReadOnlyList<Guid> patients, CancellationToken ct);
    Task LockAsync(CancellationToken ct);
    Task<Wasla.Domain.Security.ApplicationUser?> UserByEmailAsync(string email, CancellationToken ct);
    Task<bool> HasResendAsync(string key, CancellationToken ct);
    Task<Patient?> PatientByEmailAsync(string email, CancellationToken ct);
    Task<PatientAccountLink?> LinkByPatientAsync(Guid patient, CancellationToken ct);
    Task<(IReadOnlyList<Patient> Items, long Total)> MatchAsync(PatientMatchCriteria criteria, int page, int size, bool softOr, CancellationToken ct);
    Task<PatientOnboardingChallenge?> ChallengeAsync(Guid id, CancellationToken ct);
    Task<PatientOnboardingChallenge?> MutationReplayAsync(string email, string key, CancellationToken ct);
    Task<PatientOnboardingChallenge?> ContinuationAsync(string hash, CancellationToken ct);
    Task<IReadOnlyList<PatientOnboardingChallenge>> RecentChallengesAsync(string email, PatientOnboardingPurpose purpose, DateTime since, CancellationToken ct);
    Task<PatientClaimRequest?> ClaimAsync(Guid id, CancellationToken ct);
    Task<PatientClaimRequest?> ClaimReplayAsync(string email, string key, CancellationToken ct);
    Task<bool> OpenClaimAsync(string email, Guid? patient, Guid? excluding, CancellationToken ct);
    Task<(IReadOnlyList<PatientClaimRequest> Items, long Total)> ClaimsAsync(PatientClaimStatus? status, DateTime? from, DateTime? toUtc, string? email, int page, int size, CancellationToken ct);
    Task<IReadOnlyList<PatientClaimEvidence>> EvidenceAsync(Guid claim, CancellationToken ct);
    Task<IReadOnlyList<PatientClaimHistory>> HistoryAsync(Guid claim, CancellationToken ct);
    Task<PatientActivationInvitation?> InvitationAsync(string hash, CancellationToken ct);
    Task<IReadOnlyList<PatientActivationInvitation>> LiveInvitationsAsync(Guid? claim, Guid? user, CancellationToken ct);
    Task<PatientIdentityAttestation?> AttestationAsync(Guid id, CancellationToken ct);
    Task<bool> HasPracticeRelationshipAsync(Guid patient, Guid practice, CancellationToken ct);
    Task<PatientDuplicateCase?> DuplicateAsync(Guid id, CancellationToken ct);
    Task<PatientDuplicateCase?> OpenDuplicateAsync(Guid lower, Guid higher, CancellationToken ct);
    Task<(IReadOnlyList<PatientDuplicateCase> Items, long Total)> DuplicatesAsync(PatientDuplicateStatus? status, int page, int size, CancellationToken ct);
    Task<MedicationIdempotencyRecord?> OperationAsync(Guid actor, string operation, string key, CancellationToken ct);
}
