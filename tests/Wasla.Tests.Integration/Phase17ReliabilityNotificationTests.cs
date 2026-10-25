using System.Net;
using System.Net.Http.Json;
using BuildingBlock.Application.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Wasla.Domain.Notifications;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Email;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Notifications;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Options;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;
using static Wasla.Tests.Integration.Phase13ApiFixture;
using static Wasla.Tests.Integration.Phase17NotificationFixture;

namespace Wasla.Tests.Integration;

public sealed class Phase17ReliabilityNotificationTests
{
    [Fact]
    public async Task O06_O08_permanent_authentication_failure_is_safe_and_stale_admin_retry_is_denied()
    {
        await using var app = await Phase14MedicationApiTests.CreateAsync(false); await ReservationAsync(app, trusted: true);
        app.Clock.UtcNow = app.Clock.UtcNow.AddHours(2); await DispatchAsync(app);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WaslaDbContext>();
            var sender = new Sender
            {
                Failure = new BuildingBlock.Infrastructure.Exceptions.EmailServiceException(
                BuildingBlock.Domain.Results.Error.Infra(BuildingBlock.Infrastructure.Exceptions.ExternalServiceErrorCodes.Email.AuthenticationFailed, "PRIVATE_SMTP_SECRET_TEST_ONLY"))
            };
            var processor = new EmailOutboxProcessor(db, sender, app.Clock, Options.Create(new EmailOutboxOptions()), NullLogger<EmailOutboxProcessor>.Instance,
                scope.ServiceProvider.GetRequiredService<NotificationEmailGuard>());
            await processor.ProcessBatchAsync(TestContext.Current.CancellationToken);
            var failed = await db.EmailOutboxMessages.SingleAsync(m => m.NotificationDeliveryId != null, TestContext.Current.CancellationToken);
            Assert.Equal(EmailOutboxStatus.Failed, failed.Status); Assert.Equal(1, failed.AttemptCount);
            Assert.Equal(BuildingBlock.Infrastructure.Exceptions.ExternalServiceErrorCodes.Email.AuthenticationFailed, failed.LastError);
            Assert.DoesNotContain("PRIVATE", failed.LastError!, StringComparison.Ordinal);
        }
        using var admin = await app.ClientAsync("admin");
        var rows = await GetAsync(admin, "/api/v1/admin/notification-deliveries?channel=Email&status=Failed"); var delivery = rows.GetProperty("items")[0];
        Assert.DoesNotContain("patient@example.test", rows.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE", rows.GetRawText(), StringComparison.Ordinal);
        app.Clock.UtcNow = app.Clock.UtcNow.AddMinutes(16);
        var id = delivery.GetProperty("id").GetGuid();
        using var denied = await admin.PostAsJsonAsync($"/api/v1/admin/notification-deliveries/{id}/retry", new { reason = "Provider recovered", rowVersion = delivery.GetProperty("rowVersion").GetString() }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);
        await app.WithDbAsync(async db =>
        {
            Assert.True(await db.NotificationAudits.AnyAsync(a => a.DeliveryId == id && a.Action == "DeliveryRetryDenied"));
            Assert.Equal(EmailOutboxStatus.Failed, (await db.EmailOutboxMessages.SingleAsync(m => m.NotificationDeliveryId == id)).Status);
        });
    }
    [Fact]
    public async Task O04_provider_acceptance_before_an_unobserved_ack_can_duplicate_physical_delivery_but_not_logical_identity()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(); await ReservationAsync(app, trusted: true);
        app.Clock.UtcNow = app.Clock.UtcNow.AddHours(2); await DispatchAsync(app);
        await using var scope = app.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<WaslaDbContext>(); var sender = new UncertainSender();
        var processor = new EmailOutboxProcessor(db, sender, app.Clock, Options.Create(new EmailOutboxOptions()), NullLogger<EmailOutboxProcessor>.Instance, scope.ServiceProvider.GetRequiredService<NotificationEmailGuard>());
        await processor.ProcessBatchAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, sender.Acceptances);
        app.Clock.UtcNow = app.Clock.UtcNow.AddMinutes(2);
        await processor.ProcessBatchAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, sender.Acceptances);
        Assert.Single(await db.NotificationDeliveries.Where(d => d.Channel == NotificationChannel.Email).ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal(EmailOutboxStatus.Sent, (await db.EmailOutboxMessages.SingleAsync(m => m.NotificationDeliveryId != null, TestContext.Current.CancellationToken)).Status);
    }
    [Fact]
    public async Task O12_large_inbox_has_bounded_stable_pages_counts_and_own_bulk_marking()
    {
        await using var app = await Phase13ApiFixture.CreateAsync(); var sourceId = await ReservationAsync(app); await DispatchAsync(app);
        await app.WithDbAsync(async db =>
        {
            var user = (await db.PatientAccountLinks.SingleAsync(l => l.PatientId == app.PatientId)).ApplicationUserId;
            // Synthetic persisted inbox fixtures isolate pagination from unrelated booking capacity rules.
            for (var index = 0; index < 205; index++)
            {
                var key = Guid.NewGuid().ToString("N");
                var source = new NotificationEvent("Reservation.Created", "Reservation", sourceId, key, key, app.PatientId, app.DoctorId, app.PracticeId, null, app.Clock.UtcNow);
                var intent = new NotificationIntent(source, app.Clock.UtcNow); intent.Finish(NotificationIntentStatus.Dispatched, app.Clock.UtcNow);
                var delivery = new NotificationDelivery(source, intent, NotificationChannel.InApp, $"user:{user:N}", user, app.PatientId, app.Clock.UtcNow); delivery.Queue(app.Clock.UtcNow);
                db.NotificationEvents.Add(source); db.NotificationIntents.Add(intent); db.NotificationDeliveries.Add(delivery); db.UserNotifications.Add(new(source, delivery, app.Clock.UtcNow));
            }
            await db.SaveChangesAsync();
        });
        using var patient = await app.ClientAsync("patient"); using var other = await app.ClientAsync("other-patient");
        var first = await GetAsync(patient, "/api/v1/notifications/mine?pageNumber=1&pageSize=100");
        var second = await GetAsync(patient, "/api/v1/notifications/mine?pageNumber=2&pageSize=100");
        Assert.Equal(206, first.GetProperty("totalCount").GetInt32());
        Assert.Equal(100, first.GetProperty("items").GetArrayLength()); Assert.Equal(100, second.GetProperty("items").GetArrayLength());
        var ids = first.GetProperty("items").EnumerateArray().Concat(second.GetProperty("items").EnumerateArray()).Select(n => n.GetProperty("id").GetGuid()).ToArray();
        Assert.Equal(200, ids.Distinct().Count());
        var replay = await GetAsync(patient, "/api/v1/notifications/mine?pageNumber=1&pageSize=100"); Assert.Equal(first.GetRawText(), replay.GetRawText());
        Assert.Equal(206, (await GetAsync(patient, "/api/v1/notifications/mine/unread-count")).GetProperty("unreadCount").GetInt32());
        using (var marked = await patient.PostAsJsonAsync("/api/v1/notifications/mine/read-all", new { }, TestContext.Current.CancellationToken)) Assert.Equal(HttpStatusCode.NoContent, marked.StatusCode);
        Assert.Equal(0, (await GetAsync(patient, "/api/v1/notifications/mine/unread-count")).GetProperty("unreadCount").GetInt32());
        Assert.Equal(0, (await GetAsync(other, "/api/v1/notifications/mine/unread-count")).GetProperty("unreadCount").GetInt32());
    }
    private sealed class UncertainSender : IEmailSender
    {
        public int Acceptances { get; private set; }
        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            Acceptances++;
            if (Acceptances == 1) throw new IOException("Test simulates provider acceptance with a lost response.");
            return Task.CompletedTask;
        }
    }
}
