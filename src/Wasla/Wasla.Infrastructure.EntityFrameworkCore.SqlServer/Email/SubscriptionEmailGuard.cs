using Microsoft.EntityFrameworkCore;
using Wasla.Domain.Subscriptions;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Notifications;
using Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Email;

public sealed class SubscriptionEmailGuard(WaslaDbContext db)
{
    public static bool TryPeriod(string key, out Guid periodId)
    {
        const string prefix = "subscription-paid-reminder:";
        periodId = default;
        return key.StartsWith(prefix, StringComparison.Ordinal) && key.Length >= prefix.Length + 36 && Guid.TryParse(key.AsSpan(prefix.Length, 36), out periodId);
    }
    public async Task<IAsyncDisposable?> AcquireAsync(Guid periodId, CancellationToken ct)
    {
        var contract = await db.DoctorSubscriptions.AsNoTracking().Where(p => p.Id == periodId).Select(p => (Guid?)p.SubscriptionContractId).SingleOrDefaultAsync(ct);
        return contract is null ? null : await NotificationSourceLock.SessionAsync(db, $"Wasla:Subscription:Reminder:{contract.Value:N}", ct);
    }
    public async Task<string?> DenialAsync(Guid periodId, CancellationToken ct)
    {
        var period = await db.DoctorSubscriptions.AsNoTracking().SingleOrDefaultAsync(p => p.Id == periodId, ct);
        if (period?.SubscriptionKind != SubscriptionKind.Paid) return "Subscription.PeriodUnavailable";
        return await db.DoctorSubscriptions.AnyAsync(p => p.Id != period.Id && p.SubscriptionContractId == period.SubscriptionContractId &&
            p.SubscriptionKind == SubscriptionKind.Paid && p.StartsAtUtc >= period.EndsAtUtc &&
            (p.Status == SubscriptionStatus.Active || p.Status == SubscriptionStatus.Scheduled || p.Status == SubscriptionStatus.Expired), ct)
            ? "Subscription.PaidSuccessorConfirmed" : null;
    }
}
