using Wasla.Domain.Common;
using Wasla.Domain.Patients;
using Wasla.Domain.Security;
namespace Wasla.Tests.Unit;

public sealed class Phase5FinalIdentityDomainTests
{
    private static readonly DateTime Now = new(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc);
    private static readonly string[] Phase5Permissions = ["Patients.RegisterFull", "Patients.RegistrationMatch", "PatientClaims.ViewAll", "PatientClaims.ViewDetails", "PatientClaims.ViewEvidence", "PatientClaims.Approve", "PatientClaims.Reject", "PatientClaims.RequestModification", "PatientDuplicates.View", "PatientDuplicates.Resolve"];
    private static readonly string[] NotificationPermissions = ["Notifications.ViewOwn", "Notifications.MarkReadOwn", "Notifications.PreferencesViewOwn", "Notifications.PreferencesManageOwn", "Notifications.SettingsView", "Notifications.SettingsManage", "Notifications.DeliveriesView", "Notifications.DeliveriesRetry"];
    [Theory]
    [InlineData(" Person@Example.Test ", "person@example.test")]
    [InlineData(" ", null)]
    [InlineData(null, null)]
    public void Patient_identity_email_is_normalized_without_provider_specific_rewriting(string? input, string? expected) => Assert.Equal(expected, PatientIdentityEmail.Normalize(input));
    [Fact]
    public void Contact_email_is_separate_optional_nonidentity_and_shared()
    {
        var a = PatientContact.Create(Guid.NewGuid(), Guid.NewGuid(), "ولي الأمر", null, "01000000000", PatientContactRelationshipType.Guardian, null, true, " Parent@Example.Test ").Value;
        var b = PatientContact.Create(Guid.NewGuid(), Guid.NewGuid(), "ولي الأمر", null, "01000000000", PatientContactRelationshipType.Guardian, null, true, "parent@example.test").Value;
        Assert.Equal(a.ContactEmail, b.ContactEmail); Assert.NotEqual(a.PatientId, b.PatientId);
        Assert.False(PatientContact.Create(Guid.NewGuid(), Guid.NewGuid(), "ولي الأمر", null, "", PatientContactRelationshipType.Guardian, null, true, "parent@example.test").IsSuccess);
    }
    [Fact]
    public void Otp_attempt_limit_and_exact_expiry_are_enforced()
    {
        var c = Challenge(); Assert.True(c.CanVerify(Now.AddMinutes(10).AddTicks(-1))); Assert.False(c.CanVerify(Now.AddMinutes(10)));
        for (var i = 0; i < 5; i++) c.FailedAttempt(Now);
        Assert.Equal(5, c.AttemptCount); Assert.False(c.CanVerify(Now)); Assert.NotNull(c.InvalidatedOnUtc);
    }
    [Fact]
    public void Continuation_distinguishes_otp_purpose_from_server_allowed_action_and_mutation_from_read()
    {
        var c = Challenge(); c.Verify("hashed-continuation", PatientOnboardingAction.Claim, Now, Now.AddMinutes(30));
        Assert.True(c.CanUse(PatientOnboardingAction.Claim, Now)); Assert.False(c.CanUse(PatientOnboardingAction.RegisterNew, Now));
        c.Consume(Now, "fingerprint"); Assert.False(c.CanUse(PatientOnboardingAction.Claim, Now));
        Assert.True(c.CanUse(PatientOnboardingAction.Claim, Now, false)); Assert.False(c.CanUse(PatientOnboardingAction.Claim, Now.AddMinutes(30), false));
    }
    [Fact]
    public void Claim_approval_does_not_link_and_terminal_claim_cannot_reopen()
    {
        var c = Claim(); var target = Guid.NewGuid();
        Assert.True(c.Decide(PatientClaimStatus.ApprovedPendingActivation, Guid.NewGuid(), null, target, Now).IsSuccess);
        Assert.Null(c.LinkedApplicationUserId); Assert.Equal(target, c.ApprovedTargetPatientId);
        Assert.True(c.Link(Guid.NewGuid(), Now).IsSuccess); Assert.Equal(PatientClaimStatus.Linked, c.Status);
        Assert.False(c.Resubmit(Now).IsSuccess); Assert.False(c.Expire(Now.AddYears(1), 90));
    }
    [Fact]
    public void Modification_requires_new_revision_and_rejection_requires_reason()
    {
        var c = Claim(); Assert.False(c.Decide(PatientClaimStatus.Rejected, Guid.NewGuid(), " ", null, Now).IsSuccess);
        Assert.True(c.Decide(PatientClaimStatus.ModificationRequested, Guid.NewGuid(), "أعد تقديم الدليل", null, Now).IsSuccess);
        Assert.False(c.Decide(PatientClaimStatus.Rejected, Guid.NewGuid(), "reason", null, Now).IsSuccess);
        Assert.True(c.Resubmit(Now.AddDays(1)).IsSuccess); Assert.Equal(2, c.EvidenceRevision);
    }
    [Fact]
    public void Retention_uses_utc_inactivity_boundary_and_legal_hold_is_durable_metadata()
    {
        var c = Claim(); Assert.False(c.Expire(Now.AddDays(90).AddTicks(-1), 90));
        c.SetLegalHold(true, Guid.NewGuid(), "Synthetic preservation request", Now);
        Assert.True(c.Expire(Now.AddDays(90), 90)); Assert.True(c.IsUnderLegalHold); Assert.NotNull(c.LegalHoldSetByApplicationUserId);
        var modified = Claim(); Assert.True(modified.Decide(PatientClaimStatus.ModificationRequested, Guid.NewGuid(), "Synthetic modification review", null, Now.AddDays(89)).IsSuccess);
        Assert.Equal(Now, modified.LastActivityOnUtc); Assert.True(modified.Expire(Now.AddDays(90), 90));
        var resubmitted = Claim(); Assert.True(resubmitted.Decide(PatientClaimStatus.ModificationRequested, Guid.NewGuid(), "Synthetic modification review", null, Now.AddDays(89)).IsSuccess);
        Assert.True(resubmitted.Resubmit(Now.AddDays(89)).IsSuccess); Assert.False(resubmitted.Expire(Now.AddDays(90), 90)); Assert.True(resubmitted.Expire(Now.AddDays(179), 90));
    }
    [Fact]
    public void Invite_is_single_use_scoped_and_invalid_at_exact_48_hours()
    {
        var i = new PatientActivationInvitation(Guid.NewGuid(), PatientActivationPurpose.ReceptionFull, Guid.NewGuid(), null, "test@example.test", "hashed", Now, Now.AddHours(48));
        Assert.False(i.CanActivate(Now.AddHours(48))); Assert.True(i.CanActivate(Now.AddHours(48).AddTicks(-1)));
        i.Consume(Now); Assert.False(i.CanActivate(Now)); Assert.Throws<InvalidOperationException>(() => i.Consume(Now));
    }
    [Fact]
    public void Duplicate_pair_is_canonical_and_resolution_only_classifies()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var first = new PatientDuplicateCase(Guid.NewGuid(), a, b, PatientDuplicateTrigger.SoftMatch, "Phone", null, Now);
        var second = new PatientDuplicateCase(Guid.NewGuid(), b, a, PatientDuplicateTrigger.SoftMatch, "Phone", null, Now);
        Assert.Equal(first.LowerPatientId, second.LowerPatientId); Assert.Equal(first.HigherPatientId, second.HigherPatientId);
        Assert.True(first.Resolve(PatientDuplicateStatus.ConfirmedDuplicate, Guid.NewGuid(), "Reviewed", Now).IsSuccess);
        Assert.Equal(first.FirstPatientId, second.FirstPatientId); Assert.False(first.Resolve(PatientDuplicateStatus.DistinctPatients, Guid.NewGuid(), "Again", Now).IsSuccess);
    }
    [Fact]
    public void Attestation_binds_actor_practice_patient_and_single_operation()
    {
        var patient = Guid.NewGuid(); var practice = Guid.NewGuid(); var actor = Guid.NewGuid();
        var a = new PatientIdentityAttestation(Guid.NewGuid(), patient, practice, actor, PatientIdentityVerificationBasis.GovernmentIdSeen, PatientIdentityAttestationPurpose.ReuseForBooking, Now, Now.AddMinutes(15), null);
        Assert.False(a.IsValid(patient, Guid.NewGuid(), actor, Now)); Assert.False(a.IsValid(patient, practice, Guid.NewGuid(), Now)); Assert.False(a.IsValid(patient, practice, actor, Now.AddMinutes(15)));
        a.Associate(Guid.NewGuid(), null, Now); Assert.False(a.IsValid(patient, practice, actor, Now));
    }
    [Fact]
    public void Evidence_cannot_be_marked_purged_without_retirement_tombstone()
    {
        var e = new PatientClaimEvidence(Guid.NewGuid(), Guid.NewGuid(), 1, PatientClaimEvidenceType.GovernmentIdFront, "private/synthetic", "image/png", 20, "hash", PatientEvidenceUploaderType.Applicant, null, Now);
        Assert.Throws<InvalidOperationException>(() => e.MarkPurged(Now)); e.Retire(Now); e.MarkPurged(Now);
        Assert.NotNull(e.PrivateMediaKey); Assert.NotNull(e.PurgedOnUtc);
    }
    [Fact]
    public void Every_existing_permission_id_is_preserved_and_only_approved_permissions_are_appended()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory); while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "Wasla.sln"))) folder = folder.Parent;
        var old = File.ReadAllLines(Path.Combine(folder!.FullName, "tests", "Wasla.Tests.Unit", "Phase5ExistingPermissionIds.txt"));
        Assert.Equal(303, old.Length); Assert.Equal(old, PermissionNames.All.Take(old.Length));
        // Preserve the original ten additions and their identities, then require exactly the eight Phase 17 additions.
        Assert.Equal(Phase5Permissions, PermissionNames.All.Skip(old.Length).Take(10));
        Assert.Equal(NotificationPermissions, PermissionNames.All.Skip(old.Length + 10));
        Assert.Equal(old.Length + 10 + 8, PermissionNames.All.Count);
        var expected = old.Concat(Phase5Permissions).Concat(NotificationPermissions).ToArray();
        for (var i = 0; i < expected.Length; i++) Assert.Equal(Guid.Parse($"20000000-0000-0000-0000-{i + 1:D12}"), SystemPermissionIds.For(expected[i]));
        Assert.Equal(1, (int)PatientAccountLinkSource.SelfRegistration); Assert.Equal(2, (int)PatientAccountLinkSource.PatientClaim); Assert.Equal(3, (int)PatientAccountLinkSource.ReceptionFullRegistration);
    }
    private static PatientOnboardingChallenge Challenge() => new(Guid.NewGuid(), PatientOnboardingPurpose.Register, "synthetic@example.test", null, null, "hashed", Now, Now.AddMinutes(10), 5);
    private static PatientClaimRequest Claim() => new(Guid.NewGuid(), null, "synthetic@example.test", null, "مريض تجريبي", new(1990, 1, 1), Gender.Male, null, Guid.NewGuid(), Guid.NewGuid().ToString(), "hash", Now);
}
