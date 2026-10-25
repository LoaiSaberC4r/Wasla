using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.Notifications;
using Wasla.Domain.Notifications;
using Wasla.Domain.Reservations;
using static Wasla.Tests.Integration.Phase13ApiFixture;
using static Wasla.Tests.Integration.Phase17NotificationFixture;

namespace Wasla.Tests.Integration;

public sealed class Phase17LifecycleNotificationTests
{
    [Fact]
    public async Task A14_O15_legacy_nullable_revision_can_restore_without_backfill_or_invented_revision()
    {
        await using var app = await Phase13ApiFixture.CreateAsync();
        var options = app.Services.GetRequiredService<IOptions<NotificationOptions>>().Value;
        options.Enabled = false; var id = await ReservationAsync(app, trusted: true); options.Enabled = true;
        await app.WithDbAsync(async db =>
        {
            await db.Reservations.Where(r => r.Id == id).ExecuteUpdateAsync(s => s.SetProperty(r => r.AppointmentRevisionHistoryId, (Guid?)null));
            var r = await db.Reservations.Include(r => r.History).SingleAsync(r => r.Id == id);
            Assert.True(r.MarkNoShow(null, app.Clock.UtcNow).IsSuccess); await db.SaveChangesAsync();
            Assert.True(r.RestoreFromNoShow(app.DoctorUserId, app.Clock.UtcNow).IsSuccess); await db.SaveChangesAsync();
            Assert.Null(r.AppointmentRevisionHistoryId);
        });
        await DispatchAsync(app);
        await app.WithDbAsync(async db =>
        {
            Assert.False(await db.NotificationEvents.AnyAsync(e => e.SourceId == id && e.EventType == "Appointment.ReminderDue"));
            Assert.True(await db.UserNotifications.AnyAsync(n => n.EventType == "Reservation.RestoredFromNoShow"));
            Assert.True(await db.EmailOutboxMessages.AnyAsync(m => m.NotificationDeliveryId != null));
        });
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A16_E03_successful_normal_and_force_checkin_capture_one_attendance_update(bool force)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(); var id = await ReservationAsync(app, trusted: true);
        using var reception = await app.ClientAsync("reception");
        if (!force) app.Clock.UtcNow = app.Clock.UtcNow.AddHours(4).AddMinutes(-10);
        var key = Guid.NewGuid().ToString("N"); var url = $"/api/v1/practices/{app.PracticeId}/reservations/{id}/" + (force ? "force-check-in" : "check-in");
        var body = new { paidAmount = 300, paymentMethod = "Cash", reason = "Approved attendance" };
        using (var checkedIn = await PostAsync(reception, url, body, key)) Assert.Equal(HttpStatusCode.Created, checkedIn.StatusCode);
        using (var replay = await PostAsync(reception, url, body, key)) Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        await DispatchAsync(app);
        await app.WithDbAsync(async db =>
        {
            var e = await db.NotificationEvents.SingleAsync(e => e.SourceId == id && e.EventType == "Patient.CheckedIn");
            Assert.Single(await db.NotificationDeliveries.Where(d => d.NotificationEventId == e.Id).ToArrayAsync());
            var reminder = await db.NotificationEvents.SingleAsync(e => e.SourceId == id && e.EventType == "Appointment.ReminderDue");
            Assert.Equal(NotificationIntentStatus.Cancelled, (await db.NotificationIntents.SingleAsync(i => i.NotificationEventId == reminder.Id)).Status);
            Assert.False(await db.EmailOutboxMessages.AnyAsync(m => m.NotificationDeliveryId != null));
        });
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task E02_NoShow_and_expiration_capture_only_inapp_and_do_not_change_source_lifecycle(bool expire)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(); var id = await ReservationAsync(app, trusted: true);
        await app.WithDbAsync(async db =>
        {
            var r = await db.Reservations.Include(r => r.History).SingleAsync(r => r.Id == id);
            Assert.True((expire ? r.Expire(app.Clock.UtcNow) : r.MarkNoShow(null, app.Clock.UtcNow)).IsSuccess); await db.SaveChangesAsync();
        });
        await DispatchAsync(app);
        await app.WithDbAsync(async db =>
        {
            var e = await db.NotificationEvents.SingleAsync(e => e.SourceId == id && e.EventType == (expire ? "Reservation.Expired" : "Reservation.MarkedNoShow"));
            Assert.True(await db.UserNotifications.AnyAsync(n => n.EventType == e.EventType));
            Assert.False(await db.NotificationDeliveries.AnyAsync(d => d.NotificationEventId == e.Id && d.Channel == NotificationChannel.Email));
        });
    }
    [Fact]
    public async Task A10_new_reschedule_can_have_its_own_reminder_without_retracting_prior_delivery()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(); var id = await ReservationAsync(app, trusted: true);
        app.Clock.UtcNow = app.Clock.UtcNow.AddHours(2); await DispatchAsync(app);
        Assert.Equal(1, await SendEmailsAsync(app));
        await app.WithDbAsync(async db =>
        {
            var r = await db.Reservations.Include(r => r.History).SingleAsync(r => r.Id == id);
            Assert.True(r.Reschedule(r.ScheduledStartUtc.AddHours(2), r.ScheduledLocalDateTime.AddHours(2), r.BusinessDate, 20, r.TimeZoneIdSnapshot,
                ReservationActionInitiator.Doctor, app.DoctorUserId, true, "Patient agreed", app.Clock.UtcNow).IsSuccess); await db.SaveChangesAsync();
            Assert.Equal(2, await db.NotificationEvents.CountAsync(e => e.SourceId == id && e.EventType == "Appointment.ReminderDue"));
            Assert.Single(await db.EmailOutboxMessages.Where(m => m.NotificationDeliveryId != null).ToArrayAsync());
            Assert.True(await db.EmailOutboxMessages.AnyAsync(m => m.NotificationDeliveryId != null && m.Status == Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Email.EmailOutboxStatus.Sent));
        });
        app.Clock.UtcNow = app.Clock.UtcNow.AddHours(2); await DispatchAsync(app);
        await app.WithDbAsync(async db => Assert.Equal(2, await db.EmailOutboxMessages.CountAsync(m => m.NotificationDeliveryId != null)));
    }
    [Fact]
    public async Task A20_A21_timezone_configuration_changes_do_not_recompute_committed_utc_or_reminder_snapshots()
    {
        await using var app = await Phase13ApiFixture.CreateAsync();
        var start = new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
        var id = await ReservationAsync(app, start, trusted: true);
        await app.WithDbAsync(async db =>
        {
            await db.DoctorPracticeConfigurations.Where(c => c.DoctorPracticeId == app.PracticeId).ExecuteUpdateAsync(s => s.SetProperty(c => c.TimeZoneId, "UTC"));
            var r = await db.Reservations.SingleAsync(r => r.Id == id);
            Assert.Equal(start, r.ScheduledStartUtc); Assert.Equal("Africa/Cairo", r.TimeZoneIdSnapshot);
            var e = await db.NotificationEvents.SingleAsync(e => e.SourceId == id && e.EventType == "Appointment.ReminderDue");
            var intent = await db.NotificationIntents.SingleAsync(i => i.NotificationEventId == e.Id);
            Assert.Equal(start.AddMinutes(-120), intent.DueAtUtc); Assert.Equal(start, intent.AppointmentStartUtcSnapshot);
        });
        app.Clock.UtcNow = start.AddMinutes(-120); await DispatchAsync(app);
        await app.WithDbAsync(async db => Assert.True(await db.UserNotifications.AnyAsync(n => n.EventType == "Appointment.ReminderDue")));
    }
    [Fact]
    public async Task A22_closed_day_suppresses_reminder_without_mutating_no_show_or_expiration_lifecycle()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(); var id = await ReservationAsync(app, trusted: true);
        await app.WithDbAsync(async db =>
        {
            var day = await db.PracticeOperationalDays.SingleAsync(d => d.DoctorPracticeId == app.PracticeId);
            var end = app.Clock.UtcNow.AddHours(1);
            Assert.True(day.Revise(day.TimeZoneId, end, new string('C', 64), app.Clock.UtcNow));
            app.Clock.UtcNow = end; day.Close(end); await db.SaveChangesAsync();
        });
        app.Clock.UtcNow = app.Clock.UtcNow.AddHours(1); await DispatchAsync(app);
        await app.WithDbAsync(async db =>
        {
            Assert.Equal(ReservationStatus.Active, (await db.Reservations.SingleAsync(r => r.Id == id)).Status);
            var e = await db.NotificationEvents.SingleAsync(e => e.SourceId == id && e.EventType == "Appointment.ReminderDue");
            Assert.Equal("Reminder.OperationalDayClosed", (await db.NotificationIntents.SingleAsync(i => i.NotificationEventId == e.Id)).SkipReasonCode);
            Assert.False(await db.NotificationDeliveries.AnyAsync(d => d.NotificationEventId == e.Id));
        });
    }
    [Fact]
    public async Task R05_E01_O01_controlled_email_cutover_has_one_owner_and_replay_does_not_recapture()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(); using var patient = await app.ClientAsync("patient");
        app.Services.GetRequiredService<IOptions<NotificationOptions>>().Value.OwnReservationEmails = true;
        await app.WithDbAsync(async db => { var user = (await db.PatientAccountLinks.SingleAsync(l => l.PatientId == app.PatientId)).ApplicationUserId; await TrustEmailAsync(db, user, app.Clock.UtcNow); await db.SaveChangesAsync(); });
        var key = Guid.NewGuid().ToString("N"); var body = new { patientId = app.PatientId, doctorPracticeId = app.PracticeId, businessDate = app.Today, slotStartTime = "18:00", segmentId = app.SegmentId, visitTypeId = app.ConsultationTypeId };
        using var created = await PostAsync(patient, "/api/v1/reservations", body, key); Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await JsonAsync(created)).GetProperty("reservationId").GetGuid();
        using (var replay = await PostAsync(patient, "/api/v1/reservations", body, key)) Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        await DispatchAsync(app); await DispatchAsync(app);
        await app.WithDbAsync(async db =>
        {
            var e = await db.NotificationEvents.SingleAsync(e => e.SourceId == id && e.EventType == "Reservation.Created"); Assert.False(e.LegacyEmailOwned);
            var message = Assert.Single(await db.EmailOutboxMessages.Where(m => m.IdempotencyKey.StartsWith("reservation-created:")).ToArrayAsync());
            Assert.NotNull(message.NotificationDeliveryId); Assert.Contains(e.SourceEventKey, message.IdempotencyKey, StringComparison.Ordinal);
        });
    }
    [Fact]
    public async Task A17_O18_walkin_and_feature_disable_never_create_historical_reminders()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(); using var reception = await app.ClientAsync("reception");
        await app.WalkInAsync(reception);
        await app.WithDbAsync(async db => Assert.False(await db.NotificationEvents.AnyAsync(e => e.EventType == "Appointment.ReminderDue")));
        var options = app.Services.GetRequiredService<IOptions<NotificationOptions>>().Value;
        options.Enabled = false;
        await ReservationAsync(app, patientId: app.OtherPatientId);
        options.Enabled = true; await DispatchAsync(app);
        await app.WithDbAsync(async db => Assert.False(await db.NotificationEvents.AnyAsync(e => e.EventType == "Appointment.ReminderDue")));
    }
    [Fact]
    public async Task A18_platform_setting_audit_keeps_existing_due_snapshot()
    {
        await using var app = await Phase14MedicationApiTests.CreateAsync(false); var old = await ReservationAsync(app);
        using var admin = await app.ClientAsync("admin"); var settings = await GetAsync(admin, "/api/v1/admin/notification-settings");
        using var update = await admin.PutAsJsonAsync("/api/v1/admin/notification-settings", new { appointmentReminderEnabled = true, reminderLeadMinutes = 90, reason = "Approved test update", rowVersion = settings.GetProperty("rowVersion").GetString() }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var newer = await ReservationAsync(app, app.Clock.UtcNow.AddHours(5), app.OtherPatientId);
        await app.WithDbAsync(async db =>
        {
            var oldEvent = await db.NotificationEvents.SingleAsync(e => e.SourceId == old && e.EventType == "Appointment.ReminderDue");
            var newEvent = await db.NotificationEvents.SingleAsync(e => e.SourceId == newer && e.EventType == "Appointment.ReminderDue");
            Assert.Equal(120, (await db.NotificationIntents.SingleAsync(i => i.NotificationEventId == oldEvent.Id)).ReminderLeadMinutesSnapshot);
            Assert.Equal(90, (await db.NotificationIntents.SingleAsync(i => i.NotificationEventId == newEvent.Id)).ReminderLeadMinutesSnapshot);
            Assert.Single(await db.PlatformNotificationSettingsAudits.ToArrayAsync());
        });
    }
}
