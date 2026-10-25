using BuildingBlock.Application.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wasla.Domain.Notifications;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Email;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Notifications;

public sealed class NotificationEmailGuard(WaslaDbContext db, NotificationEligibility eligibility, ILogger<NotificationEmailGuard> logger)
{
    public async Task<IAsyncDisposable?> AcquireAsync(Guid deliveryId, CancellationToken ct)
    {
        var source = await (from d in db.NotificationDeliveries.AsNoTracking()
                            join e in db.NotificationEvents.AsNoTracking() on d.NotificationEventId equals e.Id
                            where d.Id == deliveryId
                            select new { e.SourceType, e.SourceId }).SingleOrDefaultAsync(ct);
        return source is null ? null : await NotificationSourceLock.SessionAsync(db, NotificationSourceLock.Resource(source.SourceType, source.SourceId), ct);
    }
    public async Task<string?> DenialAsync(Guid deliveryId, string email, CancellationToken ct)
    {
        var delivery = await db.NotificationDeliveries.AsNoTracking().SingleOrDefaultAsync(d => d.Id == deliveryId, ct);
        if (delivery is null || delivery.Channel != NotificationChannel.Email || delivery.Status == NotificationDeliveryStatus.Suppressed) return "Delivery.Suppressed";
        var source = await db.NotificationEvents.AsNoTracking().SingleAsync(e => e.Id == delivery.NotificationEventId, ct);
        var intent = await db.NotificationIntents.AsNoTracking().SingleAsync(i => i.Id == delivery.NotificationIntentId, ct);
        var denial = await eligibility.SourceDenialAsync(source, intent, ct);
        if (denial is not null) return denial;
        var recipient = (await eligibility.ResolveAsync(source, ct)).SingleOrDefault(r => r.LogicalKey == delivery.RecipientLogicalKey);
        if (recipient is null || !string.Equals(recipient.TrustedEmail, email, StringComparison.OrdinalIgnoreCase)) return "Recipient.Changed";
        return (await eligibility.ChannelAsync(source, recipient, NotificationChannel.Email, ct)).Denial;
    }
    public async Task SuppressAsync(Guid deliveryId, CancellationToken ct)
    {
        await db.NotificationDeliveries.Where(d => d.Id == deliveryId).ExecuteUpdateAsync(s => s
            .SetProperty(d => d.Status, NotificationDeliveryStatus.Suppressed).SetProperty(d => d.SafeFailureCode, "Delivery.SendTimeSuppressed"), ct);
        await RecordTransportAsync(deliveryId, false, ct);
    }

    public async Task RecordTransportAsync(Guid deliveryId, bool sent, CancellationToken ct)
    {
        var type = await (from d in db.NotificationDeliveries
                          join e in db.NotificationEvents on d.NotificationEventId equals e.Id
                          where d.Id == deliveryId
                          select e.EventType).SingleOrDefaultAsync(ct);
        NotificationTelemetry.Record(logger, sent ? "notification.delivery.email.sent" : "notification.delivery.email.suppressed", type ?? "Unavailable", NotificationChannel.Email, sent ? "Sent" : "Suppressed");
    }

    public async Task RecordRetryOutcomeAsync(Guid deliveryId, bool sent, DateTime now, CancellationToken ct)
    {
        var accepted = await db.NotificationAudits.AsNoTracking().Where(a => a.DeliveryId == deliveryId && a.Action == "DeliveryRetryAccepted")
            .OrderByDescending(a => a.OccurredAtUtc).FirstOrDefaultAsync(ct);
        if (accepted is null || await db.NotificationAudits.AnyAsync(a => a.DeliveryId == deliveryId &&
            (a.Action == "DeliveryRetryCompleted" || a.Action == "DeliveryRetryFailed") && a.OccurredAtUtc >= accepted.OccurredAtUtc, ct)) return;
        db.NotificationAudits.Add(new(Guid.NewGuid(), accepted.ActorApplicationUserId, deliveryId, sent ? "DeliveryRetryCompleted" : "DeliveryRetryFailed", sent ? "Sent" : "Email.NotDelivered", null, now));
        await db.SaveChangesAsync(ct);
    }
}
