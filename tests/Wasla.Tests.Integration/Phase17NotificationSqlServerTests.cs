using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Wasla.Domain.Notifications;
using Wasla.Domain.Reservations;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Email;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Notifications;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Options;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;
using static Wasla.Tests.Integration.Phase13ApiFixture;
using static Wasla.Tests.Integration.Phase17NotificationFixture;

namespace Wasla.Tests.Integration;

[Trait("Category", "SQLServerConcurrency")]
public sealed class Phase17NotificationSqlServerTests
{
    [Fact]
    public async Task O02_two_real_sql_workers_create_one_logical_delivery_per_channel()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(true); var id = await ReservationAsync(app, trusted: true);
        await DispatchAsync(app); app.Clock.UtcNow = app.Clock.UtcNow.AddHours(2);
        await Task.WhenAll(DispatchAsync(app), DispatchAsync(app));
        await app.WithDbAsync(async db =>
        {
            var e = await db.NotificationEvents.SingleAsync(e => e.SourceId == id && e.EventType == "Appointment.ReminderDue");
            Assert.Equal(2, await db.NotificationDeliveries.CountAsync(d => d.NotificationEventId == e.Id));
            Assert.Single(await db.EmailOutboxMessages.Where(m => m.NotificationDeliveryId != null).ToArrayAsync());
            Assert.Equal(NotificationIntentStatus.Dispatched, (await db.NotificationIntents.SingleAsync(i => i.NotificationEventId == e.Id)).Status);
        });
    }
    [Fact]
    public async Task O07_expired_claim_is_recovered_once_and_unique_delivery_is_enforced_by_sql()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(true); var id = await ReservationAsync(app, trusted: true);
        await DispatchAsync(app); app.Clock.UtcNow = app.Clock.UtcNow.AddHours(2);
        await app.WithDbAsync(async db =>
        {
            var ids = await db.NotificationEvents.Where(e => e.SourceId == id && e.EventType == "Appointment.ReminderDue").Select(e => e.Id).ToArrayAsync();
            await db.NotificationIntents.Where(i => ids.Contains(i.NotificationEventId)).ExecuteUpdateAsync(s => s.SetProperty(i => i.Status, NotificationIntentStatus.Processing)
                .SetProperty(i => i.ProcessingToken, Guid.NewGuid()).SetProperty(i => i.LeaseUntilUtc, app.Clock.UtcNow.AddSeconds(-1)));
        });
        await Task.WhenAll(DispatchAsync(app), DispatchAsync(app)); await DispatchAsync(app);
        await app.WithDbAsync(async db =>
        {
            var e = await db.NotificationEvents.SingleAsync(e => e.SourceId == id && e.EventType == "Appointment.ReminderDue");
            var i = await db.NotificationIntents.SingleAsync(i => i.NotificationEventId == e.Id);
            var d = await db.NotificationDeliveries.FirstAsync(d => d.NotificationEventId == e.Id);
            db.NotificationDeliveries.Add(new(e, i, d.Channel, d.RecipientLogicalKey, d.RecipientApplicationUserId, d.RecipientPatientId, app.Clock.UtcNow));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
        });
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task O03_source_commit_serializes_with_dispatch_and_invalidates_old_revision(bool reschedule)
    {
        await using var app = await Phase13ApiFixture.CreateAsync(true); var id = await ReservationAsync(app, trusted: true);
        await DispatchAsync(app); app.Clock.UtcNow = app.Clock.UtcNow.AddHours(2);
        await using var scope = app.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<WaslaDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var r = await db.Reservations.Include(r => r.History).SingleAsync(r => r.Id == id, TestContext.Current.CancellationToken);
        if (reschedule)
        {
            var starts = r.ScheduledStartUtc.AddHours(2); var local = r.ScheduledLocalDateTime.AddHours(2);
            Assert.True(r.Reschedule(starts, local, DateOnly.FromDateTime(local), 20, r.TimeZoneIdSnapshot, ReservationActionInitiator.Doctor, app.DoctorUserId, true, "Approved", app.Clock.UtcNow).IsSuccess);
        }
        else Assert.True(r.Cancel(ReservationCancellationInitiator.Patient, ReservationActionInitiator.Patient, r.CreatedByApplicationUserId,
            ReservationCancellationReasons.PatientChangedPlans, null, app.Clock.UtcNow).IsSuccess);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var dispatch = DispatchAsync(app);
        await transaction.CommitAsync(TestContext.Current.CancellationToken); await dispatch;
        await app.WithDbAsync(async verify =>
        {
            var oldEvents = await verify.NotificationEvents.Where(e => e.SourceId == id && e.EventType == "Appointment.ReminderDue" && e.OccurredAtUtc < app.Clock.UtcNow).Select(e => e.Id).ToArrayAsync();
            Assert.False(await verify.NotificationDeliveries.AnyAsync(d => oldEvents.Contains(d.NotificationEventId)));
        });
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A18_concurrent_settings_and_preference_updates_return_one_conflict(bool preferences)
    {
        await using var app = await Phase14MedicationApiTests.CreateAsync(true);
        using var actor = await app.ClientAsync(preferences ? "patient" : "admin");
        var route = preferences ? "/api/v1/notifications/me/preferences" : "/api/v1/admin/notification-settings";
        var original = await GetAsync(actor, route); var token = original.GetProperty("rowVersion").GetString();
        object body = preferences ? new { preferredLanguage = "en", preferences = new[] { new { category = "AppointmentReminder", channel = "Email", enabled = false } }, rowVersion = token } :
            new { appointmentReminderEnabled = true, reminderLeadMinutes = 90, reason = "Approved test configuration", rowVersion = token };
        var results = await Task.WhenAll(actor.PutAsJsonAsync(route, body, TestContext.Current.CancellationToken), actor.PutAsJsonAsync(route, body, TestContext.Current.CancellationToken));
        try { Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict); }
        finally { foreach (var result in results) result.Dispose(); }
    }
    [Fact]
    public async Task O05_O08_smtp_failure_retries_independently_and_expired_reminder_is_suppressed()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(true); var id = await ReservationAsync(app, trusted: true);
        app.Clock.UtcNow = app.Clock.UtcNow.AddHours(2); await DispatchAsync(app);
        await using var scope = app.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<WaslaDbContext>(); var sender = new Sender { Failure = new InvalidOperationException("PRIVATE_PROVIDER_RESPONSE") };
        var processor = new EmailOutboxProcessor(db, sender, app.Clock, Options.Create(new EmailOutboxOptions()), NullLogger<EmailOutboxProcessor>.Instance, scope.ServiceProvider.GetRequiredService<NotificationEmailGuard>());
        await processor.ProcessBatchAsync(TestContext.Current.CancellationToken);
        var message = await db.EmailOutboxMessages.AsNoTracking().SingleAsync(m => m.NotificationDeliveryId != null, TestContext.Current.CancellationToken);
        Assert.Equal(EmailOutboxStatus.Pending, message.Status); Assert.DoesNotContain("PRIVATE", message.LastError!, StringComparison.Ordinal);
        Assert.True(await db.Reservations.AnyAsync(r => r.Id == id && r.Status == ReservationStatus.Active, TestContext.Current.CancellationToken));
        app.Clock.UtcNow = app.Clock.UtcNow.AddMinutes(16); sender.Failure = null;
        await processor.ProcessBatchAsync(TestContext.Current.CancellationToken);
        Assert.Empty(sender.Messages); Assert.Equal(EmailOutboxStatus.Suppressed, (await db.EmailOutboxMessages.AsNoTracking().SingleAsync(m => m.Id == message.Id, TestContext.Current.CancellationToken)).Status);
    }
    [Fact]
    public async Task O10_admin_retry_is_atomic_audited_and_rejects_already_claimed_work()
    {
        await using var app = await Phase14MedicationApiTests.CreateAsync(true); await ReservationAsync(app, trusted: true);
        app.Clock.UtcNow = app.Clock.UtcNow.AddHours(2); await DispatchAsync(app);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WaslaDbContext>(); var sender = new Sender { Failure = new InvalidOperationException("test-only failure") };
            await new EmailOutboxProcessor(db, sender, app.Clock, Options.Create(new EmailOutboxOptions { MaxAttempts = 1 }), NullLogger<EmailOutboxProcessor>.Instance,
                scope.ServiceProvider.GetRequiredService<NotificationEmailGuard>()).ProcessBatchAsync(TestContext.Current.CancellationToken);
        }
        using var admin = await app.ClientAsync("admin");
        var list = await GetAsync(admin, "/api/v1/admin/notification-deliveries?channel=Email&status=Failed"); var item = list.GetProperty("items")[0];
        var id = item.GetProperty("id").GetGuid(); var token = item.GetProperty("rowVersion").GetString();
        var url = $"/api/v1/admin/notification-deliveries/{id}/retry";
        var results = await Task.WhenAll(admin.PostAsJsonAsync(url, new { reason = "Provider restored", rowVersion = token }, TestContext.Current.CancellationToken),
            admin.PostAsJsonAsync(url, new { reason = "Provider restored", rowVersion = token }, TestContext.Current.CancellationToken));
        try { Assert.Single(results, r => r.StatusCode == HttpStatusCode.NoContent); Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict); }
        finally { foreach (var result in results) result.Dispose(); }
        await app.WithDbAsync(async db => Assert.Single(await db.NotificationAudits.Where(a => a.DeliveryId == id && a.Action == "DeliveryRetryAccepted").ToArrayAsync()));
        using var arbitrary = await admin.PostAsJsonAsync(url, new { reason = "Invalid", rowVersion = token, recipientEmail = "arbitrary@example.test", body = "arbitrary" }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, arbitrary.StatusCode);
    }
}
