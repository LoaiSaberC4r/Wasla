using Microsoft.EntityFrameworkCore;
using Wasla.Domain.Practices;
using Wasla.Domain.Subscriptions;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal static class CommercialPracticeQuery
{
    internal static IQueryable<DoctorPractice> PubliclyCoveredPractices(this WaslaDbContext db, DateTime now) =>
        db.DoctorPractices.AsNoTracking().Where(p => db.PracticeSubscriptionAllocations.Any(a => a.DoctorPracticeId == p.Id && a.ReleasedAtUtc == null && a.AllocatedFromUtc <= now &&
            db.DoctorSubscriptions.Any(s => s.SubscriptionContractId == a.SubscriptionContractId && s.DoctorId == p.DoctorId &&
                (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Scheduled) && s.StartsAtUtc <= now && now < s.EndsAtUtc &&
                !db.SubscriptionAllocationSelections.Any(selection => selection.SubscriptionId == s.Id && selection.AppliedOnUtc == null &&
                    db.SubscriptionRetainedPractices.Any(r => r.SubscriptionId == s.Id && !db.PracticeSubscriptionAllocations.Any(current => current.Id == r.AllocationId && current.ReleasedAtUtc == null && current.SubscriptionContractId == s.SubscriptionContractId))) &&
                db.SubscriptionFeatureSnapshots.Any(f => f.SubscriptionId == s.Id && f.FeatureCodeSnapshot == "PUBLIC_DISCOVERY" && f.AccessLevel == FeatureAccessLevel.Full) &&
                db.SubscriptionFeatureSnapshots.Any(f => f.SubscriptionId == s.Id && f.FeatureCodeSnapshot == "ONLINE_BOOKING" && f.AccessLevel == FeatureAccessLevel.Full) &&
                (!db.SubscriptionAllocationSelections.Any(x => x.SubscriptionId == s.Id && x.AppliedOnUtc == null) || db.SubscriptionRetainedPractices.Any(x => x.SubscriptionId == s.Id && x.AllocationId == a.Id)))));
}
