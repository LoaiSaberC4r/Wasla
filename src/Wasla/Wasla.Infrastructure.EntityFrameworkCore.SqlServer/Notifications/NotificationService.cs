using System.Security.Cryptography;
using BuildingBlock.Application.Abstraction.Security;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Wasla.Application.Features.Notifications;
using Wasla.Application.Persistence;
using Wasla.Domain.Clinical;
using Wasla.Domain.Common;
using Wasla.Domain.Diagnostics;
using Wasla.Domain.Families;
using Wasla.Domain.Medications;
using Wasla.Domain.Notifications;
using Wasla.Domain.Security;
using Wasla.Domain.Tickets;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Email;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Notifications;

internal sealed class NotificationService(WaslaDbContext db, IWaslaDataStore store, ICurrentUser current, IDateTimeProvider clock,
    NotificationEligibility eligibility, NotificationEmailGuard emailGuard, IOptions<NotificationPrivacyOptions> privacy, ILogger<NotificationService> logger) : INotificationService
{
    private static readonly string[] ClinicalSources = ["Prescription", "LabRequest", "RadiologyRequest", "LabResult", "RadiologyResult"];
    private static Error Denied => NotificationErrors.Denied;
    private static Error Missing => NotificationErrors.Missing;
    private static Error Conflict => NotificationErrors.Conflict;

    private async Task<Guid?> ActorAsync(string permission, bool admin, CancellationToken ct)
    {
        if (!current.IsAuthenticated || current.UserId is not { } id || !current.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase)) return null;
        var s = await store.GetAccessSnapshotAsync(id, ct);
        if (s is not { User.IsActive: true, User.IsFirstLogin: false }) return null;
        return s.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase) &&
            (s.User.UserType != UserType.Doctor || s.DoctorStatus == DoctorApprovalStatus.Approved) &&
            (s.User.UserType != UserType.Reception || await db.Receptions.AnyAsync(r => r.ApplicationUserId == id && db.ReceptionPracticeAssignments.Any(a => a.ReceptionId == r.Id && a.IsActive), ct)) &&
            (!admin || s.User.UserType == UserType.SuperAdmin && s.SuperAdminId is not null) ? id : null;
    }

    private IQueryable<NotificationEvent> VisibleClinical(Guid user, string[] allowedSources)
    {
        var encounters = db.MedicalEncounters.Where(e => e.Status == EncounterStatus.Completed &&
            db.PatientAccountLinks.Any(l => l.ApplicationUserId == user && l.PatientId == e.PatientId) &&
            db.AllTickets.Any(t => t.Id == e.TicketId && t.Status == TicketStatus.Completed && t.PatientId == e.PatientId && t.DoctorId == e.DoctorId && t.DoctorPracticeId == e.DoctorPracticeId) &&
            db.DoctorPractices.Any(p => p.Id == e.DoctorPracticeId && p.DoctorId == e.DoctorId));
        return db.NotificationEvents.Where(e => e.DataClassification == NotificationDataClassification.Clinical && privacy.Value.ClinicalInAppReleaseApproved &&
            allowedSources.Contains(e.SourceType) &&
            db.ApplicationUsers.Any(u => u.Id == user && u.UserType == UserType.Patient) &&
            (e.SourceType == "Prescription" && db.Prescriptions.Any(p => p.Id == e.SourceId && p.PatientId == e.PatientId && encounters.Any(x => x.Id == p.MedicalEncounterId) &&
                p.Versions.Any(v => v.Id == e.SourceVersionId && v.Status == PrescriptionVersionStatus.Finalized)) ||
             e.SourceType == "LabRequest" && db.LabRequests.Any(p => p.Id == e.SourceId && p.PatientId == e.PatientId && p.Status != DiagnosticRequestStatus.Draft && p.Status != DiagnosticRequestStatus.Cancelled && encounters.Any(x => x.Id == p.MedicalEncounterId)) ||
             e.SourceType == "RadiologyRequest" && db.RadiologyRequests.Any(p => p.Id == e.SourceId && p.PatientId == e.PatientId && p.Status != DiagnosticRequestStatus.Draft && p.Status != DiagnosticRequestStatus.Cancelled && encounters.Any(x => x.Id == p.MedicalEncounterId)) ||
             e.SourceType == "LabResult" && db.LabResults.Any(r => r.Id == e.SourceId && r.PatientId == e.PatientId && db.LabRequests.Any(q => q.Id == r.RequestId && encounters.Any(x => x.Id == q.MedicalEncounterId)) &&
                r.Versions.Any(v => v.Id == e.SourceVersionId && (e.EventType == "DiagnosticResult.Voided" ? v.Status == DiagnosticVersionStatus.Voided : v.Status == DiagnosticVersionStatus.Finalized))) ||
             e.SourceType == "RadiologyResult" && db.RadiologyResults.Any(r => r.Id == e.SourceId && r.PatientId == e.PatientId && db.RadiologyRequests.Any(q => q.Id == r.RequestId && encounters.Any(x => x.Id == q.MedicalEncounterId)) &&
                r.Versions.Any(v => v.Id == e.SourceVersionId && (e.EventType == "DiagnosticResult.Voided" ? v.Status == DiagnosticVersionStatus.Voided : v.Status == DiagnosticVersionStatus.Finalized)))));
    }

    private async Task<IQueryable<UserNotification>> VisibleInboxAsync(Guid user, CancellationToken ct)
    {
        var own = await db.PatientAccountLinks.AsNoTracking().Where(l => l.ApplicationUserId == user).Select(l => (Guid?)l.PatientId).SingleOrDefaultAsync(ct);
        var subjects = new List<Guid>();
        if (own is { } patient)
        {
            subjects.Add(patient);
            var member = await db.FamilyMembers.AsNoTracking().SingleOrDefaultAsync(m => m.PatientId == patient && m.IsActive, ct);
            if (member is not null)
            {
                var children = await db.FamilyMembers.AsNoTracking().Where(m => m.FamilyId == member.FamilyId && m.IsActive && m.Role == FamilyMemberRole.Child)
                    .Select(m => m.PatientId).ToArrayAsync(ct);
                foreach (var child in children)
                {
                    // Reuse exactly the dispatch policy; a historical inbox cannot preserve revoked family authority.
                    var subject = new NotificationEvent("Reservation.Created", "Reservation", child, "visibility", "visibility", child, null, null, null, clock.UtcNow);
                    if ((await eligibility.ResolveAsync(subject, ct)).Any(r => r.UserId == user)) subjects.Add(child);
                }
            }
        }
        var access = await store.GetAccessSnapshotAsync(user, ct);
        var permissions = access?.Permissions.Intersect(current.Permissions, StringComparer.OrdinalIgnoreCase).ToArray() ?? [];
        var allowedSources = ClinicalSources
            .Where(s => permissions.Contains(NotificationEligibility.SourceReadPermission(s), StringComparer.OrdinalIgnoreCase)).ToArray();
        var visibleClinical = VisibleClinical(user, allowedSources);
        return from n in db.UserNotifications.AsNoTracking()
               join d in db.NotificationDeliveries on n.NotificationDeliveryId equals d.Id
               join e in db.NotificationEvents on d.NotificationEventId equals e.Id
               where n.ApplicationUserId == user && n.TemplateVersion == NotificationPolicy.TemplateVersion &&
                   (e.DataClassification == NotificationDataClassification.Clinical ? visibleClinical.Any(c => c.Id == e.Id) :
                       e.Category == NotificationCategory.DoctorOperational ? db.DoctorPractices.Any(p => p.Id == e.DoctorPracticeId && p.IsActive && db.Doctors.Any(doc => doc.Id == p.DoctorId && doc.ApplicationUserId == user && doc.ApprovalStatus == DoctorApprovalStatus.Approved)) :
                       e.Category == NotificationCategory.AccountGovernance ? e.TargetApplicationUserId == user : e.PatientId.HasValue && subjects.Contains(e.PatientId.Value))
               select n;
    }

    public async Task<Result<NotificationPage<InboxItem>>> InboxAsync(InboxFilter filter, CancellationToken ct)
    {
        if (await ActorAsync(PermissionNames.NotificationsViewOwn, false, ct) is not { } user) return Result<NotificationPage<InboxItem>>.Fail(Denied);
        var q = await VisibleInboxAsync(user, ct);
        if (filter.Category is { } category) q = q.Where(n => n.Category == category);
        if (filter.IsRead is { } read) q = q.Where(n => (n.ReadAtUtc != null) == read);
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(n => n.CreatedAtUtc).ThenByDescending(n => n.Id).Skip((filter.PageNumber - 1) * filter.PageSize).Take(filter.PageSize).ToArrayAsync(ct);
        var language = await db.NotificationPreferences.Where(p => p.ApplicationUserId == user).Select(p => p.PreferredLanguage).FirstOrDefaultAsync(ct);
        var items = rows.Select(n =>
        {
            var text = NotificationTemplates.Render(n.TemplateCode, n.TemplateVersion, language);
            return new InboxItem(n.Id, n.EventType, n.Category, text.Title, text.Message, n.CreatedAtUtc, n.ReadAtUtc, n.ReadAtUtc.HasValue,
                new(n.TargetResourceType, n.TargetResourceKey));
        }).ToArray();
        return Result<NotificationPage<InboxItem>>.Ok(new(items, filter.PageNumber, filter.PageSize, total));
    }
    public async Task<Result<UnreadCountResponse>> UnreadAsync(CancellationToken ct)
    {
        if (await ActorAsync(PermissionNames.NotificationsViewOwn, false, ct) is not { } user) return Result<UnreadCountResponse>.Fail(Denied);
        return Result<UnreadCountResponse>.Ok(new(await (await VisibleInboxAsync(user, ct)).CountAsync(n => n.ReadAtUtc == null, ct)));
    }
    public async Task<Result> MarkReadAsync(Guid? id, CancellationToken ct)
    {
        if (await ActorAsync(PermissionNames.NotificationsMarkReadOwn, false, ct) is not { } user) return Result.Fail(Denied);
        var q = await VisibleInboxAsync(user, ct);
        if (id is { } item) q = q.Where(n => n.Id == item);
        if (id.HasValue && !await q.AnyAsync(ct)) return Result.Fail(Missing);
        var now = clock.UtcNow;
        await db.UserNotifications.Where(n => q.Select(v => v.Id).Contains(n.Id) && n.ReadAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAtUtc, now), ct);
        return Result.Ok();
    }

    private static string PreferenceVersion(IEnumerable<NotificationPreference> rows)
        => Convert.ToBase64String(SHA256.HashData(rows.OrderBy(p => p.Category).ThenBy(p => p.Channel).SelectMany(p => p.RowVersion).ToArray()));
    private static PreferencesResponse Preferences(IReadOnlyList<NotificationPreference> rows)
        => new(rows.Count > 0 ? rows[0].PreferredLanguage : "ar", (from category in new[] { NotificationCategory.AppointmentReminder, NotificationCategory.ClinicalAvailability }
                                                                   from channel in Enum.GetValues<NotificationChannel>()
                                                                   let row = rows.SingleOrDefault(p => p.Category == category && p.Channel == channel)
                                                                   select new PreferenceItem(category, channel, row?.Enabled ?? NotificationPolicy.DefaultEnabled(category, channel))).ToArray(), PreferenceVersion(rows));
    public async Task<Result<PreferencesResponse>> PreferencesAsync(CancellationToken ct)
    {
        if (await ActorAsync(PermissionNames.NotificationsPreferencesViewOwn, false, ct) is not { } user) return Result<PreferencesResponse>.Fail(Denied);
        return Result<PreferencesResponse>.Ok(Preferences(await db.NotificationPreferences.AsNoTracking().Where(p => p.ApplicationUserId == user).ToArrayAsync(ct)));
    }
    public async Task<Result<PreferencesResponse>> UpdatePreferencesAsync(UpdatePreferencesRequest request, CancellationToken ct)
    {
        if (await ActorAsync(PermissionNames.NotificationsPreferencesManageOwn, false, ct) is not { } user) return Result<PreferencesResponse>.Fail(Denied);
        await NotificationSourceLock.AcquireAsync(db, $"Wasla:Notification:Preferences:{user:N}", "Transaction", ct);
        var rows = await db.NotificationPreferences.Where(p => p.ApplicationUserId == user).ToListAsync(ct);
        if (request.RowVersion != PreferenceVersion(rows)) return Result<PreferencesResponse>.Fail(Conflict);
        foreach (var category in new[] { NotificationCategory.AppointmentReminder, NotificationCategory.ClinicalAvailability })
            foreach (var channel in Enum.GetValues<NotificationChannel>())
            {
                var row = rows.SingleOrDefault(p => p.Category == category && p.Channel == channel);
                var enabled = request.Preferences.SingleOrDefault(p => p.Category == category && p.Channel == channel)?.Enabled ?? row?.Enabled ?? NotificationPolicy.DefaultEnabled(category, channel);
                if (row is null) { row = new(user, category, channel, enabled, request.PreferredLanguage, clock.UtcNow); rows.Add(row); db.NotificationPreferences.Add(row); }
                else row.Update(enabled, request.PreferredLanguage, clock.UtcNow);
            }
        db.NotificationAudits.Add(new(Guid.NewGuid(), user, null, "PreferencesChanged", "Accepted", null, clock.UtcNow));
        await db.SaveChangesAsync(ct);
        return Result<PreferencesResponse>.Ok(Preferences(rows));
    }
    private static NotificationSettingsResponse Settings(PlatformNotificationSettings s) => new(s.AppointmentReminderEnabled, s.ReminderLeadMinutes, s.Version, Convert.ToBase64String(s.RowVersion));
    public async Task<Result<NotificationSettingsResponse>> SettingsAsync(CancellationToken ct)
    {
        if (await ActorAsync(PermissionNames.NotificationsSettingsView, true, ct) is null) return Result<NotificationSettingsResponse>.Fail(Denied);
        return Result<NotificationSettingsResponse>.Ok(Settings(await db.PlatformNotificationSettings.AsNoTracking().SingleAsync(ct)));
    }
    public async Task<Result<NotificationSettingsResponse>> UpdateSettingsAsync(UpdateNotificationSettingsRequest request, CancellationToken ct)
    {
        if (await ActorAsync(PermissionNames.NotificationsSettingsManage, true, ct) is not { } user) return Result<NotificationSettingsResponse>.Fail(Denied);
        await NotificationSourceLock.AcquireAsync(db, "Wasla:Notification:Settings", "Transaction", ct);
        var settings = await db.PlatformNotificationSettings.SingleAsync(ct);
        if (request.RowVersion != Convert.ToBase64String(settings.RowVersion)) return Result<NotificationSettingsResponse>.Fail(Conflict);
        db.PlatformNotificationSettingsAudits.Add(settings.Update(request.AppointmentReminderEnabled, request.ReminderLeadMinutes, user, request.Reason, clock.UtcNow));
        await db.SaveChangesAsync(ct); return Result<NotificationSettingsResponse>.Ok(Settings(settings));
    }

    private sealed class DeliveryRow
    {
        public NotificationDelivery Delivery { get; init; } = null!;
        public NotificationEvent Event { get; init; } = null!;
        public EmailOutboxMessage? Outbox { get; init; }
    }
    private IQueryable<DeliveryRow> DeliveryRows => from d in db.NotificationDeliveries.AsNoTracking()
                                                    join e in db.NotificationEvents.AsNoTracking() on d.NotificationEventId equals e.Id
                                                    from m in db.EmailOutboxMessages.AsNoTracking().Where(m => m.NotificationDeliveryId == d.Id).DefaultIfEmpty()
                                                    select new DeliveryRow { Delivery = d, Event = e, Outbox = m };
    private static NotificationDeliveryStatus Status(DeliveryRow row) => row.Outbox?.Status switch
    {
        EmailOutboxStatus.Sent => NotificationDeliveryStatus.Sent,
        EmailOutboxStatus.Failed => NotificationDeliveryStatus.Failed,
        EmailOutboxStatus.Suppressed => NotificationDeliveryStatus.Suppressed,
        _ => row.Delivery.Status
    };
    private static string DeliveryVersion(DeliveryRow row) => Convert.ToBase64String(SHA256.HashData([.. row.Delivery.RowVersion, .. row.Outbox?.RowVersion ?? []]));
    private static DeliveryResponse Delivery(DeliveryRow r) => new(r.Delivery.Id, r.Event.EventType, r.Event.Category, r.Delivery.Channel, Status(r),
        r.Outbox?.AttemptCount ?? 0, r.Delivery.CreatedAtUtc, r.Delivery.DispatchedAtUtc, r.Outbox?.LastAttemptOnUtc is { } attempted ? DateTime.SpecifyKind(attempted, DateTimeKind.Utc) : null,
        r.Delivery.SafeFailureCode ?? (r.Outbox?.LastError is null ? null : "Email.SendFailed"),
        r.Outbox?.Status == EmailOutboxStatus.Failed && r.Delivery.Status != NotificationDeliveryStatus.Suppressed, DeliveryVersion(r));
    public async Task<Result<NotificationPage<DeliveryResponse>>> DeliveriesAsync(DeliveryFilter filter, CancellationToken ct)
    {
        if (await ActorAsync(PermissionNames.NotificationsDeliveriesView, true, ct) is null) return Result<NotificationPage<DeliveryResponse>>.Fail(Denied);
        var q = DeliveryRows;
        if (filter.Channel is { } channel) q = q.Where(r => r.Delivery.Channel == channel);
        if (filter.EventType is { } type) q = q.Where(r => r.Event.EventType == type);
        if (filter.FromUtc is { } from) q = q.Where(r => r.Delivery.CreatedAtUtc >= from);
        if (filter.ToUtc is { } to) q = q.Where(r => r.Delivery.CreatedAtUtc <= to);
        if (filter.Status is { } status) q = q.Where(r => (r.Outbox != null && r.Outbox.Status == EmailOutboxStatus.Sent ? NotificationDeliveryStatus.Sent :
            r.Outbox != null && r.Outbox.Status == EmailOutboxStatus.Failed ? NotificationDeliveryStatus.Failed :
            r.Outbox != null && r.Outbox.Status == EmailOutboxStatus.Suppressed ? NotificationDeliveryStatus.Suppressed : r.Delivery.Status) == status);
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(r => r.Delivery.CreatedAtUtc).ThenByDescending(r => r.Delivery.Id)
            .Skip((filter.PageNumber - 1) * filter.PageSize).Take(filter.PageSize).ToArrayAsync(ct);
        return Result<NotificationPage<DeliveryResponse>>.Ok(new(rows.Select(Delivery).ToArray(), filter.PageNumber, filter.PageSize, total));
    }
    public async Task<Result<DeliveryResponse>> DeliveryAsync(Guid id, CancellationToken ct)
    {
        if (await ActorAsync(PermissionNames.NotificationsDeliveriesView, true, ct) is null) return Result<DeliveryResponse>.Fail(Denied);
        var row = await DeliveryRows.SingleOrDefaultAsync(r => r.Delivery.Id == id, ct);
        return row is null ? Result<DeliveryResponse>.Fail(Missing) : Result<DeliveryResponse>.Ok(Delivery(row));
    }
    public async Task<Result> RetryAsync(Guid id, RetryDeliveryRequest request, CancellationToken ct)
    {
        var user = await ActorAsync(PermissionNames.NotificationsDeliveriesRetry, true, ct);
        // Audit authenticated denials independently; permission checks in the API also write denial evidence.
        var actor = user ?? current.UserId;
        if (user is null)
        {
            if (actor is { } deniedActor) { db.NotificationAudits.Add(new(Guid.NewGuid(), deniedActor, id, "DeliveryRetryDenied", "AccessDenied", null, clock.UtcNow)); await db.SaveChangesAsync(ct); }
            return Result.Fail(Denied);
        }
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var row = await DeliveryRows.SingleOrDefaultAsync(r => r.Delivery.Id == id, ct);
        if (row is not null) await NotificationSourceLock.AcquireAsync(db, NotificationSourceLock.Resource(row.Event.SourceType, row.Event.SourceId), "Transaction", ct);
        row = await DeliveryRows.SingleOrDefaultAsync(r => r.Delivery.Id == id, ct);
        var denial = row is null ? "Delivery.NotFound" : request.RowVersion != DeliveryVersion(row) ? "Delivery.Conflict" :
            row.Outbox?.Status != EmailOutboxStatus.Failed || row.Delivery.Status == NotificationDeliveryStatus.Suppressed ? "Delivery.InvalidRetryState" :
            await emailGuard.DenialAsync(id, row.Outbox.RecipientEmail, ct);
        db.NotificationAudits.Add(new(Guid.NewGuid(), user.Value, id, "DeliveryRetryRequested", "Requested", request.Reason.Trim(), clock.UtcNow));
        if (denial is null && row?.Outbox is { } message)
        {
            var changed = await db.EmailOutboxMessages.Where(m => m.Id == message.Id && m.Status == EmailOutboxStatus.Failed && m.RowVersion == message.RowVersion)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.Status, EmailOutboxStatus.Pending).SetProperty(m => m.NextAttemptOnUtc, clock.UtcNow)
                    .SetProperty(m => m.ProcessingToken, (Guid?)null).SetProperty(m => m.LastError, (string?)null).SetProperty(m => m.AttemptCount, 0), ct);
            if (changed != 1) denial = "Delivery.Conflict";
        }
        db.NotificationAudits.Add(new(Guid.NewGuid(), user.Value, id, denial is null ? "DeliveryRetryAccepted" : "DeliveryRetryDenied", denial ?? "Accepted", null, clock.UtcNow));
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        NotificationTelemetry.Record(logger, "notification.admin.retry", row?.Event.EventType ?? "Unavailable", row?.Delivery.Channel, denial is null ? "Accepted" : "Denied", denial ?? "None");
        return denial is null ? Result.Ok() : Result.Fail(row is null ? Missing : Conflict);
    }
}
