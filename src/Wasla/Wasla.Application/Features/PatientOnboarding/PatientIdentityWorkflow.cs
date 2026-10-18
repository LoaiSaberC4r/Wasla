using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Encryption;
using BuildingBlock.Application.Abstraction.Media;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using Microsoft.Extensions.Options;
using Wasla.Application.Email;
using Wasla.Application.Features.Practices;
using Wasla.Application.Media;
using Wasla.Application.Persistence;
using Wasla.Application.Security;
using Wasla.Domain.Common;
using Wasla.Domain.Medications;
using Wasla.Domain.Patients;
using Wasla.Domain.Resources;
using Wasla.Domain.Security;

namespace Wasla.Application.Features.PatientOnboarding;

public sealed record VerifiedPatientRegistrationResponse(Guid PatientId, Guid ApplicationUserId, string AccountStatus = "Active");
public sealed record ReceptionFullRegistrationResponse(Guid PatientId, Guid ApplicationUserId, string ProfileType, string AccountStatus, string ActivationEmailMasked, DateTime InvitationExpiresOnUtc);
public sealed record PatientIdentityAttestationResponse(Guid PatientId, Guid AttestationId, DateTime? ExpiresOnUtc);

public sealed partial class PatientIdentityWorkflow(IWaslaDataStore data, IPatientIdentityStore store, ICurrentUser current,
    IReceptionPracticeAuthorizationService reception, IPasswordResetProtector protector, IPasswordService passwords,
    IMediaService media, IPrivateMediaReader privateMedia, IEmailOutbox outbox, IDateTimeProvider clock,
    IOptions<PatientIdentityOptions> options, IPatientIdentityMediaCompensation compensation)
    : ICommandHandler<PatientIdentityCommand, object>, IQueryHandler<PatientIdentityQuery, object>
{
    private static readonly System.Diagnostics.Metrics.Meter IdentityMeter = new("Wasla.PatientIdentity.Onboarding");
    private static readonly System.Diagnostics.Metrics.Counter<long> IdentityOperations = IdentityMeter.CreateCounter<long>("patient_identity_operations");
    private static readonly JsonSerializerOptions StoredJson = new(JsonSerializerDefaults.Web);
    private PatientIdentityOptions Options => options.Value;
    private DateTime Now => clock.UtcNow;
    private Guid Actor => current.UserId ?? Guid.Empty;
    private static Result<object> Ok(object value) => Result<object>.Ok(value);
    private static Result<object> Conflict(string code) => Result<object>.Fail(PatientIdentityErrors.Conflict(code));
    private static Result<object> Invalid() => Result<object>.Fail(PatientIdentityErrors.Invalid);
    public string ContinuationHash(string token) => protector.HashResetToken("patient-identity:continuation:" + token);
    private string InvitationHash(string token) => protector.HashResetToken("patient-identity:activation:" + token);
    private static string OtpScope(PatientOnboardingChallenge c, string otp) => $"patient-identity:otp:{c.Id:N}:{(int)c.Purpose}:{c.NormalizedEmail}:{otp}";
    private static bool IsUuid(string? key) => key is { Length: <= 100 } && Guid.TryParse(key, out _);
    private static bool ValidProfile(PatientIdentityInput p, DateTime now) => !string.IsNullOrWhiteSpace(p.NameAr) && p.NameAr.Trim().Length <= 200 &&
        p.NameEn?.Length is not > 200 && p.DateOfBirth != default && p.DateOfBirth <= DateOnly.FromDateTime(now) && p.Gender is Gender.Male or Gender.Female &&
        p.PhoneNumber?.Length is not > 30 && PatientIdentityEmail.IsValid(PatientIdentityEmail.Normalize(p.Email));
    private static int Age(DateOnly dob, DateTime now) { var today = DateOnly.FromDateTime(now); var age = today.Year - dob.Year; return dob > today.AddYears(-age) ? age - 1 : age; }
    public async Task<Result<object>> Handle(PatientIdentityCommand command, CancellationToken cancellationToken)
    {
        if (command.Operation == PatientIdentityMutation.Register && string.IsNullOrWhiteSpace(command.Input?.EmailVerificationToken)) return Conflict("PatientOnboarding.VerificationRequired");
        if (!Enum.IsDefined(command.Operation) || command.Input is null) return Invalid();
        if (command.Operation is not (PatientIdentityMutation.Start or PatientIdentityMutation.Verify or PatientIdentityMutation.Activate) && !IsUuid(command.IdempotencyKey)) return Invalid();
        await store.LockAsync(cancellationToken);
        var p = command.Input;
        // Each mutation performs live authorization before looking up a replay.
        var permission = command.Operation switch { PatientIdentityMutation.Full => PermissionNames.PatientsRegisterFull,
            PatientIdentityMutation.ConfirmReuse or PatientIdentityMutation.ConfirmClaimIdentity => PermissionNames.PatientsRegistrationMatch,
            PatientIdentityMutation.Approve => PermissionNames.PatientClaimsApprove, PatientIdentityMutation.Reject => PermissionNames.PatientClaimsReject,
            PatientIdentityMutation.Modify => PermissionNames.PatientClaimsRequestModification, PatientIdentityMutation.ResolveDuplicate => PermissionNames.PatientDuplicatesResolve, _ => null };
        if (permission is not null)
        {
            var access = permission.StartsWith("Patients.", StringComparison.Ordinal) ? await reception.AuthorizeAsync(p.DoctorPracticeId, permission, cancellationToken) : await AdminAsync(permission, cancellationToken);
            if (access.IsFailure) return Result<object>.Fail(access.Errors);
        }
        var fingerprint = await FingerprintAsync(command, cancellationToken);
        var memoOperation = "PatientIdentity:" + command.Operation;
        var useMemo = permission is not null;
        if (useMemo)
        {
            var previous = await store.OperationAsync(Actor, memoOperation, command.IdempotencyKey!, cancellationToken);
            if (previous is not null) return previous.RequestFingerprint == fingerprint && previous.ResponseJson is not null
                ? Ok(JsonSerializer.Deserialize<JsonElement>(previous.ResponseJson)) : Conflict("PatientIdentity.IdempotencyKeyReused");
        }
        var result = command.Operation switch
        {
            PatientIdentityMutation.Start => await StartAsync(p, cancellationToken), PatientIdentityMutation.Verify => await VerifyAsync(p, cancellationToken),
            PatientIdentityMutation.Register => await RegisterAsync(command, fingerprint, cancellationToken), PatientIdentityMutation.Full => await FullAsync(p, cancellationToken),
            PatientIdentityMutation.ConfirmReuse => await AttestAsync(p, false, cancellationToken), PatientIdentityMutation.ConfirmClaimIdentity => await AttestAsync(p, true, cancellationToken),
            PatientIdentityMutation.SubmitClaim => await SubmitClaimAsync(command, fingerprint, cancellationToken), PatientIdentityMutation.ResubmitClaim => await ResubmitClaimAsync(command, fingerprint, cancellationToken),
            PatientIdentityMutation.Activate => await ActivateAsync(p, cancellationToken), PatientIdentityMutation.Resend => await ResendAsync(command, cancellationToken),
            PatientIdentityMutation.Approve or PatientIdentityMutation.Reject or PatientIdentityMutation.Modify => await DecideAsync(command, cancellationToken),
            PatientIdentityMutation.ResolveDuplicate => await ResolveDuplicateAsync(command, cancellationToken), _ => Invalid()
        };
        if (result.IsSuccess && useMemo)
        {
            var memo = MedicationIdempotencyRecord.Create(Actor, memoOperation, command.IdempotencyKey!, fingerprint, Now);
            memo.Complete(JsonSerializer.Serialize(result.Value, StoredJson)); data.Add(memo);
        }
        if (result.IsSuccess) await data.SaveChangesAsync(cancellationToken);
        IdentityOperations.Add(1, new KeyValuePair<string, object?>("operation", command.Operation.ToString()),
            new KeyValuePair<string, object?>("outcome", result.IsFailure || result.Value is PatientOtpVerificationFailure ? "Rejected" : "Accepted"));
        return result;
    }
    private async Task<string> FingerprintAsync(PatientIdentityCommand c, CancellationToken ct)
    {
        var p = c.Input;
        var files = new List<object?>();
        foreach (var file in new[] { p.ProfileImage, p.IdentityFront, p.IdentityBack, p.BirthCertificate, p.GuardianRelationshipDocument, p.PersonalIdFrontImage, p.PersonalIdBackImage })
            files.Add(file is null ? null : new { file.FileName, file.ContentType, file.Length, sha256 = await PatientIdentityUploadFingerprint.HashAsync(file, ct) });
        var value = JsonSerializer.Serialize(new { c.Operation, c.Id, p.Email, p.UserName, p.NameAr, p.NameEn, p.DateOfBirth, p.Gender, p.PhoneNumber,
            p.DoctorPracticeId, p.PatientId, p.TargetPatientId, p.VerifiedInPerson, p.VerificationBasis, p.VerificationNote, p.MatchReviewOutcome, p.MatchReviewReason,
            p.PrimaryContactNameAr, p.PrimaryContactNameEn, p.PrimaryContactPhoneNumber, p.PrimaryContactEmail, p.PrimaryContactRelationshipType, p.PrimaryContactIsPrimary, p.PrimaryContactLinkedPatientId, p.EvidenceTypes, p.IdentityProfileCorrections,
            p.PatientReference, p.InPersonIdentityAttestationId, p.RowVersion, p.Reason, p.Resolution, p.ReviewedEvidenceIds,
            Password = protector.HashResetToken("patient-identity:fingerprint:" + (p.Password ?? "")),
            ConfirmPassword = protector.HashResetToken("patient-identity:fingerprint:" + (p.ConfirmPassword ?? "")), Files = files });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
    private async Task<Result> AdminAsync(string permission, CancellationToken cancellationToken)
    {
        var snapshot = Actor == Guid.Empty ? null : await data.GetAccessSnapshotAsync(Actor, cancellationToken);
        return snapshot is { User.IsActive: true, User.IsFirstLogin: false, User.UserType: UserType.SuperAdmin, SuperAdminId: not null } &&
            snapshot.Roles.Contains(SystemRoleNames.SuperAdmin) && snapshot.Permissions.Contains(permission)
            ? Result.Ok() : Result.Fail(PatientIdentityErrors.Denied);
    }
    public async Task<PatientOnboardingChallenge?> ProofAsync(string? token, PatientOnboardingAction action, bool mutation, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 500) return null;
        var c = await store.ContinuationAsync(ContinuationHash(token), cancellationToken);
        return c is not null && c.CanUse(action, Now, mutation) ? c : null;
    }
    private async Task<Result<object>> StartAsync(PatientIdentityInput p, CancellationToken cancellationToken)
    {
        var email = PatientIdentityEmail.Normalize(p.Email);
        if (email is null || !PatientIdentityEmail.IsValid(email) || !Enum.IsDefined(p.Purpose) || p.Purpose != PatientOnboardingPurpose.Claim && p.PatientReference is not null) return Invalid();
        Guid? userId = null;
        if (p.Purpose == PatientOnboardingPurpose.EmailChange)
        {
            var snapshot = Actor == Guid.Empty ? null : await data.GetAccessSnapshotAsync(Actor, cancellationToken);
            if (snapshot is not { User.IsActive: true, User.IsFirstLogin: false, User.UserType: UserType.Patient, PatientId: not null })
                return Result<object>.Fail(Error.Unauthorized("Auth.AuthenticationRequired", ErrorMessage.AuthenticationRequired));
            userId = Actor;
        }
        var id = Guid.NewGuid();
        var recent = await store.RecentChallengesAsync(email, p.Purpose, Now.AddHours(-1), cancellationToken);
        // Same neutral response shape and fresh nonresolving ID under cooldown/ineligible email-change requests.
        if (recent.Count >= 10 || recent.Any(x => x.LastSentOnUtc.AddSeconds(Options.EmailOtpResendCooldownSeconds) > Now)) return Ok(new { requestId = id, message = ErrorMessage.PatientIdentityStartAccepted });
        foreach (var old in recent.Where(x => x.InvalidatedOnUtc is null && x.ConsumedOnUtc is null)) old.Invalidate(Now);
        var otp = protector.GenerateOtp(Options.OtpLength);
        var hash = protector.HashOtp($"patient-identity:otp:{id:N}:{(int)p.Purpose}:{email}:{otp}");
        data.Add(new PatientOnboardingChallenge(id, p.Purpose, email, userId, p.PatientReference, hash, Now, Now.AddMinutes(Options.EmailOtpTtlMinutes), Options.EmailOtpMaxAttempts));
        await MailAsync($"patient-otp:{id:N}", email, "رمز التحقق / Verification code", $"رمز التحقق: {otp}. صالح لمدة {Options.EmailOtpTtlMinutes} دقائق.\nVerification code: {otp}. Valid for {Options.EmailOtpTtlMinutes} minutes.", cancellationToken);
        return Ok(new { requestId = id, message = ErrorMessage.PatientIdentityStartAccepted });
    }
    private async Task<Result<object>> VerifyAsync(PatientIdentityInput p, CancellationToken cancellationToken)
    {
        var c = await store.ChallengeAsync(p.RequestId, cancellationToken);
        if (c is null || !c.CanVerify(Now)) return Ok(new PatientOtpVerificationFailure(c is { AttemptCount: var attempts } && attempts >= c.MaxAttempts
            ? Error.RateLimit("PatientOnboarding.TooManyAttempts", ErrorMessage.PatientIdentityRateLimited, TimeSpan.FromSeconds(60)) : PatientIdentityErrors.Otp));
        if (p.Otp is null || p.Otp.Length != Options.OtpLength || !p.Otp.All(char.IsAsciiDigit) || !protector.VerifyOtp(OtpScope(c, p.Otp), c.OtpHash))
        {
            c.FailedAttempt(Now);
            // Success envelope commits failed-attempt accounting; API maps this internal outcome to ProblemDetails afterwards.
            return Ok(new PatientOtpVerificationFailure(c.AttemptCount >= c.MaxAttempts ? Error.RateLimit("PatientOnboarding.TooManyAttempts", ErrorMessage.PatientIdentityRateLimited, TimeSpan.FromSeconds(60)) : PatientIdentityErrors.Otp));
        }
        var user = await store.UserByEmailAsync(c.NormalizedEmail, cancellationToken); var patient = await store.PatientByEmailAsync(c.NormalizedEmail, cancellationToken);
        var action = user is not null ? user.UserType == UserType.Patient ? PatientOnboardingAction.LoginOrRecovery : PatientOnboardingAction.IdentityConflict :
            patient is not null ? await store.LinkByPatientAsync(patient.Id, cancellationToken) is not null ? PatientOnboardingAction.LoginOrRecovery : PatientOnboardingAction.Claim : PatientOnboardingAction.RegisterNew;
        // Email proof authorizes intake only. N06 evaluates reference, demographics and evidence.
        if (c.Purpose == PatientOnboardingPurpose.Claim && user is null && patient is null)
            action = PatientOnboardingAction.Claim;
        if (c.Purpose == PatientOnboardingPurpose.EmailChange)
        {
            if (c.ApplicationUserId != Actor || (await data.GetAccessSnapshotAsync(Actor, cancellationToken)) is not { User.IsActive: true, User.UserType: UserType.Patient, PatientId: not null })
                return Ok(new PatientOtpVerificationFailure(PatientIdentityErrors.Otp));
            action = PatientOnboardingAction.EmailChange;
        }
        var verifiedAt = Now;
        if (!c.CanVerify(verifiedAt)) return Ok(new PatientOtpVerificationFailure(PatientIdentityErrors.Otp));
        var token = protector.GenerateResetToken(); var expires = verifiedAt.AddMinutes(Options.VerifiedContinuationTtlMinutes);
        c.Verify(ContinuationHash(token), action, verifiedAt, expires);
        return Ok(new { verificationToken = token, expiresOnUtc = expires, nextAction = action == PatientOnboardingAction.Claim ? "ClaimExisting" : action.ToString() });
    }
    private async Task<Result<object>> RegisterAsync(PatientIdentityCommand command, string fingerprint, CancellationToken cancellationToken)
    {
        var p = command.Input;
        var verified = string.IsNullOrWhiteSpace(p.EmailVerificationToken) ? null : await store.ContinuationAsync(ContinuationHash(p.EmailVerificationToken), cancellationToken);
        if (verified is not null && verified.Purpose != PatientOnboardingPurpose.EmailChange && verified.AllowedAction is { } priorAction && verified.CanUse(priorAction, Now, false) && verified.NormalizedEmail == PatientIdentityEmail.Normalize(p.Email))
        {
            var previous = await store.MutationReplayAsync(verified.NormalizedEmail, command.IdempotencyKey!, cancellationToken);
            if (previous is not null) return previous.IdempotencyFingerprint == fingerprint
                ? Ok(JsonSerializer.Deserialize<JsonElement>(previous.MutationResponseJson!)) : Conflict("PatientIdentity.IdempotencyKeyReused");
        }
        var proof = await ProofAsync(p.EmailVerificationToken, PatientOnboardingAction.RegisterNew, false, cancellationToken);
        if (proof is null)
        {
            var registered = await ProofAsync(p.EmailVerificationToken, PatientOnboardingAction.LoginOrRecovery, false, cancellationToken);
            if (registered?.NormalizedEmail == PatientIdentityEmail.Normalize(p.Email)) return Conflict("Identity.AlreadyRegistered");
            var conflict = await ProofAsync(p.EmailVerificationToken, PatientOnboardingAction.IdentityConflict, false, cancellationToken);
            if (conflict?.NormalizedEmail == PatientIdentityEmail.Normalize(p.Email)) return Conflict("Identity.AlreadyRegistered");
            var claim = await ProofAsync(p.EmailVerificationToken, PatientOnboardingAction.Claim, false, cancellationToken);
            if (claim is not null && claim.NormalizedEmail != PatientIdentityEmail.Normalize(p.Email)) return Conflict("PatientOnboarding.TokenInvalidOrExpired");
            return Conflict(claim is null ? "PatientOnboarding.VerificationRequired" : "Patient.ClaimRequired");
        }
        if (proof.NormalizedEmail != PatientIdentityEmail.Normalize(p.Email)) return Conflict("PatientOnboarding.TokenInvalidOrExpired");
        if (proof.ConsumedOnUtc is not null) return proof.MutationIdempotencyKey == command.IdempotencyKey && proof.IdempotencyFingerprint == fingerprint && proof.MutationResponseJson is not null
            ? Ok(JsonSerializer.Deserialize<JsonElement>(proof.MutationResponseJson)) : Conflict("PatientOnboarding.TokenInvalidOrExpired");
        if (!ValidProfile(p, Now) || string.IsNullOrWhiteSpace(p.PhoneNumber) || string.IsNullOrWhiteSpace(p.UserName) || p.UserName.Length > 100) return Invalid();
        if (Age(p.DateOfBirth, Now) < 18) return Result<object>.Fail(PatientIdentityErrors.Age("Patient.SelfRegistrationAgeRestricted"));
        if (p.Password is null || p.Password != p.ConfirmPassword || !passwords.IsStrongPassword(p.Password)) return Invalid();
        if (await store.PatientByEmailAsync(proof.NormalizedEmail, cancellationToken) is not null) return Conflict("Patient.ClaimRequired");
        if (await data.EmailExistsAsync(proof.NormalizedEmail, null, cancellationToken)) return Conflict("Identity.AlreadyRegistered");
        if (await data.UserNameExistsAsync(p.UserName.Trim(), null, cancellationToken)) return Conflict("Identity.UserNameAlreadyExists");
        var role = await data.FindRoleByNameAsync(SystemRoleNames.Patient, cancellationToken); if (role is null) return Conflict("Identity.SecurityDataMissing");
        var id = Guid.NewGuid(); var userId = Guid.NewGuid();
        var image = await SaveAsync(p.ProfileImage, $"Patients/{id}/Profile", cancellationToken);
        var front = await SaveAsync(p.PersonalIdFrontImage, $"Patients/{id}/Verification/Front", cancellationToken);
        var back = await SaveAsync(p.PersonalIdBackImage, $"Patients/{id}/Verification/Back", cancellationToken);
        var patient = Patient.Create(id, p.NameAr, p.NameEn, p.DateOfBirth, p.Gender, p.PhoneNumber, proof.NormalizedEmail, image, front, back, DateOnly.FromDateTime(Now));
        var user = ApplicationUser.Create(userId, p.UserName, proof.NormalizedEmail, p.PhoneNumber, await passwords.HashAsync(p.Password, cancellationToken), UserType.Patient, false, Now);
        if (patient.IsFailure || user.IsFailure) return Invalid();
        data.Add(patient.Value); data.Add(user.Value); data.Add(new UserRole(Guid.NewGuid(), userId, role.Id));
        data.Add(PatientAccountLink.Create(Guid.NewGuid(), userId, id, PatientAccountLinkSource.SelfRegistration, Now).Value);
        var possible = await store.MatchAsync(new(null, p.PhoneNumber, p.NameAr, p.DateOfBirth, p.Gender), 1, 20, true, cancellationToken);
        await OpenDuplicatesAsync(id, possible.Items, cancellationToken, userId);
        var response = new VerifiedPatientRegistrationResponse(id, userId); proof.Consume(Now, fingerprint); proof.CompleteMutation(command.IdempotencyKey!, JsonSerializer.Serialize(response, StoredJson));
        return Ok(response);
    }
    private async Task<Result<object>> FullAsync(PatientIdentityInput p, CancellationToken cancellationToken)
    {
        var email = PatientIdentityEmail.Normalize(p.Email);
        if (!ValidProfile(p, Now) || email is null || !p.VerifiedInPerson || p.VerificationBasis is not (PatientIdentityVerificationBasis.GovernmentIdSeen or PatientIdentityVerificationBasis.ApprovedAlternateEvidence)) return Invalid();
        if (Age(p.DateOfBirth, Now) < 18) return Result<object>.Fail(PatientIdentityErrors.Age("Patient.FullRegistrationAgeRestricted"));
        if (await store.PatientByEmailAsync(email, cancellationToken) is not null) return Conflict("Patient.ClaimRequired");
        var existing = await store.UserByEmailAsync(email, cancellationToken);
        if (existing is not null) return Conflict(existing.UserType == UserType.Patient ? "Identity.AlreadyRegistered" : "Identity.EmailOwnedByAnotherAccount");
        var role = await data.FindRoleByNameAsync(SystemRoleNames.Patient, cancellationToken); if (role is null) return Conflict("Identity.SecurityDataMissing");
        var matches = await store.MatchAsync(new(email, p.PhoneNumber, p.NameAr, p.DateOfBirth, p.Gender, SecondaryPhoneNumber: p.PrimaryContactPhoneNumber), 1, 20, true, cancellationToken);
        if (matches.Total > 0 && (p.MatchReviewOutcome != "DistinctPerson" || string.IsNullOrWhiteSpace(p.MatchReviewReason))) return Conflict("Patient.IdentityConflict");
        var id = Guid.NewGuid(); var userId = Guid.NewGuid();
        if (p.PrimaryContactLinkedPatientId is { } linked && await data.FindPatientByIdAsync(linked, cancellationToken) is null) return Invalid();
        var contact = CreateContact(p, id); if (contact.IsFailure) return Result<object>.Fail(contact.Errors);
        var patient = Patient.Create(id, p.NameAr, p.NameEn, p.DateOfBirth, p.Gender, p.PhoneNumber, email, await SaveAsync(p.ProfileImage, $"Patients/{id}/Profile", cancellationToken), null, null, DateOnly.FromDateTime(Now));
        if (patient.IsFailure) return Invalid();
        var userName = await InternalUserNameAsync(cancellationToken);
        if (userName is null) return Conflict("Identity.UserNameAlreadyExists");
        patient.Value.StampReceptionRegistration(p.DoctorPracticeId, Actor, Now);
        var user = ApplicationUser.Create(userId, userName, email, p.PhoneNumber,
            await passwords.HashAsync(protector.GenerateResetToken() + "Aa1!", cancellationToken), UserType.Patient, true, Now, Actor);
        if (user.IsFailure) return Invalid(); user.Value.Deactivate(); data.Add(patient.Value); data.Add(user.Value);
        if (contact.Value is not null) data.Add(contact.Value);
        data.Add(new UserRole(Guid.NewGuid(), userId, role.Id)); data.Add(PatientAccountLink.Create(Guid.NewGuid(), userId, id, PatientAccountLinkSource.ReceptionFullRegistration, Now).Value);
        data.Add(new PatientIdentityAttestation(Guid.NewGuid(), id, p.DoctorPracticeId, Actor, p.VerificationBasis, PatientIdentityAttestationPurpose.NewFullRegistration, Now, null, p.VerificationNote));
        await OpenDuplicatesAsync(id, matches.Items, cancellationToken);
        if (matches.Total > 0) data.Add(new PatientIdentityAttestation(Guid.NewGuid(), id, p.DoctorPracticeId, Actor, p.VerificationBasis,
            PatientIdentityAttestationPurpose.SoftMatchOverride, Now, null, p.MatchReviewReason));
        var invitation = await InviteAsync(PatientActivationPurpose.ReceptionFull, userId, null, email, cancellationToken);
        return Ok(new ReceptionFullRegistrationResponse(id, userId, "Full", "PendingActivation", PatientIdentityEmail.Mask(email)!, invitation.ExpiresOnUtc));
    }
    private async Task<string?> InternalUserNameAsync(CancellationToken ct)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var candidate = "patient-" + Guid.NewGuid().ToString("N");
            if (!await data.UserNameExistsAsync(candidate, null, ct)) return candidate;
        }
        return null;
    }
    private static Result<PatientContact?> CreateContact(PatientIdentityInput p, Guid patientId)
    {
        if (string.IsNullOrWhiteSpace(p.PrimaryContactPhoneNumber)) return !string.IsNullOrWhiteSpace(p.PhoneNumber) ? Result<PatientContact?>.Ok(null) : Result<PatientContact?>.Fail(PatientIdentityErrors.Invalid);
        var c = PatientContact.Create(Guid.NewGuid(), patientId, p.PrimaryContactNameAr ?? "", p.PrimaryContactNameEn, p.PrimaryContactPhoneNumber, p.PrimaryContactRelationshipType, p.PrimaryContactLinkedPatientId, p.PrimaryContactIsPrimary, p.PrimaryContactEmail);
        return c.IsFailure || string.IsNullOrWhiteSpace(p.PhoneNumber) && !p.PrimaryContactIsPrimary ? Result<PatientContact?>.Fail(PatientIdentityErrors.Invalid) : Result<PatientContact?>.Ok(c.Value);
    }
    private async Task<Result<object>> AttestAsync(PatientIdentityInput p, bool claim, CancellationToken cancellationToken)
    {
        var patient = await data.FindPatientByIdAsync(p.PatientId, cancellationToken);
        if (patient is null) return Result<object>.Fail(PatientIdentityErrors.NotFound);
        if (!p.VerifiedInPerson || !Enum.IsDefined(p.VerificationBasis) || p.Reason?.Length > 500 || claim &&
            (p.VerificationBasis == PatientIdentityVerificationBasis.PatientProvidedKnownDemographics || string.IsNullOrWhiteSpace(p.Reason) || PatientIdentityEmail.Normalize(p.Email) is not { } || !PatientIdentityEmail.IsValid(p.Email))) return Invalid();
        var attestation = new PatientIdentityAttestation(Guid.NewGuid(), patient.Id, p.DoctorPracticeId, Actor, p.VerificationBasis,
            claim ? PatientIdentityAttestationPurpose.ClaimProof : PatientIdentityAttestationPurpose.ReuseForBooking, Now,
            claim ? Now.AddHours(48) : Now.AddMinutes(Options.PatientReuseAttestationTtlMinutes), p.Reason, claim ? p.Email : null);
        data.Add(attestation); return Ok(new PatientIdentityAttestationResponse(patient.Id, attestation.Id, attestation.ExpiresOnUtc));
    }
    public async Task OpenDuplicatesAsync(Guid newId, IReadOnlyList<Patient> matches, CancellationToken cancellationToken, Guid? creator = null)
    {
        foreach (var patient in matches.Where(x => x.Id != newId))
        {
            var item = new PatientDuplicateCase(Guid.NewGuid(), newId, patient.Id, PatientDuplicateTrigger.SoftMatch, "PhoneOrDemographics", creator ?? current.UserId, Now);
            if (await store.OpenDuplicateAsync(item.LowerPatientId, item.HigherPatientId, cancellationToken) is null) data.Add(item);
        }
    }
    private async Task<string?> SaveAsync(MediaUpload? upload, string folder, CancellationToken cancellationToken)
    {
        if (upload is null) return null;
        var stored = await media.SaveAsync(upload, new MediaStorageRequest(folder), cancellationToken); compensation.Track(stored.Key); return stored.Key;
    }
    private Task MailAsync(string key, string email, string subject, string text, CancellationToken cancellationToken) => outbox.QueueAsync(new QueueEmailMessage(key, email, subject,
        "<p dir=\"auto\">" + WebUtility.HtmlEncode(text).Replace("\n", "<br>", StringComparison.Ordinal) + "</p>", text), cancellationToken);
    private async Task<PatientActivationInvitation> InviteAsync(PatientActivationPurpose purpose, Guid? userId, Guid? claimId, string email, CancellationToken cancellationToken, int sendCount = 1)
    {
        var token = protector.GenerateResetToken(); var invitation = new PatientActivationInvitation(Guid.NewGuid(), purpose, userId, claimId, email, InvitationHash(token), Now, Now.AddHours(Options.ActivationInvitationTtlHours), sendCount);
        data.Add(invitation);
        await MailAsync($"patient-full-invite:{invitation.Id:N}", email, "تفعيل حساب وصلة / Activate your Wasla account",
            $"استخدم دعوة التفعيل لاختيار كلمة المرور خلال {Options.ActivationInvitationTtlHours} ساعة.\nUse this activation invitation to choose your password within {Options.ActivationInvitationTtlHours} hours.\n{token}", cancellationToken);
        return invitation;
    }
}
