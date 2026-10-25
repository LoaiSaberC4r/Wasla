using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Wasla.Application.Features.Notifications;
using Wasla.Domain.Notifications;
using Wasla.Domain.Reservations;
using Wasla.Domain.Patients;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Email;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Notifications;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Options;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;
using static Wasla.Tests.Integration.Phase13ApiFixture;
using static Wasla.Tests.Integration.Phase17NotificationFixture;

namespace Wasla.Tests.Integration;

public sealed class Phase17NotificationTests
{
    [Theory]
    [InlineData("A01", 1, 1)]
    [InlineData("A02", 0, 0)]
    [InlineData("A03", -1, 0)]
    public async Task Capture_schedules_only_strictly_future_due_times(string caseId, int secondsBeforeDue, int expected)
    {
        Assert.NotEmpty(caseId);
        await using var app = await Phase13ApiFixture.CreateAsync();
        var id = await ReservationAsync(app, app.Clock.UtcNow.AddMinutes(120).AddSeconds(secondsBeforeDue));
        await app.WithDbAsync(async db =>
        {
            var source = await db.NotificationEvents.Where(e => e.SourceId == id).ToArrayAsync();
            Assert.Single(source, e => e.EventType == "Reservation.Created");
            Assert.Single(source, e => e.EventType == "Doctor.NewBooking");
            Assert.Equal(expected, source.Count(e => e.EventType == "Appointment.ReminderDue"));
            Assert.Equal(expected + 2, await db.NotificationIntents.CountAsync(i => source.Select(e => e.Id).Contains(i.NotificationEventId)));
        });
    }
    [Theory]
    [InlineData("A04", 0, true)]
    [InlineData("A05", 900, true)]
    [InlineData("A06", 901, false)]
    [InlineData("A07", 7200, false)]
    public async Task Dispatcher_obeys_due_grace_and_appointment_boundaries(string caseId, int secondsAfterDue, bool delivered)
    {
        Assert.NotEmpty(caseId);
        await using var app = await Phase13ApiFixture.CreateAsync();
        var id = await ReservationAsync(app, trusted: true);
        await DispatchAsync(app);
        app.Clock.UtcNow = app.Clock.UtcNow.AddHours(2).AddSeconds(secondsAfterDue);
        await DispatchAsync(app);
        await app.WithDbAsync(async db =>
        {
            var source = await db.NotificationEvents.SingleAsync(e => e.SourceId == id && e.EventType == "Appointment.ReminderDue");
            var intent = await db.NotificationIntents.SingleAsync(i => i.NotificationEventId == source.Id);
            Assert.Equal(delivered ? NotificationIntentStatus.Dispatched : NotificationIntentStatus.Skipped, intent.Status);
            Assert.Equal(delivered ? 2 : 0, await db.NotificationDeliveries.CountAsync(d => d.NotificationEventId == source.Id && d.Status == NotificationDeliveryStatus.Queued));
        });
    }
    [Fact]
    public async Task A08_A09_A11_A12_mutations_preserve_history_and_invalidate_previous_revisions()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(); var id = await ReservationAsync(app);
        Guid oldRevision = Guid.Empty;
        await app.WithDbAsync(async db =>
        {
            var r = await db.Reservations.Include(r => r.History).SingleAsync(r => r.Id == id); oldRevision = r.AppointmentRevisionHistoryId!.Value;
            var count = r.History.Count;
            Assert.True(r.Reschedule(r.ScheduledStartUtc, r.ScheduledLocalDateTime, r.BusinessDate, 20, r.TimeZoneIdSnapshot,
                ReservationActionInitiator.Doctor, app.DoctorUserId, true, "No change", app.Clock.UtcNow).IsFailure);
            Assert.Equal(count, r.History.Count); Assert.Equal(oldRevision, r.AppointmentRevisionHistoryId);
            var start = app.Clock.UtcNow.AddMinutes(60); var local = DateTime.SpecifyKind(start.AddHours(3), DateTimeKind.Unspecified);
            Assert.True(r.Reschedule(start, local, DateOnly.FromDateTime(local), 20, "Africa/Cairo", ReservationActionInitiator.Doctor,
                app.DoctorUserId, true, "Patient consent", app.Clock.UtcNow.AddSeconds(1)).IsSuccess);
            await db.SaveChangesAsync();
        });
        await app.WithDbAsync(async db =>
        {
            var old = await db.NotificationEvents.SingleAsync(e => e.SourceId == id && e.EventType == "Appointment.ReminderDue");
            Assert.Equal(oldRevision.ToString("N"), old.SourceRevisionKey);
            Assert.Equal(NotificationIntentStatus.Cancelled, (await db.NotificationIntents.SingleAsync(i => i.NotificationEventId == old.Id)).Status);
            Assert.Equal(1, await db.NotificationEvents.CountAsync(e => e.SourceId == id && e.EventType == "Reservation.Rescheduled"));
            var r = await db.Reservations.Include(r => r.History).SingleAsync(r => r.Id == id);
            Assert.True(r.Cancel(ReservationCancellationInitiator.Patient, ReservationActionInitiator.Patient, r.CreatedByApplicationUserId,
                ReservationCancellationReasons.PatientChangedPlans, "PRIVATE_REASON", app.Clock.UtcNow.AddSeconds(2)).IsSuccess);
            await db.SaveChangesAsync();
            Assert.Equal(1, await db.NotificationEvents.CountAsync(e => e.SourceId == id && e.EventType == "Reservation.Cancelled"));
        });
    }
    [Theory]
    [InlineData("A13", true)]
    [InlineData("A14", false)]
    public async Task Restore_keeps_revision_and_never_creates_a_second_reminder(string caseId, bool alreadyDispatched)
    {
        Assert.NotEmpty(caseId);
        await using var app = await Phase13ApiFixture.CreateAsync(); var id = await ReservationAsync(app, trusted: true);
        if (alreadyDispatched) { app.Clock.UtcNow = app.Clock.UtcNow.AddHours(2); await DispatchAsync(app); Assert.Equal(1, await SendEmailsAsync(app)); }
        await app.WithDbAsync(async db =>
        {
            var r = await db.Reservations.Include(r => r.History).SingleAsync(r => r.Id == id); var revision = r.AppointmentRevisionHistoryId;
            Assert.True(r.MarkNoShow(null, app.Clock.UtcNow).IsSuccess); await db.SaveChangesAsync();
            Assert.True(r.RestoreFromNoShow(app.DoctorUserId, app.Clock.UtcNow).IsSuccess); await db.SaveChangesAsync();
            Assert.Equal(revision, r.AppointmentRevisionHistoryId);
            Assert.Single(await db.NotificationEvents.Where(e => e.SourceId == id && e.EventType == "Appointment.ReminderDue").ToArrayAsync());
        });
        if (!alreadyDispatched) { app.Clock.UtcNow = app.Clock.UtcNow.AddHours(2); await DispatchAsync(app); }
        await app.WithDbAsync(async db =>
        {
            var events = await db.NotificationEvents.Where(e => e.SourceId == id && e.EventType == "Appointment.ReminderDue").Select(e => e.Id).ToArrayAsync();
            Assert.Equal(2, await db.NotificationDeliveries.CountAsync(d => events.Contains(d.NotificationEventId)));
        });
    }
    [Fact]
    public async Task R03_R04_R13_E01_E05_no_trusted_email_still_commits_and_delivers_only_own_inboxes()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(); var id = await ReservationAsync(app);
        await DispatchAsync(app);
        using var patient = await app.ClientAsync("patient"); using var doctor = await app.ClientAsync("doctor");
        using var reception = await app.ClientAsync("reception"); using var other = await app.ClientAsync("other-patient");
        using var otherDoctor = await app.ClientAsync("other-doctor");
        var inbox = await GetAsync(patient, "/api/v1/notifications/mine");
        Assert.Single(inbox.GetProperty("items").EnumerateArray());
        Assert.Contains("Reservation.Created", inbox.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_BOOKING_NOTE", inbox.GetRawText(), StringComparison.Ordinal);
        Assert.Single((await GetAsync(doctor, "/api/v1/notifications/mine")).GetProperty("items").EnumerateArray());
        Assert.Empty((await GetAsync(reception, "/api/v1/notifications/mine")).GetProperty("items").EnumerateArray());
        Assert.Empty((await GetAsync(other, "/api/v1/notifications/mine")).GetProperty("items").EnumerateArray());
        Assert.Empty((await GetAsync(otherDoctor, "/api/v1/notifications/mine")).GetProperty("items").EnumerateArray());
        app.Clock.UtcNow = app.Clock.UtcNow.AddHours(2); await DispatchAsync(app);
        await app.WithDbAsync(async db =>
        {
            Assert.True(await db.Reservations.AnyAsync(r => r.Id == id));
            Assert.True(await db.NotificationDeliveries.AnyAsync(d => d.Channel == NotificationChannel.Email && d.SafeFailureCode == "Recipient.NoTrustedEmail"));
            Assert.Empty(await db.EmailOutboxMessages.Where(m => m.NotificationDeliveryId != null).ToArrayAsync());
        });
    }
    [Fact]
    public async Task O11_O12_inbox_pagination_counts_and_idempotent_read_are_owner_scoped()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(); await ReservationAsync(app); await DispatchAsync(app);
        using var patient = await app.ClientAsync("patient"); using var other = await app.ClientAsync("other-patient");
        var page = await GetAsync(patient, "/api/v1/notifications/mine?pageNumber=1&pageSize=1");
        var item = page.GetProperty("items")[0].GetProperty("id").GetGuid();
        using (var denied = await other.PostAsJsonAsync($"/api/v1/notifications/mine/{item}/read", new { }, TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.Equal(1, (await GetAsync(patient, "/api/v1/notifications/mine/unread-count")).GetProperty("unreadCount").GetInt32());
        using (var read = await patient.PostAsJsonAsync($"/api/v1/notifications/mine/{item}/read", new { }, TestContext.Current.CancellationToken)) Assert.True(read.IsSuccessStatusCode);
        using (var read = await patient.PostAsJsonAsync($"/api/v1/notifications/mine/{item}/read", new { }, TestContext.Current.CancellationToken)) Assert.True(read.IsSuccessStatusCode);
        Assert.Equal(0, (await GetAsync(patient, "/api/v1/notifications/mine/unread-count")).GetProperty("unreadCount").GetInt32());
        Assert.Empty((await GetAsync(patient, "/api/v1/notifications/mine?pageNumber=2&pageSize=1")).GetProperty("items").EnumerateArray());
        using var invalid = await patient.GetAsync("/api/v1/notifications/mine?pageSize=101", TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task E09_E10_preferences_are_concurrent_independent_and_checked_at_dispatch(bool emailEnabled)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(); await ReservationAsync(app, trusted: true);
        using var patient = await app.ClientAsync("patient"); var preferences = await GetAsync(patient, "/api/v1/notifications/me/preferences");
        var request = new { preferredLanguage = "en", preferences = new[] { new { category = "AppointmentReminder", channel = "Email", enabled = emailEnabled }, new { category = "AppointmentReminder", channel = "InApp", enabled = !emailEnabled } }, rowVersion = preferences.GetProperty("rowVersion").GetString() };
        using (var update = await patient.PutAsJsonAsync("/api/v1/notifications/me/preferences", request, TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        using (var stale = await patient.PutAsJsonAsync("/api/v1/notifications/me/preferences", request, TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        app.Clock.UtcNow = app.Clock.UtcNow.AddHours(2); await DispatchAsync(app);
        await app.WithDbAsync(async db =>
        {
            var e = await db.NotificationEvents.SingleAsync(e => e.EventType == "Appointment.ReminderDue");
            Assert.Equal(emailEnabled, await db.NotificationDeliveries.AnyAsync(d => d.NotificationEventId == e.Id && d.Channel == NotificationChannel.Email && d.Status == NotificationDeliveryStatus.Queued));
            Assert.Equal(!emailEnabled, await db.UserNotifications.AnyAsync(n => n.EventType == "Appointment.ReminderDue"));
        });
    }
    [Fact]
    public async Task A19_O09_C12_api_permissions_admin_metadata_and_denial_audit_are_enforced()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(); using var anonymous = app.CreateClient();
        using (var denied = await anonymous.GetAsync("/api/v1/notifications/mine", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        using var patient = await app.ClientAsync("patient");
        using (var denied = await patient.GetAsync("/api/v1/admin/notification-settings", TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using (var denied = await patient.PostAsJsonAsync($"/api/v1/admin/notification-deliveries/{Guid.NewGuid()}/retry", new { reason = "Unauthorized", rowVersion = "AA==" }, TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        await app.WithDbAsync(async db => Assert.True(await db.NotificationAudits.AnyAsync(a => a.Action == "DeliveryRetryDenied")));
    }
    [Theory]
    [InlineData("O08", true)]
    [InlineData("O03", false)]
    public async Task Send_time_guard_suppresses_expired_or_cancelled_reminders_before_smtp(string caseId, bool expired)
    {
        Assert.NotEmpty(caseId);
        await using var app = await Phase13ApiFixture.CreateAsync(); var id = await ReservationAsync(app, trusted: true);
        app.Clock.UtcNow = app.Clock.UtcNow.AddHours(2); await DispatchAsync(app);
        if (expired) app.Clock.UtcNow = app.Clock.UtcNow.AddMinutes(16);
        else await app.WithDbAsync(async db =>
        {
            var r = await db.Reservations.Include(r => r.History).SingleAsync(r => r.Id == id);
            r.Cancel(ReservationCancellationInitiator.Patient, ReservationActionInitiator.Patient, r.CreatedByApplicationUserId, ReservationCancellationReasons.Other, null, app.Clock.UtcNow); await db.SaveChangesAsync();
        });
        await using var scope = app.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<WaslaDbContext>(); var sender = new Sender();
        var processor = new EmailOutboxProcessor(db, sender, app.Clock, Options.Create(new EmailOutboxOptions()), NullLogger<EmailOutboxProcessor>.Instance,
            scope.ServiceProvider.GetRequiredService<NotificationEmailGuard>());
        await processor.ProcessBatchAsync(TestContext.Current.CancellationToken);
        Assert.Empty(sender.Messages);
        Assert.All(await db.EmailOutboxMessages.Where(m => m.NotificationDeliveryId != null).ToArrayAsync(TestContext.Current.CancellationToken), m => Assert.Equal(EmailOutboxStatus.Suppressed, m.Status));
    }
}
