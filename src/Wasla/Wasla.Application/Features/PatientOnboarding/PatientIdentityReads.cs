using BuildingBlock.Domain.Results;
using Wasla.Application.Persistence;
using Wasla.Domain.Archives;
using Wasla.Domain.Patients;
using Wasla.Domain.Security;

namespace Wasla.Application.Features.PatientOnboarding;

public sealed partial class PatientIdentityWorkflow
{
    public async Task<Result<object>> Handle(PatientIdentityQuery query, CancellationToken cancellationToken)
    {
        var p = query.Input;
        if (p.PageNumber < 1 || p.PageSize is < 1 or > 100) return Invalid();
        var permission = query.Operation switch { PatientIdentityRead.Claims => PermissionNames.PatientClaimsViewAll,
            PatientIdentityRead.ClaimDetails or PatientIdentityRead.ClaimHistory => PermissionNames.PatientClaimsViewDetails,
            PatientIdentityRead.ClaimEvidence => PermissionNames.PatientClaimsViewEvidence,
            PatientIdentityRead.Duplicates or PatientIdentityRead.DuplicateDetails => PermissionNames.PatientDuplicatesView, _ => null };
        if (permission is not null) { var access = await AdminAsync(permission, cancellationToken); if (access.IsFailure) return Result<object>.Fail(access.Errors); }
        if (query.Operation == PatientIdentityRead.Matches)
        {
            var access = await reception.AuthorizeAsync(p.DoctorPracticeId, PermissionNames.PatientsRegistrationMatch, cancellationToken);
            if (access.IsFailure) return Result<object>.Fail(access.Errors);
            if (!BoundedSearch(p.Email, p.PhoneNumber, p.NameAr, p.DateOfBirth == default ? null : p.DateOfBirth) || p.PageSize > 20) return Invalid();
            var exact = PatientIdentityEmail.Normalize(p.Email) is { } email ? await store.PatientByEmailAsync(email, cancellationToken) : null;
            var matches = exact is not null ? new List<Patient> { exact } :
                (await store.MatchAsync(new(p.Email, p.PhoneNumber, p.NameAr, p.DateOfBirth == default ? null : p.DateOfBirth, p.Gender == default ? null : p.Gender), 1, p.PageSize, true, cancellationToken)).Items;
            var candidates = new List<object>(); var linked = false;
            foreach (var candidate in matches)
            {
                var hasLink = await store.LinkByPatientAsync(candidate.Id, cancellationToken) is not null; linked |= hasLink;
                candidates.Add(new { patientId = candidate.Id, candidate.NameAr, candidate.DateOfBirth, candidate.Gender,
                    emailMasked = PatientIdentityEmail.Mask(candidate.Email), phoneMasked = PatientIdentityEmail.MaskPhone(candidate.PhoneNumber), isLinkedAccount = hasLink });
            }
            var user = PatientIdentityEmail.Normalize(p.Email) is { } requestedEmail ? await store.UserByEmailAsync(requestedEmail, cancellationToken) : null;
            var conflict = user is not null && user.UserType != Wasla.Domain.Common.UserType.Patient;
            var outcome = conflict ? "IdentityConflict" : exact is not null && linked ? "AlreadyLinked" : matches.Count > 1 ? "MultipleMatches" : matches.Count == 1 ? "PossibleMatch" : "NoMatch";
            data.Add(new ArchiveAuditEvent("PatientRegistrationMatch", "PatientIdentity", null, p.DoctorPracticeId, Actor, Now)); await data.SaveChangesAsync(cancellationToken);
            return Ok(new { matchOutcome = outcome, candidates, canCreateBasic = exact is null, canCreateFull = exact is null && user is null, requiresClaimForAccount = exact is not null && !linked });
        }
        if (query.Operation == PatientIdentityRead.OwnClaim)
        {
            var proof = string.IsNullOrWhiteSpace(query.Token) ? null : await store.ContinuationAsync(ContinuationHash(query.Token), cancellationToken);
            var claim = query.Id is { } id ? await store.ClaimAsync(id, cancellationToken) : null;
            if (proof is null || proof.Purpose == PatientOnboardingPurpose.EmailChange || proof.InvalidatedOnUtc is not null || proof.VerifiedOnUtc is null || proof.ContinuationTokenExpiresOnUtc <= Now ||
                claim is null || proof.NormalizedEmail != claim.ClaimantNormalizedEmail) return Result<object>.Fail(PatientIdentityErrors.NotFound);
            return Ok(new { claimRequestId = claim.Id, status = claim.Status.ToString(), claim.SubmittedOnUtc, claim.LastActivityOnUtc,
                reasonForApplicant = claim.Status is PatientClaimStatus.ModificationRequested or PatientClaimStatus.Rejected ? claim.DecisionReason : null,
                canResubmit = claim.Status == PatientClaimStatus.ModificationRequested && Now < claim.LastActivityOnUtc.AddDays(Options.ClaimInactivityExpiryDays),
                canActivate = claim.Status == PatientClaimStatus.ApprovedPendingActivation && Now < claim.LastActivityOnUtc.AddDays(Options.ClaimInactivityExpiryDays),
                activationInvitationSent = (await store.LiveInvitationsAsync(claim.Id, null, cancellationToken)).Count > 0, claim.ClosedOnUtc,
                rowVersion = Convert.ToBase64String(claim.RowVersion) });
        }
        if (query.Operation == PatientIdentityRead.Claims)
        {
            var page = await store.ClaimsAsync(p.Status, p.FromUtc, p.ToUtc, PatientIdentityEmail.Normalize(p.Email), p.PageNumber, p.PageSize, cancellationToken);
            var items = new List<object>();
            foreach (var c in page.Items) items.Add(new { id = c.Id, status = c.Status.ToString(), claimantEmailMasked = PatientIdentityEmail.Mask(c.ClaimantNormalizedEmail), c.SubmittedOnUtc, c.LastActivityOnUtc,
                evidenceCount = (await store.EvidenceAsync(c.Id, cancellationToken)).Count, rowVersion = Convert.ToBase64String(c.RowVersion) });
            return Ok(new { items, totalCount = page.Total, p.PageNumber, p.PageSize });
        }
        if (query.Operation is PatientIdentityRead.ClaimDetails or PatientIdentityRead.ClaimHistory or PatientIdentityRead.ClaimEvidence)
        {
            var c = query.Id is { } id ? await store.ClaimAsync(id, cancellationToken) : null;
            if (c is null) return Result<object>.Fail(PatientIdentityErrors.NotFound);
            var history = await store.HistoryAsync(c.Id, cancellationToken);
            var safeHistory = history.Select(h => new { eventId = h.Id, h.RevisionNumber, action = h.Action.ToString(), fromStatus = h.FromStatus?.ToString(), toStatus = h.ToStatus.ToString(), actorId = h.ActorApplicationUserId, h.OccurredOnUtc, h.Reason }).ToArray();
            if (query.Operation == PatientIdentityRead.ClaimHistory) return Ok(new { items = safeHistory });
            var evidence = await store.EvidenceAsync(c.Id, cancellationToken);
            if (query.Operation == PatientIdentityRead.ClaimEvidence)
            {
                var e = evidence.SingleOrDefault(e => e.Id == query.EvidenceId && e.RetiredOnUtc is null && e.PurgedOnUtc is null && e.PrivateMediaKey is not null);
                if (e is null) return Result<object>.Fail(PatientIdentityErrors.NotFound);
                var file = await privateMedia.OpenAsync(e.PrivateMediaKey!, cancellationToken);
                if (file is null) return Result<object>.Fail(PatientIdentityErrors.NotFound);
                data.Add(new ArchiveAuditEvent("PatientClaimEvidenceRead", "PatientClaimEvidence", e.Id, null, Actor, Now)); await data.SaveChangesAsync(cancellationToken);
                return Ok(file with { FileName = "identity-evidence" + Path.GetExtension(file.FileName) });
            }
            IReadOnlyList<Patient> candidates;
            if (c.TargetPatientId is { } target) { var patient = await data.FindPatientByIdAsync(target, cancellationToken); candidates = patient is null ? [] : [patient]; }
            else candidates = (await store.MatchAsync(new(c.ClaimantNormalizedEmail, c.ApplicantPhoneNumber, c.ApplicantNameAr, c.ApplicantDateOfBirth, c.ApplicantGender, OnlyUnlinked: true), 1, 20, true, cancellationToken)).Items;
            var access = await data.GetAccessSnapshotAsync(Actor, cancellationToken); var pending = c.Status == PatientClaimStatus.PendingReview && Now < c.LastActivityOnUtc.AddDays(Options.ClaimInactivityExpiryDays);
            return Ok(new { claimRequestId = c.Id, status = c.Status.ToString(), claimant = new { nameAr = c.ApplicantNameAr, dateOfBirth = c.ApplicantDateOfBirth, gender = c.ApplicantGender,
                email = c.ClaimantNormalizedEmail, phoneMasked = PatientIdentityEmail.MaskPhone(c.ApplicantPhoneNumber) }, c.PatientReference,
                candidatePatients = candidates.Select(x => new { patientId = x.Id, x.NameAr, x.DateOfBirth, x.Gender, emailMasked = PatientIdentityEmail.Mask(x.Email) }),
                evidence = evidence.Where(e => e.PurgedOnUtc is null).Select(e => new { evidenceId = e.Id, type = e.EvidenceType.ToString(), e.RevisionNumber, e.UploadedOnUtc }),
                historySummary = safeHistory, c.SubmittedOnUtc, c.LastActivityOnUtc, rowVersion = Convert.ToBase64String(c.RowVersion),
                reviewCapabilities = new { canApprove = pending && access!.Permissions.Contains(PermissionNames.PatientClaimsApprove), canReject = pending && access!.Permissions.Contains(PermissionNames.PatientClaimsReject), canRequestModification = pending && access!.Permissions.Contains(PermissionNames.PatientClaimsRequestModification) } });
        }
        if (query.Operation == PatientIdentityRead.Duplicates)
        {
            var page = await store.DuplicatesAsync(p.DuplicateStatus, p.PageNumber, p.PageSize, cancellationToken);
            return Ok(new { items = page.Items.Select(x => new { caseId = x.Id, x.FirstPatientId, x.SecondPatientId, status = x.Status.ToString(), x.CreatedOnUtc, rowVersion = Convert.ToBase64String(x.RowVersion) }), totalCount = page.Total, p.PageNumber, p.PageSize });
        }
        if (query.Operation == PatientIdentityRead.DuplicateDetails)
        {
            var item = query.Id is { } id ? await store.DuplicateAsync(id, cancellationToken) : null;
            if (item is null) return Result<object>.Fail(PatientIdentityErrors.NotFound);
            var a = await data.FindPatientByIdAsync(item.FirstPatientId, cancellationToken); var b = await data.FindPatientByIdAsync(item.SecondPatientId, cancellationToken);
            return Ok(new { caseId = item.Id, status = item.Status.ToString(), patientA = Candidate(a!), patientB = Candidate(b!), item.MatchIndicators, item.CreatedOnUtc, rowVersion = Convert.ToBase64String(item.RowVersion) });
        }
        return Invalid();
    }
    private static object Candidate(Patient p) => new { p.Id, p.NameAr, p.DateOfBirth, p.Gender, emailMasked = PatientIdentityEmail.Mask(p.Email) };
    public static bool BoundedSearch(string? email, string? phone, string? name, DateOnly? dob) =>
        PatientIdentityEmail.Normalize(email) is { } normalized && PatientIdentityEmail.IsValid(normalized) ||
        phone?.Trim().Count(char.IsAsciiDigit) >= 7 || name?.Trim().Length >= 3 && dob.HasValue;
}
