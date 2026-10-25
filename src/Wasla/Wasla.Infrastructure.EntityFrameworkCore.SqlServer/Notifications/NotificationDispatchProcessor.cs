using System.Diagnostics.Metrics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BuildingBlock.Application.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wasla.Application.Email;
using Wasla.Application.Features.Notifications;
using Wasla.Domain.Notifications;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Notifications;

public sealed partial class NotificationDispatchProcessor(WaslaDbContext db, NotificationEligibility eligibility, IEmailOutbox email,
    IDateTimeProvider clock, IOptions<NotificationOptions> options, ILogger<NotificationDispatchProcessor> logger)
{
    private static readonly Meter Meter = new("Wasla.Notifications", "1.0");
    private static readonly Counter<long> Outcomes = Meter.CreateCounter<long>("notification_dispatch_total");

    public async Task<int> ProcessBatchAsync(CancellationToken ct = default)
    {
        if (!options.Value.Enabled || !options.Value.DispatchEnabled) return 0;
        var now = clock.UtcNow;
        await db.NotificationIntents.Where(i => i.Status == NotificationIntentStatus.Processing && i.LeaseUntilUtc <= now && i.AttemptCount >= options.Value.MaxAttempts)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.Status, NotificationIntentStatus.Failed).SetProperty(i => i.ProcessingToken, (Guid?)null)
                .SetProperty(i => i.LeaseUntilUtc, (DateTime?)null).SetProperty(i => i.SkipReasonCode, "Dispatch.AttemptsExhausted"), ct);
        var ids = await db.NotificationIntents.AsNoTracking().Where(i =>
            i.Status == NotificationIntentStatus.Scheduled && i.DueAtUtc <= now && (i.NextAttemptOnUtc == null || i.NextAttemptOnUtc <= now) ||
            i.Status == NotificationIntentStatus.Processing && i.LeaseUntilUtc <= now)
            .OrderBy(i => i.DueAtUtc).ThenBy(i => i.Id).Select(i => i.Id).Take(options.Value.BatchSize).ToArrayAsync(ct);
        var token = Guid.NewGuid(); var lease = now.AddSeconds(options.Value.ClaimLeaseSeconds);
        // Conditional UPDATE is the durable claim; candidate reads never confer ownership.
        await db.NotificationIntents.Where(i => ids.Contains(i.Id) &&
            (i.Status == NotificationIntentStatus.Scheduled && (i.NextAttemptOnUtc == null || i.NextAttemptOnUtc <= now) ||
                i.Status == NotificationIntentStatus.Processing && i.LeaseUntilUtc <= now))
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.Status, NotificationIntentStatus.Processing)
                .SetProperty(i => i.ProcessingToken, token).SetProperty(i => i.LeaseUntilUtc, lease)
                .SetProperty(i => i.AttemptCount, i => i.AttemptCount + 1), ct);
        var claimed = await db.NotificationIntents.AsNoTracking().Where(i => i.ProcessingToken == token && i.Status == NotificationIntentStatus.Processing).Select(i => i.Id).ToArrayAsync(ct);
        foreach (var id in claimed)
        {
            try { await DispatchAsync(id, token, ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception)
            {
                db.ChangeTracker.Clear();
                var next = clock.UtcNow.AddMinutes(1);
                await db.NotificationIntents.Where(i => i.Id == id && i.ProcessingToken == token && i.Status == NotificationIntentStatus.Processing)
                    .ExecuteUpdateAsync(s => s.SetProperty(i => i.Status, i => i.AttemptCount >= options.Value.MaxAttempts ? NotificationIntentStatus.Failed : NotificationIntentStatus.Scheduled)
                        .SetProperty(i => i.NextAttemptOnUtc, next).SetProperty(i => i.ProcessingToken, (Guid?)null)
                        .SetProperty(i => i.LeaseUntilUtc, (DateTime?)null).SetProperty(i => i.SkipReasonCode, "Dispatch.Failed"), ct);
                Outcome(logger, "Failed", "Dispatch.Failed"); Outcomes.Add(1, new KeyValuePair<string, object?>("status", "Failed"));
                NotificationTelemetry.Record(logger, "notification.dispatch.failed", "Unavailable", null, "Failed", "Dispatch.Failed");
            }
        }
        return claimed.Length;
    }

    private async Task DispatchAsync(Guid id, Guid token, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        var source = await (from i in db.NotificationIntents.AsNoTracking()
                            join e in db.NotificationEvents.AsNoTracking() on i.NotificationEventId equals e.Id
                            where i.Id == id
                            select e).SingleAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await NotificationSourceLock.AcquireAsync(db, NotificationSourceLock.Resource(source.SourceType, source.SourceId), "Transaction", ct);
        // The source lock can outlast a claim lease; verify ownership again after admission.
        var intent = await db.NotificationIntents.SingleAsync(i => i.Id == id, ct);
        if (intent.ProcessingToken != token || intent.Status != NotificationIntentStatus.Processing || intent.LeaseUntilUtc <= clock.UtcNow) return;
        NotificationTelemetry.Record(logger, "notification.dispatch.started", source.EventType, null, "Processing");
        NotificationTelemetry.DispatchLag(clock.UtcNow - intent.DueAtUtc);
        var denial = await eligibility.SourceDenialAsync(source, intent, ct);
        if (denial is not null)
        {
            intent.Finish(NotificationIntentStatus.Skipped, clock.UtcNow, denial);
        }
        else
        {
            var recipients = await eligibility.ResolveAsync(source, ct);
            foreach (var recipient in recipients)
                foreach (var channel in Enum.GetValues<NotificationChannel>())
                {
                    if (channel == NotificationChannel.Email && (!NotificationPolicy.EmailAllowed(source.EventType) || source.LegacyEmailOwned)) continue;
                    var delivery = new NotificationDelivery(source, intent, channel, recipient.LogicalKey, recipient.UserId,
                        recipient.PatientId == Guid.Empty ? null : recipient.PatientId, clock.UtcNow);
                    if (await db.NotificationDeliveries.AnyAsync(d => d.LogicalDeliveryKey == delivery.LogicalDeliveryKey, ct)) continue;
                    db.NotificationDeliveries.Add(delivery);
                    var policy = await eligibility.ChannelAsync(source, recipient, channel, ct);
                    if (policy.Denial is { } reason)
                    {
                        delivery.Suppress(reason);
                        NotificationTelemetry.Record(logger, reason == "Preference.Disabled" ? "notification.preference.suppressed" : "notification.recipient.denied", source.EventType, channel, "Suppressed", reason);
                        continue;
                    }
                    if (channel == NotificationChannel.InApp) db.UserNotifications.Add(new UserNotification(source, delivery, clock.UtcNow));
                    else
                    {
                        var reservation = source.SourceType == "Reservation" ? await db.Reservations.AsNoTracking().SingleOrDefaultAsync(r => r.Id == source.SourceId, ct) : null;
                        var rendered = NotificationTemplates.Render(delivery.TemplateCode, delivery.TemplateVersion, policy.Language, reservation?.ReservationReference, reservation?.ScheduledLocalDateTime);
                        var key = delivery.LogicalDeliveryKey;
                        if (source.EventType is "Reservation.Created" or "Reservation.Rescheduled" or "Reservation.Cancelled")
                        {
                            var name = source.EventType[12..].ToLowerInvariant();
                            if (source.EventType == "Reservation.Cancelled" && reservation?.CancellationReasonCode == "DoctorSuspended") name = "doctor-suspended";
                            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(recipient.TrustedEmail!)))[..12];
                            key = $"reservation-{name}:{source.SourceId:N}:{source.SourceEventKey}:{hash}";
                        }
                        await email.QueueAsync(new QueueEmailMessage(key, recipient.TrustedEmail!, $"Wasla | {rendered.Title}", rendered.HtmlBody, rendered.TextBody, delivery.Id), ct);
                    }
                    delivery.Queue(clock.UtcNow);
                }
            intent.Finish(NotificationIntentStatus.Dispatched, clock.UtcNow, recipients.Count == 0 ? "Recipient.NoneEligible" : null);
        }
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        var status = intent.Status.ToString();
        NotificationTelemetry.Record(logger, intent.Status == NotificationIntentStatus.Skipped ? "notification.intent.skipped" : "notification.dispatch.completed", source.EventType, null, status, intent.SkipReasonCode ?? "None");
        foreach (var delivery in db.NotificationDeliveries.Local.Where(d => d.NotificationEventId == source.Id && d.Status == NotificationDeliveryStatus.Queued))
            NotificationTelemetry.Record(logger, delivery.Channel == NotificationChannel.Email ? "notification.delivery.email.queued" : "notification.delivery.inapp.created", source.EventType, delivery.Channel, "Queued");
        Outcome(logger, status, intent.SkipReasonCode ?? "Dispatch.Recorded");
        Outcomes.Add(1, new KeyValuePair<string, object?>("status", status));
    }
    [LoggerMessage(EventId = 4700, Level = LogLevel.Information, Message = "Notification dispatch {Status}; {ReasonCode}.")]
    private static partial void Outcome(ILogger logger, string status, string reasonCode);
}
