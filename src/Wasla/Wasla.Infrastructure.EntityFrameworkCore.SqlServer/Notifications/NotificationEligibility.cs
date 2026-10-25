using BuildingBlock.Application.Time;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.Notifications;
using Wasla.Application.Persistence;
using Wasla.Domain.Clinical;
using Wasla.Domain.Common;
using Wasla.Domain.Diagnostics;
using Wasla.Domain.Families;
using Wasla.Domain.Medications;
using Wasla.Domain.Notifications;
using Wasla.Domain.Patients;
using Wasla.Domain.Reservations;
using Wasla.Domain.Security;
using Wasla.Domain.Tickets;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Notifications;

public sealed record NotificationRecipient(string LogicalKey, Guid? UserId, Guid PatientId, string? TrustedEmail);

public sealed class NotificationEligibility(WaslaDbContext db, IWaslaDataStore store, IDateTimeProvider clock,
    IOptions<NotificationOptions> options, IOptions<NotificationPrivacyOptions> privacy,
    IOptions<Wasla.Application.Features.PatientOnboarding.PatientIdentityOptions> identityOptions)
{
    public async Task<string?> SourceDenialAsync(NotificationEvent source, NotificationIntent intent, CancellationToken ct)
    {
        if (!options.Value.Enabled || !options.Value.DispatchEnabled) return "Feature.Disabled";
        if (source.SourceType == "Reservation")
        {
            var r = await db.Reservations.AsNoTracking().Include(x => x.History).SingleOrDefaultAsync(x => x.Id == source.SourceId, ct);
            if (r is null) return "Source.Unavailable";
            if (source.EventType == "Appointment.ReminderDue")
            {
                if (intent.AppointmentStartUtcSnapshot != r.ScheduledStartUtc) return "Reminder.AppointmentChanged";
                if (!await db.DoctorPractices.AnyAsync(p => p.Id == r.DoctorPracticeId && p.IsActive &&
                    db.Doctors.Any(d => d.Id == p.DoctorId && d.ApprovalStatus == DoctorApprovalStatus.Approved), ct)) return "Reminder.PracticeUnavailable";
                if (await db.PracticeOperationalDays.AnyAsync(d => d.DoctorPracticeId == r.DoctorPracticeId && d.BusinessDate == r.BusinessDate && d.ClosedAtUtc != null, ct)) return "Reminder.OperationalDayClosed";
                if (r.History.Any(h => h.EventType == ReservationHistoryEventType.RestoredFromNoShow &&
                    h.OccurredOnUtc >= source.OccurredAtUtc && h.OccurredOnUtc >= intent.DueAtUtc)) return "Reminder.RestoredAfterDue";
                var revision = AppointmentRevisionIdentity.Current(r) ?? "";
                return NotificationPolicy.ReminderEligible(clock.UtcNow, intent.DueAtUtc, r.ScheduledStartUtc, r.Status == ReservationStatus.Active,
                    intent.SourceRevisionKey, revision) ? null : "Reminder.Ineligible";
            }
            var valid = source.EventType switch
            {
                "Reservation.Created" or "Reservation.Rescheduled" or "Reservation.RestoredFromNoShow" or "Doctor.NewBooking" => r.Status == ReservationStatus.Active,
                "Reservation.Cancelled" or "Doctor.BookingCancelled" => r.Status == ReservationStatus.Cancelled,
                "Reservation.MarkedNoShow" => r.Status == ReservationStatus.NoShow,
                "Reservation.Expired" => r.Status == ReservationStatus.Expired,
                "Patient.CheckedIn" => r.Status == ReservationStatus.ConvertedToTicket,
                _ => false
            };
            if (!valid) return "Source.Changed";
            if (source.EventType is "Reservation.Created" or "Reservation.Rescheduled" or "Reservation.RestoredFromNoShow")
            {
                var revision = AppointmentRevisionIdentity.Current(r);
                if (revision != source.SourceRevisionKey) return "Source.RevisionChanged";
            }
        }
        else if (source.DataClassification == NotificationDataClassification.Clinical && !await ClinicalVisibleAsync(source, ct)) return "Clinical.Unavailable";
        return null;
    }

    public async Task<IReadOnlyList<NotificationRecipient>> ResolveAsync(NotificationEvent source, CancellationToken ct)
    {
        if (source.Category is NotificationCategory.DoctorOperational or NotificationCategory.AccountGovernance)
        {
            var user = source.TargetApplicationUserId;
            if (source.Category == NotificationCategory.DoctorOperational)
                user = await (from p in db.DoctorPractices.AsNoTracking()
                              join d in db.Doctors.AsNoTracking() on p.DoctorId equals d.Id
                              where p.Id == source.DoctorPracticeId && p.IsActive && d.ApprovalStatus == DoctorApprovalStatus.Approved
                              select (Guid?)d.ApplicationUserId).SingleOrDefaultAsync(ct);
            if (user is not { } id || !await ActiveInboxAsync(id, ct)) return [];
            if (source.Category == NotificationCategory.DoctorOperational &&
                (await store.GetAccessSnapshotAsync(id, ct))?.Permissions.Contains(PermissionNames.DoctorPracticeReservationsViewOwn, StringComparer.OrdinalIgnoreCase) != true) return [];
            return [new($"user:{id:N}", id, Guid.Empty, null)];
        }
        if (source.PatientId is not { } patientId) return [];
        var result = new List<NotificationRecipient>();
        await AddPatientAsync(patientId, result, ct);
        if (source.Category == NotificationCategory.ClinicalAvailability)
        {
            var allowed = new List<NotificationRecipient>();
            foreach (var recipient in result)
                if (recipient.UserId is { } user && (await store.GetAccessSnapshotAsync(user, ct))?.Permissions.Contains(SourceReadPermission(source.SourceType), StringComparer.OrdinalIgnoreCase) == true)
                    allowed.Add(recipient);
            return allowed;
        }
        var child = await db.FamilyMembers.AsNoTracking().SingleOrDefaultAsync(m => m.PatientId == patientId && m.IsActive && m.Role == FamilyMemberRole.Child, ct);
        var patient = await db.Patients.AsNoTracking().SingleOrDefaultAsync(p => p.Id == patientId, ct);
        if (child is null || patient is null || !await db.Families.AnyAsync(f => f.Id == child.FamilyId && f.Status == FamilyStatus.Active, ct)) return result;
        var adults = await db.FamilyMembers.AsNoTracking().Where(m => m.FamilyId == child.FamilyId && m.IsActive && m.PatientId != patientId).ToArrayAsync(ct);
        foreach (var adult in adults)
        {
            if (!new PatientAccessPolicy().CanBookReservation(adult, child, patient, DateOnly.FromDateTime(clock.UtcNow))) continue;
            // Medical and booking ages remain separate. Exact relationship evidence is evaluated independently.
            if (await db.Set<FamilyMedicalAccessRestriction>().AnyAsync(r => r.ChildPatientId == patientId && r.AffectedAdultPatientId == adult.PatientId && r.Status == FamilyMedicalRestrictionStatus.Active, ct)) continue;
            var proof = await db.FamilyRelationshipRequests.AsNoTracking().Where(p => p.FamilyId == child.FamilyId &&
                p.RequesterPatientId == adult.PatientId && p.TargetPatientId == patientId && p.RequesterClaimedRole == adult.Role &&
                p.TargetClaimedRole == FamilyMemberRole.Child && p.Status == FamilyRelationshipRequestStatus.Approved &&
                p.ReviewedBySuperAdminApplicationUserId != null && p.ReviewedOnUtc != null).OrderByDescending(p => p.ReviewedOnUtc).FirstOrDefaultAsync(ct);
            if (proof is null || !await db.FamilyRelationshipDocuments.AnyAsync(d => d.FamilyRelationshipRequestId == proof.Id && d.RevisionNumber == proof.CurrentRevisionNumber, ct)) continue;
            if (!await db.SuperAdmins.AnyAsync(a => a.ApplicationUserId == proof.ReviewedBySuperAdminApplicationUserId, ct)) continue;
            if (adult.Role == FamilyMemberRole.LegalGuardian && await db.Set<FamilyMedicalEligibilityReview>().AnyAsync(r =>
                r.AdultPatientId == adult.PatientId && r.ChildPatientId == patientId && r.AuthorityRelationshipRequestId == proof.Id && r.Status == FamilyMedicalEligibilityStatus.Revoked, ct)) continue;
            if (adult.Role is FamilyMemberRole.Father or FamilyMemberRole.Mother && !await db.Set<VerifiedParentChildRelationship>().AnyAsync(p =>
                p.ParentFamilyMemberId == adult.Id && p.ChildFamilyMemberId == child.Id && p.Status == ParentChildVerificationStatus.Active &&
                p.ApprovedRelationshipRequestId == proof.Id && p.ApprovedRevisionNumber == proof.CurrentRevisionNumber, ct)) continue;
            await AddPatientAsync(adult.PatientId, result, ct);
        }
        return result.DistinctBy(r => r.LogicalKey).ToArray();
    }

    private async Task AddPatientAsync(Guid patientId, List<NotificationRecipient> result, CancellationToken ct)
    {
        var link = await db.PatientAccountLinks.AsNoTracking().SingleOrDefaultAsync(l => l.PatientId == patientId, ct);
        if (link is null)
        {
            var standaloneEmail = await db.Patients.Where(p => p.Id == patientId).Select(p => p.Email).SingleOrDefaultAsync(ct);
            var cutoff = clock.UtcNow.AddDays(-identityOptions.Value.ClaimInactivityExpiryDays);
            // A reviewed, still-current claim binds the verified address to this exact global Patient,
            // even during the interval before account activation. Mere contact syntax is insufficient.
            var standaloneTrusted = standaloneEmail is not null && !await db.ApplicationUsers.AnyAsync(u => u.Email == standaloneEmail, ct) &&
                await db.PatientClaimRequests.AnyAsync(c => c.ApprovedTargetPatientId == patientId && c.TargetPatientId == patientId &&
                    c.Status == PatientClaimStatus.ApprovedPendingActivation && c.ClaimantNormalizedEmail == standaloneEmail && c.LastActivityOnUtc > cutoff &&
                    c.ReviewedByApplicationUserId != null && c.ReviewedOnUtc != null && db.SuperAdmins.Any(a => a.ApplicationUserId == c.ReviewedByApplicationUserId) &&
                    db.PatientOnboardingChallenges.Any(p => p.Id == c.SourceChallengeId && p.Purpose == PatientOnboardingPurpose.Claim && p.NormalizedEmail == standaloneEmail && p.VerifiedOnUtc != null && p.ConsumedOnUtc != null && p.InvalidatedOnUtc == null), ct);
            result.Add(new($"patient:{patientId:N}", null, patientId, standaloneTrusted ? standaloneEmail : null)); return;
        }
        if (!await ActiveInboxAsync(link.ApplicationUserId, ct)) return;
        var user = await db.ApplicationUsers.AsNoTracking().SingleAsync(u => u.Id == link.ApplicationUserId, ct);
        if (user.UserType != UserType.Patient) return;
        var email = user.Email;
        var fingerprint = NotificationVerifiedContact.Fingerprint(email);
        var proofs = await db.PatientOnboardingChallenges.AsNoTracking().Where(c => c.NormalizedEmail == email && c.VerifiedOnUtc != null &&
            c.ConsumedOnUtc != null && c.InvalidatedOnUtc == null && (c.Purpose == PatientOnboardingPurpose.Register ||
                c.Purpose == PatientOnboardingPurpose.EmailChange && c.ApplicationUserId == user.Id)).OrderByDescending(c => c.ConsumedOnUtc).Take(20).ToArrayAsync(ct);
        var trusted = await db.Set<NotificationVerifiedContact>().AnyAsync(c => c.ApplicationUserId == user.Id && c.PatientId == patientId && c.AddressFingerprint == fingerprint, ct) ||
            proofs.Any(c => c.Purpose == PatientOnboardingPurpose.EmailChange && c.ApplicationUserId == user.Id || RegistrationBound(c, user.Id, patientId)) ||
            await db.PatientActivationInvitations.AsNoTracking().AnyAsync(i => i.NormalizedEmail == email && i.ConsumedOnUtc != null && i.RevokedOnUtc == null &&
                (i.ApplicationUserId == user.Id || db.PatientClaimRequests.Any(c => c.Id == i.ClaimRequestId && c.LinkedApplicationUserId == user.Id && c.TargetPatientId == patientId && c.Status == PatientClaimStatus.Linked)), ct);
        result.Add(new($"patient:{patientId:N}:user:{user.Id:N}", user.Id, patientId, trusted ? email : null));
    }

    private static bool RegistrationBound(PatientOnboardingChallenge proof, Guid user, Guid patient)
    {
        if (proof.Purpose != PatientOnboardingPurpose.Register || proof.MutationResponseJson is not { } response) return false;
        try
        {
            using var json = JsonDocument.Parse(response);
            return json.RootElement.TryGetProperty("applicationUserId", out var u) && u.TryGetGuid(out var uid) && uid == user &&
                json.RootElement.TryGetProperty("patientId", out var p) && p.TryGetGuid(out var pid) && pid == patient;
        }
        catch (JsonException) { return false; }
    }

    public async Task<bool> ActiveInboxAsync(Guid userId, CancellationToken ct)
    {
        var s = await store.GetAccessSnapshotAsync(userId, ct);
        return s is { User.IsActive: true, User.IsFirstLogin: false } &&
            (s.User.UserType != UserType.Doctor || s.DoctorStatus == DoctorApprovalStatus.Approved) &&
            s.Permissions.Contains(PermissionNames.NotificationsViewOwn, StringComparer.OrdinalIgnoreCase);
    }

    public static string SourceReadPermission(string sourceType) => sourceType switch
    {
        "Prescription" => PermissionNames.PrescriptionsViewOwnCompleted,
        "LabRequest" => PermissionNames.LabRequestsViewOwnIssued,
        "RadiologyRequest" => PermissionNames.RadiologyRequestsViewOwnIssued,
        "LabResult" => PermissionNames.LabResultsViewOwnCurrent,
        "RadiologyResult" => PermissionNames.RadiologyResultsViewOwnCurrent,
        _ => PermissionNames.ReservationsViewOwn
    };

    public async Task<(string? Denial, string Language)> ChannelAsync(NotificationEvent source, NotificationRecipient recipient, NotificationChannel channel, CancellationToken ct)
    {
        var preference = recipient.UserId is { } user ? await db.NotificationPreferences.AsNoTracking()
            .SingleOrDefaultAsync(p => p.ApplicationUserId == user && p.Category == source.Category && p.Channel == channel, ct) : null;
        var language = NotificationPolicy.Language(preference?.PreferredLanguage);
        if (channel == NotificationChannel.InApp && recipient.UserId is null) return ("Recipient.NoAccount", language);
        if (channel == NotificationChannel.Email && recipient.TrustedEmail is null) return ("Recipient.NoTrustedEmail", language);
        if (NotificationPolicy.Editable(source.Category) && !(preference?.Enabled ?? NotificationPolicy.DefaultEnabled(source.Category, channel))) return ("Preference.Disabled", language);
        if (source.Category == NotificationCategory.ClinicalAvailability)
        {
            if (recipient.PatientId != source.PatientId) return ("Clinical.PatientSelfRequired", language);
            if (channel == NotificationChannel.Email && (preference is not { Enabled: true } || !privacy.Value.ClinicalEmailReleaseApproved)) return ("Clinical.EmailReleaseRequired", language);
            if (channel == NotificationChannel.InApp && !privacy.Value.ClinicalInAppReleaseApproved) return ("Clinical.InAppReleaseRequired", language);
        }
        return (null, language);
    }

    public async Task<bool> ClinicalVisibleAsync(NotificationEvent source, CancellationToken ct)
    {
        Guid? encounter = null;
        if (source.SourceType == "Prescription")
            encounter = await db.Prescriptions.AsNoTracking().Where(p => p.Id == source.SourceId && p.PatientId == source.PatientId &&
                p.Versions.Any(v => v.Id == source.SourceVersionId && v.Status == PrescriptionVersionStatus.Finalized)).Select(p => (Guid?)p.MedicalEncounterId).SingleOrDefaultAsync(ct);
        else if (source.SourceType == "LabRequest")
            encounter = await db.LabRequests.AsNoTracking().Where(p => p.Id == source.SourceId && p.PatientId == source.PatientId && p.Status != DiagnosticRequestStatus.Draft && p.Status != DiagnosticRequestStatus.Cancelled).Select(p => (Guid?)p.MedicalEncounterId).SingleOrDefaultAsync(ct);
        else if (source.SourceType == "RadiologyRequest")
            encounter = await db.RadiologyRequests.AsNoTracking().Where(p => p.Id == source.SourceId && p.PatientId == source.PatientId && p.Status != DiagnosticRequestStatus.Draft && p.Status != DiagnosticRequestStatus.Cancelled).Select(p => (Guid?)p.MedicalEncounterId).SingleOrDefaultAsync(ct);
        else if (source.SourceType == "LabResult")
            encounter = await (from r in db.LabResults.AsNoTracking()
                               join q in db.LabRequests.AsNoTracking() on r.RequestId equals q.Id
                               where r.Id == source.SourceId && r.PatientId == source.PatientId && q.Status != DiagnosticRequestStatus.Draft &&
                                   r.Versions.Any(v => v.Id == source.SourceVersionId && (source.EventType == "DiagnosticResult.Voided" ? v.Status == DiagnosticVersionStatus.Voided : v.Status == DiagnosticVersionStatus.Finalized))
                               select (Guid?)q.MedicalEncounterId).SingleOrDefaultAsync(ct);
        else if (source.SourceType == "RadiologyResult")
            encounter = await (from r in db.RadiologyResults.AsNoTracking()
                               join q in db.RadiologyRequests.AsNoTracking() on r.RequestId equals q.Id
                               where r.Id == source.SourceId && r.PatientId == source.PatientId && q.Status != DiagnosticRequestStatus.Draft &&
                                   r.Versions.Any(v => v.Id == source.SourceVersionId && (source.EventType == "DiagnosticResult.Voided" ? v.Status == DiagnosticVersionStatus.Voided : v.Status == DiagnosticVersionStatus.Finalized))
                               select (Guid?)q.MedicalEncounterId).SingleOrDefaultAsync(ct);
        return encounter is { } id && await db.MedicalEncounters.AsNoTracking().AnyAsync(e => e.Id == id && e.PatientId == source.PatientId && e.Status == EncounterStatus.Completed &&
            db.AllTickets.Any(t => t.Id == e.TicketId && t.Status == TicketStatus.Completed && t.PatientId == e.PatientId && t.DoctorId == e.DoctorId && t.DoctorPracticeId == e.DoctorPracticeId) &&
            db.DoctorPractices.Any(p => p.Id == e.DoctorPracticeId && p.DoctorId == e.DoctorId), ct);
    }
}
