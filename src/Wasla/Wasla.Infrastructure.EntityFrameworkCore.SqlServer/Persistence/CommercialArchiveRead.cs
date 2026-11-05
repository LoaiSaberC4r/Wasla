using Microsoft.EntityFrameworkCore;
using Wasla.Application.Features.Subscriptions;
using Wasla.Domain.Subscriptions;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal static class CommercialArchiveRead
{
    // A future appointment can create its Ticket after the original period ended.
    // Compose both proofs before pagination and counts, using the retained origin key.
    internal static IQueryable<T> CommerciallyReadable<T>(this IQueryable<T> source, WaslaDbContext db, SubscriptionReadScope scope, bool ticket) where T : class
    {
        var byTime = source.Where(scope.Predicate<T>(r => EF.Property<DateTime>(r, "CreatedOnUtc"), r => EF.Property<Guid>(r, "DoctorPracticeId")));
        var periods = scope.Windows.Where(w => w.SubscriptionId.HasValue).Select(w => w.SubscriptionId!.Value).Distinct().ToArray();
        if (ticket)
            return source.Where(row => byTime.Any(t => EF.Property<Guid>(t, "Id") == EF.Property<Guid>(row, "Id")) ||
                db.ReservationCommercialProvenances.Any(p => p.DoctorPracticeId == EF.Property<Guid>(row, "DoctorPracticeId") && periods.Contains(p.SubscriptionId) &&
                    (p.OriginKind == CommercialCommitmentKind.Reservation && p.Id == EF.Property<Guid?>(row, "ReservationId") ||
                     p.OriginKind == CommercialCommitmentKind.WalkInTicket && p.Id == EF.Property<Guid>(row, "Id"))));
        return source.Where(row => byTime.Any(r => EF.Property<Guid>(r, "Id") == EF.Property<Guid>(row, "Id")) ||
            db.ReservationCommercialProvenances.Any(p => p.OriginKind == CommercialCommitmentKind.Reservation && p.Id == EF.Property<Guid>(row, "Id") &&
                p.DoctorPracticeId == EF.Property<Guid>(row, "DoctorPracticeId") && periods.Contains(p.SubscriptionId)));
    }
}
