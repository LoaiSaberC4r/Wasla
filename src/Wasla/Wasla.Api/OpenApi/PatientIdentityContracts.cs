using Wasla.Domain.Common;
namespace Wasla.Api.OpenApi;

// Documentation DTOs mirror the public projections; persistence entities are never exposed.
public sealed record PatientEmailStartResponse(Guid RequestId, string Message);
public sealed record PatientEmailVerifiedResponse(string VerificationToken, DateTime ExpiresOnUtc, string NextAction);
public sealed record PatientClaimSubmittedResponse(Guid ClaimRequestId, string Status, DateTime SubmittedOnUtc, bool CanAccessMedicalRecord);
public sealed record PatientClaimStatusResponse(Guid ClaimRequestId, string Status, DateTime SubmittedOnUtc, DateTime LastActivityOnUtc,
    string? ReasonForApplicant, bool CanResubmit, bool CanActivate, bool ActivationInvitationSent, DateTime? ClosedOnUtc, string RowVersion);
public sealed record PatientClaimResubmittedResponse(Guid ClaimRequestId, string Status, int RevisionNumber, string RowVersion);
public sealed record PatientActivatedResponse(Guid PatientId, Guid ApplicationUserId, string AccountStatus, bool RequiresLogin);
public sealed record PatientActivationResentResponse(string Message);
public sealed record PatientClaimApprovedResponse(Guid ClaimRequestId, string Status, Guid TargetPatientId, string RowVersion, DateTime InvitationExpiresOnUtc);
public sealed record PatientClaimDecisionResponse(Guid ClaimRequestId, string Status, DateTime? ClosedOnUtc, string RowVersion, bool CanResubmit);
public sealed record PatientRegistrationCandidate(Guid PatientId, string NameAr, DateOnly DateOfBirth, Gender Gender, string? EmailMasked, string? PhoneMasked, bool IsLinkedAccount);
public sealed record PatientRegistrationMatchesResponse(string MatchOutcome, IReadOnlyList<PatientRegistrationCandidate> Candidates, bool CanCreateBasic, bool CanCreateFull, bool RequiresClaimForAccount);
public sealed record PatientClaimListItem(Guid Id, string Status, string? ClaimantEmailMasked, DateTime SubmittedOnUtc, DateTime LastActivityOnUtc, int EvidenceCount, string RowVersion);
public sealed record PatientIdentityPage<T>(IReadOnlyList<T> Items, long TotalCount, int PageNumber, int PageSize);
public sealed record PatientClaimantView(string NameAr, DateOnly DateOfBirth, Gender Gender, string Email, string? PhoneMasked);
public sealed record PatientClaimCandidateView(Guid PatientId, string NameAr, DateOnly DateOfBirth, Gender Gender, string? EmailMasked);
public sealed record PatientClaimEvidenceView(Guid EvidenceId, string Type, int RevisionNumber, DateTime UploadedOnUtc);
public sealed record PatientClaimHistoryItem(Guid EventId, int RevisionNumber, string Action, string? FromStatus, string ToStatus, Guid? ActorId, DateTime OccurredOnUtc, string? Reason);
public sealed record PatientClaimHistoryResponse(IReadOnlyList<PatientClaimHistoryItem> Items);
public sealed record PatientClaimReviewCapabilities(bool CanApprove, bool CanReject, bool CanRequestModification);
public sealed record PatientClaimDetailsResponse(Guid ClaimRequestId, string Status, PatientClaimantView Claimant, Guid? PatientReference,
    IReadOnlyList<PatientClaimCandidateView> CandidatePatients, IReadOnlyList<PatientClaimEvidenceView> Evidence,
    IReadOnlyList<PatientClaimHistoryItem> HistorySummary, DateTime SubmittedOnUtc, DateTime LastActivityOnUtc, string RowVersion, PatientClaimReviewCapabilities ReviewCapabilities);
public sealed record PatientDuplicateListItem(Guid CaseId, Guid FirstPatientId, Guid SecondPatientId, string Status, DateTime CreatedOnUtc, string RowVersion);
public sealed record PatientDuplicateCandidate(Guid Id, string NameAr, DateOnly DateOfBirth, Gender Gender, string? EmailMasked);
public sealed record PatientDuplicateDetailsResponse(Guid CaseId, string Status, PatientDuplicateCandidate PatientA, PatientDuplicateCandidate PatientB, string MatchIndicators, DateTime CreatedOnUtc, string RowVersion);
public sealed record PatientDuplicateResolvedResponse(Guid CaseId, string Status, string RowVersion);
