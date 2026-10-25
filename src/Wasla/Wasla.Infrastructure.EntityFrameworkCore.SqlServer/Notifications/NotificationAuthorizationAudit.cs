using BuildingBlock.Application.Time;
using Wasla.Application.Features.Notifications;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Notifications;

internal sealed class NotificationAuthorizationAudit(WaslaDbContext db, IDateTimeProvider clock) : INotificationAuthorizationAudit
{
    public async Task RetryDeniedAsync(Guid actor, CancellationToken ct)
    {
        db.NotificationAudits.Add(new(Guid.NewGuid(), actor, null, "DeliveryRetryDenied", "AccessDenied", null, clock.UtcNow));
        await db.SaveChangesAsync(ct);
    }
}
