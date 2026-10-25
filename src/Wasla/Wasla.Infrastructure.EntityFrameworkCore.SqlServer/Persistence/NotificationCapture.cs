using Microsoft.EntityFrameworkCore;
using Wasla.Domain.Notifications;
using Wasla.Domain.Reservations;
using Wasla.Domain.Doctors;
using Wasla.Domain.Common;
using Wasla.Domain.Medications;
using Wasla.Domain.Labs;
using Wasla.Domain.Radiology;
using Wasla.Domain.Diagnostics;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Notifications;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

public sealed partial class WaslaDbContext
{
    private bool NotificationCaptureEnabled => notificationOptions?.Value.Enabled ?? true;
    private bool HasNotificationSourceChanges => ChangeTracker.Entries().Any(e => e.State is EntityState.Added or EntityState.Modified &&
        e.Entity is Reservation or Doctor or Prescription or LabRequest or RadiologyRequest or LabResult or RadiologyResult);

    private async Task CaptureNotificationsAsync(CancellationToken ct)
    {
        if (ChangeTracker.Entries().Any(e => e.State is EntityState.Modified or EntityState.Deleted &&
            e.Entity is NotificationEvent or NotificationVerifiedContact or PlatformNotificationSettingsAudit or NotificationAudit))
            throw new InvalidOperationException("Notification event and audit history is immutable.");
        if (!NotificationCaptureEnabled) return;
        var sources = ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified).Select(e => e.Entity).ToArray();
        // Acquire in a stable order. Workers take the identical resource before source revalidation.
        var reservations = sources.OfType<Reservation>().OrderBy(r => r.Id).ToArray();
        foreach (var r in reservations)
            await NotificationSourceLock.AcquireAsync(this, NotificationSourceLock.Resource("Reservation", r.Id), "Transaction", ct);
        foreach (var r in reservations)
        {
            var revision = Wasla.Application.Features.Notifications.AppointmentRevisionIdentity.Current(r);
            revision ??= (await Set<ReservationHistory>().Where(h => h.ReservationId == r.Id &&
                (h.EventType == ReservationHistoryEventType.Created || h.EventType == ReservationHistoryEventType.Rescheduled && h.NewScheduledStartUtc == r.ScheduledStartUtc))
                .OrderByDescending(h => h.EventType).ThenByDescending(h => h.OccurredOnUtc).ThenByDescending(h => h.Id).Select(h => (Guid?)h.Id).FirstOrDefaultAsync(ct))?.ToString("N");
            if (revision is null) continue;
            var settings = await PlatformNotificationSettings.AsNoTracking().SingleOrDefaultAsync(ct) ?? new PlatformNotificationSettings();
            foreach (var history in r.History.Where(h => Entry(h).State == EntityState.Added).ToArray())
            {
                var type = history.EventType == ReservationHistoryEventType.ConvertedToTicket ? "Patient.CheckedIn" : $"Reservation.{history.EventType}";
                var legacy = history.EventType is ReservationHistoryEventType.Created or ReservationHistoryEventType.Cancelled or ReservationHistoryEventType.Rescheduled &&
                    !(notificationOptions?.Value.OwnReservationEmails ?? false);
                await AddNotificationAsync(type, "Reservation", r.Id, history.Id.ToString("N"), revision, r.PatientId, r.DoctorId,
                    r.DoctorPracticeId, null, history.OccurredOnUtc, legacy, ct: ct);
                if (history.EventType is ReservationHistoryEventType.Created or ReservationHistoryEventType.Cancelled)
                    await AddNotificationAsync(history.EventType == ReservationHistoryEventType.Created ? "Doctor.NewBooking" : "Doctor.BookingCancelled",
                        "Reservation", r.Id, history.Id.ToString("N"), revision, null, r.DoctorId, r.DoctorPracticeId, null, history.OccurredOnUtc, ct: ct);
                if (history.EventType is ReservationHistoryEventType.Cancelled or ReservationHistoryEventType.Rescheduled or ReservationHistoryEventType.Expired or ReservationHistoryEventType.ConvertedToTicket)
                {
                    var ids = await NotificationEvents.Where(e => e.SourceType == "Reservation" && e.SourceId == r.Id && e.EventType == "Appointment.ReminderDue")
                        .Select(e => e.Id).ToArrayAsync(ct);
                    var pending = await NotificationIntents.Where(i => ids.Contains(i.NotificationEventId) &&
                        (i.Status == NotificationIntentStatus.Scheduled || i.Status == NotificationIntentStatus.Processing)).ToArrayAsync(ct);
                    foreach (var intent in pending) intent.Finish(NotificationIntentStatus.Cancelled, history.OccurredOnUtc, "Source.Changed");
                }
                if (history.EventType == ReservationHistoryEventType.RestoredFromNoShow)
                {
                    var ids = await NotificationEvents.Where(e => e.SourceType == "Reservation" && e.SourceId == r.Id && e.EventType == "Appointment.ReminderDue")
                        .Select(e => e.Id).ToArrayAsync(ct);
                    var pending = await NotificationIntents.Where(i => ids.Contains(i.NotificationEventId) && i.DueAtUtc <= history.OccurredOnUtc &&
                        (i.Status == NotificationIntentStatus.Scheduled || i.Status == NotificationIntentStatus.Processing)).ToArrayAsync(ct);
                    foreach (var intent in pending) intent.Finish(NotificationIntentStatus.Cancelled, history.OccurredOnUtc, "Reminder.RestoredAfterDue");
                }
                if (history.EventType is ReservationHistoryEventType.Created or ReservationHistoryEventType.Rescheduled &&
                    settings.AppointmentReminderEnabled && r.Status == ReservationStatus.Active)
                {
                    var due = NotificationPolicy.ReminderDue(r.ScheduledStartUtc, settings.ReminderLeadMinutes);
                    if (history.OccurredOnUtc < due)
                        await AddNotificationAsync("Appointment.ReminderDue", "Reservation", r.Id, revision, revision, r.PatientId, r.DoctorId,
                            r.DoctorPracticeId, null, history.OccurredOnUtc, due: due, appointment: r.ScheduledStartUtc,
                            lead: settings.ReminderLeadMinutes, settings: settings.Version, ct: ct);
                }
            }
        }
        foreach (var doctor in sources.OfType<Doctor>())
            foreach (var h in ChangeTracker.Entries<DoctorStatusHistory>().Where(e => e.State == EntityState.Added && e.Entity.DoctorId == doctor.Id).Select(e => e.Entity).ToArray())
            {
                var type = h.ToStatus switch { DoctorApprovalStatus.Approved => "Doctor.Approved", DoctorApprovalStatus.Rejected => "Doctor.Rejected", DoctorApprovalStatus.Suspended => "Doctor.Suspended", _ => null };
                if (type is not null) await AddNotificationAsync(type, "Doctor", doctor.Id, h.Id.ToString("N"), h.Id.ToString("N"), null, doctor.Id,
                    null, doctor.ApplicationUserId, h.OccurredOnUtc, true, ct: ct);
            }
        foreach (var p in sources.OfType<Prescription>())
            foreach (var v in p.Versions.Where(v => v.Status == PrescriptionVersionStatus.Finalized && Entry(v).State is EntityState.Added or EntityState.Modified))
                await AddNotificationAsync("Prescription.Available", "Prescription", p.Id, v.Id.ToString("N"), v.Id.ToString("N"), p.PatientId, p.DoctorId, null, null, v.FinalizedAtUtc!.Value, ct: ct);
        foreach (var r in sources.OfType<LabRequest>().Where(r => r.Status != DiagnosticRequestStatus.Draft))
            foreach (var h in r.History.Where(h => h.Action == "Published" && Entry(h).State is EntityState.Added or EntityState.Detached))
                await AddNotificationAsync("Lab.RequestAvailable", "LabRequest", r.Id, h.Id.ToString("N"), h.Id.ToString("N"), r.PatientId, r.DoctorId, r.DoctorPracticeId, null, h.OccurredAtUtc, ct: ct);
        foreach (var r in sources.OfType<RadiologyRequest>().Where(r => r.Status != DiagnosticRequestStatus.Draft))
            foreach (var h in r.History.Where(h => h.Action == "Published" && Entry(h).State is EntityState.Added or EntityState.Detached))
                await AddNotificationAsync("Radiology.RequestAvailable", "RadiologyRequest", r.Id, h.Id.ToString("N"), h.Id.ToString("N"), r.PatientId, r.DoctorId, r.DoctorPracticeId, null, h.OccurredAtUtc, ct: ct);
        foreach (var r in sources.OfType<LabResult>())
            foreach (var h in r.History.Where(h => Entry(h).State == EntityState.Added))
            {
                var type = h.Action switch { "Recorded" => "Lab.OfficialResultAvailable", "Corrected" => "DiagnosticResult.Corrected", "Voided" => "DiagnosticResult.Voided", _ => null };
                if (type is not null) await AddNotificationAsync(type, "LabResult", r.Id, h.Id.ToString("N"), r.Versions.OrderBy(v => v.VersionNumber).Last().Id.ToString("N"), r.PatientId, r.DoctorId, r.DoctorPracticeId, null, h.OccurredAtUtc, ct: ct);
            }
        foreach (var r in sources.OfType<RadiologyResult>())
            foreach (var h in r.History.Where(h => Entry(h).State == EntityState.Added))
            {
                var type = h.Action switch { "Recorded" => "Radiology.OfficialResultAvailable", "Corrected" => "DiagnosticResult.Corrected", "Voided" => "DiagnosticResult.Voided", _ => null };
                if (type is not null) await AddNotificationAsync(type, "RadiologyResult", r.Id, h.Id.ToString("N"), r.Versions.OrderBy(v => v.VersionNumber).Last().Id.ToString("N"), r.PatientId, r.DoctorId, r.DoctorPracticeId, null, h.OccurredAtUtc, ct: ct);
            }
    }

    private async Task AddNotificationAsync(string type, string source, Guid id, string key, string revision, Guid? patient,
        Guid? doctor, Guid? practice, Guid? user, DateTime now, bool legacy = false, DateTime? due = null,
        DateTime? appointment = null, int? lead = null, long? settings = null, CancellationToken ct = default)
    {
        var e = new NotificationEvent(type, source, id, key, revision, patient, doctor, practice, user, DateTime.SpecifyKind(now, DateTimeKind.Utc), legacy);
        if (NotificationEvents.Local.Any(x => x.IdempotencyKey == e.IdempotencyKey) || await NotificationEvents.AnyAsync(x => x.IdempotencyKey == e.IdempotencyKey, ct)) return;
        NotificationEvents.Add(e); NotificationIntents.Add(new NotificationIntent(e, due ?? e.OccurredAtUtc, appointment, lead, settings));
        NotificationTelemetry.Record(notificationLogger, "notification.event.captured", type, null, "Staged");
        NotificationTelemetry.Record(notificationLogger, "notification.intent.scheduled", type, null, "Staged");
    }
}
