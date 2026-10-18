using System.Security.Cryptography;
using System.Text.Json;
using BuildingBlock.Application.Abstraction.Media;
using BuildingBlock.Domain.Results;
using Wasla.Domain.Common;
using Wasla.Domain.Patients;
using Wasla.Domain.Security;

namespace Wasla.Application.Features.PatientOnboarding;

public sealed partial class PatientIdentityWorkflow
{
    private async Task<Result<object>> SubmitClaimAsync(PatientIdentityCommand command, string fingerprint, CancellationToken ct)
    {
        var p = command.Input; var proof = await ProofAsync(p.EmailVerificationToken, PatientOnboardingAction.Claim, false, ct);
        if (proof is null) return Conflict("PatientOnboarding.TokenInvalidOrExpired");
        var replay = await store.ClaimReplayAsync(proof.NormalizedEmail, command.IdempotencyKey!, ct);
        if (replay is not null) return replay.RequestFingerprint == fingerprint ? ClaimSubmitted(replay) : Conflict("PatientIdentity.IdempotencyKeyReused");
        if (proof.ConsumedOnUtc is not null) return Conflict("PatientOnboarding.TokenInvalidOrExpired");
        if (!ValidProfile(p, Now)) return Invalid();
        if (Age(p.DateOfBirth, Now) < 16) return Result<object>.Fail(PatientIdentityErrors.Age("Patient.SelfRegistrationAgeRestricted"));
        var user = await store.UserByEmailAsync(proof.NormalizedEmail, ct);
        if (user is not null) return Conflict(user.UserType == UserType.Patient ? "Patient.AlreadyLinked" : "Identity.EmailOwnedByAnotherAccount");
        var target = await store.PatientByEmailAsync(proof.NormalizedEmail, ct);
        var reference = p.PatientReference ?? proof.PatientReference;
        if (target is null && reference is { } refId) target = await data.FindPatientByIdAsync(refId, ct);
        if (target is not null && await store.LinkByPatientAsync(target.Id, ct) is not null) return Conflict("Patient.AlreadyLinked");
        if (target is null)
        {
            var candidates = await store.MatchAsync(new(null, p.PhoneNumber, p.NameAr, p.DateOfBirth, p.Gender, OnlyUnlinked: true), 1, 20, true, ct);
            if (candidates.Total == 0) return Conflict("Patient.NoClaimableProfile");
            // Ambiguous soft matches remain unresolved for explicit reviewer selection.
        }
        if (await store.OpenClaimAsync(proof.NormalizedEmail, target?.Id, null, ct)) return Conflict("PatientClaims.PendingAlreadyExists");
        if (!await SufficientSubmissionProofAsync(p, target?.Id, proof.NormalizedEmail, ct)) return Result<object>.Fail(PatientIdentityErrors.Proof);
        var claim = new PatientClaimRequest(Guid.NewGuid(), target?.Id, proof.NormalizedEmail, reference, p.NameAr, p.DateOfBirth,
            p.Gender, p.PhoneNumber, proof.Id, command.IdempotencyKey!, fingerprint, Now);
        data.Add(claim); await AddEvidenceAsync(claim, p, ct);
        History(claim, null, PatientClaimAction.Submitted, null);
        proof.Consume(Now, fingerprint);
        await NotifyClaimAsync(claim, "submitted", ct);
        return ClaimSubmitted(claim);
    }
    private static Result<object> ClaimSubmitted(PatientClaimRequest c) => Ok(new { claimRequestId = c.Id, status = "PendingReview", c.SubmittedOnUtc, canAccessMedicalRecord = false });
    private async Task<bool> SufficientSubmissionProofAsync(PatientIdentityInput p, Guid? target, string email, CancellationToken ct)
    {
        var provided = new List<PatientClaimEvidenceType>();
        if (p.IdentityFront is not null) provided.Add(PatientClaimEvidenceType.GovernmentIdFront);
        if (p.IdentityBack is not null) provided.Add(PatientClaimEvidenceType.GovernmentIdBack);
        if (p.BirthCertificate is not null) provided.Add(PatientClaimEvidenceType.BirthCertificate);
        if (p.GuardianRelationshipDocument is not null) provided.Add(PatientClaimEvidenceType.GuardianRelationshipEvidence);
        if (p.InPersonIdentityAttestationId is not null) provided.Add(PatientClaimEvidenceType.InPersonIdentityAttestation);
        if (!provided.Order().SequenceEqual(p.EvidenceTypes.Distinct().Order())) return false;
        var attested = false;
        if (p.InPersonIdentityAttestationId is { } id)
        {
            var a = await store.AttestationAsync(id, ct);
            attested = a is not null && a.Purpose == PatientIdentityAttestationPurpose.ClaimProof && a.PatientId == target &&
                a.VerifiedClaimantNormalizedEmail == email && a.ExpiresOnUtc > Now &&
                await data.HasReceptionPracticeAccessAsync(a.ReceptionApplicationUserId, a.DoctorPracticeId, PermissionNames.PatientsRegistrationMatch, ct);
            if (!attested) return false;
        }
        var officialIdentity = p.IdentityFront is not null;
        return Age(p.DateOfBirth, Now) >= 18 ? officialIdentity || attested :
            (officialIdentity || p.BirthCertificate is not null) && (officialIdentity || attested);
    }
    private async Task AddEvidenceAsync(PatientClaimRequest claim, PatientIdentityInput p, CancellationToken ct)
    {
        foreach (var (upload, type) in new[] { (p.IdentityFront, PatientClaimEvidenceType.GovernmentIdFront), (p.IdentityBack, PatientClaimEvidenceType.GovernmentIdBack),
            (p.BirthCertificate, PatientClaimEvidenceType.BirthCertificate), (p.GuardianRelationshipDocument, PatientClaimEvidenceType.GuardianRelationshipEvidence) })
        {
            if (upload is null) continue;
            // Stream through the existing validator/storage; its configured size, signature and scanner rules are authoritative.
            var hash = await PatientIdentityUploadFingerprint.HashAsync(upload, ct);
            var stored = await media.SaveAsync(upload, new MediaStorageRequest($"PatientClaims/{claim.Id:N}/Evidence/{claim.EvidenceRevision}"), ct);
            compensation.Track(stored.Key);
            data.Add(new PatientClaimEvidence(Guid.NewGuid(), claim.Id, claim.EvidenceRevision, type, stored.Key, stored.ContentType,
                stored.Length, hash, PatientEvidenceUploaderType.Applicant, null, Now));
        }
        if (p.InPersonIdentityAttestationId is { } attestationId)
        {
            var a = (await store.AttestationAsync(attestationId, ct))!;
            data.Add(new PatientClaimEvidence(Guid.NewGuid(), claim.Id, claim.EvidenceRevision, PatientClaimEvidenceType.InPersonIdentityAttestation,
                null, null, null, null, PatientEvidenceUploaderType.Reception, a.ReceptionApplicationUserId, Now, attestationId));
        }
    }
    private async Task<Result<object>> ResubmitClaimAsync(PatientIdentityCommand command, string fingerprint, CancellationToken ct)
    {
        var proof = await ProofAsync(command.Token, PatientOnboardingAction.Claim, false, ct);
        var claim = command.Id is { } id ? await store.ClaimAsync(id, ct) : null;
        if (proof is null || claim is null || proof.NormalizedEmail != claim.ClaimantNormalizedEmail) return Result<object>.Fail(PatientIdentityErrors.NotFound);
        var previous = await store.MutationReplayAsync(proof.NormalizedEmail, command.IdempotencyKey!, ct);
        if (previous is not null) return previous.IdempotencyFingerprint == fingerprint
            ? Ok(JsonSerializer.Deserialize<JsonElement>(previous.MutationResponseJson!)) : Conflict("PatientIdentity.IdempotencyKeyReused");
        if (proof.ConsumedOnUtc is not null) return proof.MutationIdempotencyKey == command.IdempotencyKey && proof.IdempotencyFingerprint == fingerprint && proof.MutationResponseJson is not null
            ? Ok(JsonSerializer.Deserialize<JsonElement>(proof.MutationResponseJson)) : Conflict("PatientOnboarding.TokenInvalidOrExpired");
        if (claim.Status != PatientClaimStatus.ModificationRequested || Now >= claim.LastActivityOnUtc.AddDays(Options.ClaimInactivityExpiryDays)) return Conflict("PatientClaims.InvalidState");
        if (!MatchesVersion(claim.RowVersion, command.Input.RowVersion)) return Conflict("PatientClaims.ConcurrencyConflict");
        // Corrections cannot silently change verified email or approved target. Applicant demographics stay as originally attested.
        if (command.Input.IdentityProfileCorrections is { } corrections)
        {
            var corrected = claim.CorrectApplicant(corrections.NameAr, corrections.DateOfBirth, corrections.Gender, corrections.PhoneNumber, Now);
            if (corrected.IsFailure) return Result<object>.Fail(corrected.Errors);
        }
        if (Age(claim.ApplicantDateOfBirth, Now) < 16) return Result<object>.Fail(PatientIdentityErrors.Age("Patient.SelfRegistrationAgeRestricted"));
        command.Input.DateOfBirth = claim.ApplicantDateOfBirth;
        if (!await SufficientSubmissionProofAsync(command.Input, claim.TargetPatientId, claim.ClaimantNormalizedEmail, ct)) return Result<object>.Fail(PatientIdentityErrors.Proof);
        var from = claim.Status; var result = claim.Resubmit(Now); if (result.IsFailure) return Result<object>.Fail(result.Errors);
        await AddEvidenceAsync(claim, command.Input, ct); History(claim, from, PatientClaimAction.Resubmitted, null, command.Input.Reason);
        proof.Consume(Now, fingerprint); await NotifyClaimAsync(claim, "resubmitted", ct);
        await data.SaveChangesAsync(ct);
        var response = new { claimRequestId = claim.Id, status = "PendingReview", revisionNumber = claim.EvidenceRevision, rowVersion = Convert.ToBase64String(claim.RowVersion) };
        proof.CompleteMutation(command.IdempotencyKey!, JsonSerializer.Serialize(response, StoredJson)); return Ok(response);
    }
    private async Task<Result<object>> DecideAsync(PatientIdentityCommand command, CancellationToken ct)
    {
        var claim = command.Id is { } id ? await store.ClaimAsync(id, ct) : null;
        if (claim is null) return Result<object>.Fail(PatientIdentityErrors.NotFound);
        if (!MatchesVersion(claim.RowVersion, command.Input.RowVersion)) return Conflict("PatientClaims.ConcurrencyConflict");
        if (claim.Status != PatientClaimStatus.PendingReview || Now >= claim.LastActivityOnUtc.AddDays(Options.ClaimInactivityExpiryDays)) return Conflict("PatientClaims.InvalidState");
        var p = command.Input;
        if (command.Operation == PatientIdentityMutation.Approve)
        {
            var target = await data.FindPatientByIdAsync(p.TargetPatientId, ct);
            if (target is null) return Result<object>.Fail(PatientIdentityErrors.NotFound);
            if (await store.LinkByPatientAsync(target.Id, ct) is not null) return Conflict("Patient.AlreadyLinked");
            if (target.Email is not null && target.Email != claim.ClaimantNormalizedEmail) return Conflict("Patient.IdentityConflict");
            if (target.DateOfBirth != claim.ApplicantDateOfBirth || target.Gender != claim.ApplicantGender || Age(target.DateOfBirth, Now) < 16 ||
                claim.PatientReference is { } reference && reference != target.Id && string.IsNullOrWhiteSpace(p.Reason)) return Result<object>.Fail(PatientIdentityErrors.Proof);
            var existingAccount = await store.UserByEmailAsync(claim.ClaimantNormalizedEmail, ct);
            if (existingAccount is not null) return Conflict(existingAccount.UserType == UserType.Patient
                ? "Identity.AlreadyRegistered" : "Identity.EmailOwnedByAnotherAccount");
            var emailPatient = await store.PatientByEmailAsync(claim.ClaimantNormalizedEmail, ct);
            if (emailPatient is not null && emailPatient.Id != target.Id) return Conflict("Patient.IdentityConflict");
            if (await store.OpenClaimAsync(claim.ClaimantNormalizedEmail, target.Id, claim.Id, ct)) return Conflict("PatientClaims.PendingAlreadyExists");
            var evidence = (await store.EvidenceAsync(claim.Id, ct)).Where(e => e.RevisionNumber == claim.EvidenceRevision && e.RetiredOnUtc is null && e.PurgedOnUtc is null).ToArray();
            var reviewed = evidence.Where(e => p.ReviewedEvidenceIds.Contains(e.Id)).ToArray();
            if (reviewed.Length == 0 || p.ReviewedEvidenceIds.Distinct().Count() != reviewed.Length) return Result<object>.Fail(PatientIdentityErrors.Proof);
            var official = reviewed.Any(e => e.EvidenceType == PatientClaimEvidenceType.GovernmentIdFront);
            var attested = false;
            foreach (var e in reviewed.Where(e => e.IdentityAttestationId.HasValue))
            {
                var a = await store.AttestationAsync(e.IdentityAttestationId!.Value, ct);
                attested |= a is not null && a.Purpose == PatientIdentityAttestationPurpose.ClaimProof && a.PatientId == target.Id &&
                    a.VerifiedClaimantNormalizedEmail == claim.ClaimantNormalizedEmail && a.ExpiresOnUtc > Now &&
                    await data.HasReceptionPracticeAccessAsync(a.ReceptionApplicationUserId, a.DoctorPracticeId, PermissionNames.PatientsRegistrationMatch, ct);
            }
            if (!(official || attested) || Age(target.DateOfBirth, Now) < 18 && !(official || reviewed.Any(e => e.EvidenceType == PatientClaimEvidenceType.BirthCertificate))) return Result<object>.Fail(PatientIdentityErrors.Proof);
            var changed = claim.Decide(PatientClaimStatus.ApprovedPendingActivation, Actor, p.Reason, target.Id, Now);
            if (changed.IsFailure) return Result<object>.Fail(changed.Errors);
            History(claim, PatientClaimStatus.PendingReview, PatientClaimAction.Approved, Actor, p.Reason,
                JsonSerializer.Serialize(new { reviewedEvidenceIds = p.ReviewedEvidenceIds, targetPatientId = target.Id }));
            var invitation = await InviteAsync(PatientActivationPurpose.ApprovedClaim, null, claim.Id, claim.ClaimantNormalizedEmail, ct);
            await data.SaveChangesAsync(ct);
            return Ok(new { claimRequestId = claim.Id, status = "ApprovedPendingActivation", targetPatientId = target.Id, rowVersion = Convert.ToBase64String(claim.RowVersion), invitationExpiresOnUtc = invitation.ExpiresOnUtc });
        }
        var status = command.Operation == PatientIdentityMutation.Reject ? PatientClaimStatus.Rejected : PatientClaimStatus.ModificationRequested;
        var decision = claim.Decide(status, Actor, p.Reason, null, Now); if (decision.IsFailure) return Result<object>.Fail(decision.Errors);
        History(claim, PatientClaimStatus.PendingReview, status == PatientClaimStatus.Rejected ? PatientClaimAction.Rejected : PatientClaimAction.ModificationRequested, Actor, p.Reason);
        await NotifyClaimAsync(claim, status.ToString(), ct); await data.SaveChangesAsync(ct);
        return Ok(new { claimRequestId = claim.Id, status = status.ToString(), claim.ClosedOnUtc, rowVersion = Convert.ToBase64String(claim.RowVersion), canResubmit = status == PatientClaimStatus.ModificationRequested });
    }
    private async Task<Result<object>> ActivateAsync(PatientIdentityInput p, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(p.InvitationToken) || p.InvitationToken.Length > 500) return Conflict("PatientActivation.InvalidOrExpired");
        var invitation = await store.InvitationAsync(InvitationHash(p.InvitationToken), ct);
        if (invitation is null || !invitation.CanActivate(Now)) return Conflict("PatientActivation.InvalidOrExpired");
        if (p.Password is null || p.Password != p.ConfirmPassword || !passwords.IsStrongPassword(p.Password)) return Invalid();
        Patient? patient; ApplicationUser? user; PatientClaimRequest? claim = null;
        if (invitation.Purpose == PatientActivationPurpose.ReceptionFull)
        {
            user = invitation.ApplicationUserId is { } userId ? await data.FindUserByIdAsync(userId, ct) : null;
            var link = user is null ? null : await data.FindPatientAccountLinkAsync(user.Id, ct);
            patient = link is null ? null : await data.FindPatientByIdAsync(link.PatientId, ct);
            if (user is not { UserType: UserType.Patient, IsActive: false, IsFirstLogin: true } || patient is null || user.Email != invitation.NormalizedEmail ||
                patient.Email != invitation.NormalizedEmail || link?.Source != PatientAccountLinkSource.ReceptionFullRegistration || Age(patient.DateOfBirth, Now) < 18) return Conflict("PatientActivation.InvalidOrExpired");
        }
        else
        {
            claim = invitation.ClaimRequestId is { } claimId ? await store.ClaimAsync(claimId, ct) : null;
            if (claim is not { Status: PatientClaimStatus.ApprovedPendingActivation, ApprovedTargetPatientId: not null } ||
                Now >= claim.LastActivityOnUtc.AddDays(Options.ClaimInactivityExpiryDays) || claim.ClaimantNormalizedEmail != invitation.NormalizedEmail) return Conflict("PatientActivation.InvalidOrExpired");
            patient = await data.FindPatientByIdAsync(claim.ApprovedTargetPatientId.Value, ct);
            if (patient is null || patient.Email is not null && patient.Email != invitation.NormalizedEmail || Age(patient.DateOfBirth, Now) < 16) return Conflict("Patient.IdentityConflict");
            if (await store.LinkByPatientAsync(patient.Id, ct) is not null) return Conflict("Patient.AlreadyLinked");
            var existing = await store.UserByEmailAsync(invitation.NormalizedEmail, ct);
            if (existing is not null) return Conflict(existing.UserType == UserType.Patient ? "Identity.AlreadyRegistered" : "Identity.EmailOwnedByAnotherAccount");
            var emailPatient = await store.PatientByEmailAsync(invitation.NormalizedEmail, ct);
            if (emailPatient is not null && emailPatient.Id != patient.Id) return Conflict("Patient.IdentityConflict");
            var role = await data.FindRoleByNameAsync(SystemRoleNames.Patient, ct); if (role is null) return Conflict("Identity.SecurityDataMissing");
            var name = await InternalUserNameAsync(ct); if (name is null) return Conflict("Identity.UserNameAlreadyExists");
            var id = Guid.NewGuid(); user = ApplicationUser.Create(id, name, invitation.NormalizedEmail,
                patient.PhoneNumber, await passwords.HashAsync(p.Password, ct), UserType.Patient, false, Now).Value;
            var update = patient.UpdateProfile(patient.NameAr, patient.NameEn, patient.PhoneNumber, invitation.NormalizedEmail);
            if (update.IsFailure) return Invalid();
            data.Add(user); data.Add(new UserRole(Guid.NewGuid(), user.Id, role.Id));
            data.Add(PatientAccountLink.Create(Guid.NewGuid(), user.Id, patient.Id, PatientAccountLinkSource.PatientClaim, Now).Value);
            var linked = claim.Link(user.Id, Now); if (linked.IsFailure) return Result<object>.Fail(linked.Errors);
            History(claim, PatientClaimStatus.ApprovedPendingActivation, PatientClaimAction.Activated, null);
        }
        var passwordHash = await passwords.HashAsync(p.Password, ct); var activatedAt = Now;
        if (!invitation.CanActivate(activatedAt)) return Conflict("PatientActivation.InvalidOrExpired");
        user.ChangePassword(passwordHash, activatedAt); user.Activate(); invitation.Consume(activatedAt);
        return Ok(new { patientId = patient.Id, applicationUserId = user.Id, accountStatus = "Active", requiresLogin = true });
    }
    private async Task<Result<object>> ResendAsync(PatientIdentityCommand command, CancellationToken ct)
    {
        var p = command.Input; var email = PatientIdentityEmail.Normalize(p.Email);
        if (email is null || !PatientIdentityEmail.IsValid(email)) return Invalid();
        if (await store.HasResendAsync(command.IdempotencyKey!, ct)) return AcceptedResend();
        Guid? userId = null; PatientClaimRequest? claim = null;
        if (command.Id is { } claimId)
        {
            var proof = await ProofAsync(p.VerificationToken, PatientOnboardingAction.Claim, false, ct);
            claim = await store.ClaimAsync(claimId, ct);
            if (proof is null || proof.NormalizedEmail != email || claim is not { Status: PatientClaimStatus.ApprovedPendingActivation } ||
                claim.ClaimantNormalizedEmail != email || Now >= claim.LastActivityOnUtc.AddDays(Options.ClaimInactivityExpiryDays)) return AcceptedResend();
        }
        else
        {
            var user = await store.UserByEmailAsync(email, ct);
            var link = user is null ? null : await data.FindPatientAccountLinkAsync(user.Id, ct);
            if (user is not { UserType: UserType.Patient, IsActive: false, IsFirstLogin: true } || link?.Source != PatientAccountLinkSource.ReceptionFullRegistration) return AcceptedResend();
            userId = user.Id;
        }
        var active = await store.LiveInvitationsAsync(claim?.Id, userId, ct);
        if (active.Any(i => i.CreatedOnUtc.AddSeconds(Options.EmailOtpResendCooldownSeconds) > Now)) return AcceptedResend();
        foreach (var old in active) old.Revoke(Now);
        await data.SaveChangesAsync(ct); // Retire the unique live slot inside this same business transaction.
        var resent = await InviteAsync(claim is null ? PatientActivationPurpose.ReceptionFull : PatientActivationPurpose.ApprovedClaim, userId, claim?.Id, email, ct, active.Select(i => i.SendCount).DefaultIfEmpty(0).Max() + 1);
        resent.StampResend(command.IdempotencyKey!);
        claim?.InvitationResent(Now); return AcceptedResend();
    }
    private static Result<object> AcceptedResend() => Ok(new { message = Wasla.Domain.Resources.ErrorMessage.PatientIdentityResendAccepted });
    private void History(PatientClaimRequest claim, PatientClaimStatus? from, PatientClaimAction action, Guid? actor, string? reason = null, string? metadata = null) =>
        data.Add(new PatientClaimHistory(Guid.NewGuid(), claim.Id, claim.EvidenceRevision, from, claim.Status, action, actor, Now, reason, metadata));
    private Task NotifyClaimAsync(PatientClaimRequest claim, string action, CancellationToken ct) => MailAsync($"patient-claim-decision:{claim.Id:N}:{claim.EvidenceRevision}:{action}",
        claim.ClaimantNormalizedEmail, "تحديث طلب إثبات الهوية / Identity claim update", "تم تحديث طلب إثبات هويتك. افتح حالة الطلب باستخدام التحقق من البريد.\nYour identity claim was updated. Verify your email to view its status.", ct);
    private static bool MatchesVersion(byte[] actual, string? supplied)
    {
        try { return supplied is not null && Convert.FromBase64String(supplied).AsSpan().SequenceEqual(actual); } catch (FormatException) { return false; }
    }
    private async Task<Result<object>> ResolveDuplicateAsync(PatientIdentityCommand command, CancellationToken ct)
    {
        var item = command.Id is { } id ? await store.DuplicateAsync(id, ct) : null;
        if (item is null) return Result<object>.Fail(PatientIdentityErrors.NotFound);
        if (!MatchesVersion(item.RowVersion, command.Input.RowVersion)) return Conflict("PatientClaims.ConcurrencyConflict");
        var result = item.Resolve(command.Input.Resolution, Actor, command.Input.Reason ?? "", Now);
        if (result.IsFailure) return Result<object>.Fail(result.Errors);
        data.Add(new Wasla.Domain.Archives.ArchiveAuditEvent("PatientDuplicateResolved", "PatientDuplicateCase", item.Id, null, Actor, Now,
            reasonHash: Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(command.Input.Reason ?? ""))), outcome: item.Status.ToString()));
        await data.SaveChangesAsync(ct); return Ok(new { caseId = item.Id, status = item.Status.ToString(), rowVersion = Convert.ToBase64String(item.RowVersion) });
    }
}
