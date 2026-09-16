using Microsoft.EntityFrameworkCore;
using Wasla.Application.Features.PublicDiscovery;
using Wasla.Domain.Reservations;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed class ReservationPublicReaders(WaslaDbContext dbContext)
    : IPracticeReservationOccupancyReader, IPublicDoctorPopularityReader
{
    public async Task<IReadOnlyDictionary<(Guid PracticeId, DateOnly Date), PracticeOccupancySnapshot>> ReadAsync(
        IReadOnlyCollection<Guid> practiceIds,
        DateOnly fromDate,
        DateOnly throughDate,
        CancellationToken cancellationToken)
    {
        var ids = practiceIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<(Guid, DateOnly), PracticeOccupancySnapshot>();
        }

        var rows = await dbContext.Reservations.AsNoTracking()
            .Where(item => ids.Contains(item.DoctorPracticeId) &&
                           item.BusinessDate >= fromDate && item.BusinessDate <= throughDate &&
                           (item.Status == ReservationStatus.Active ||
                            item.Status == ReservationStatus.ConvertedToTicket))
            .Select(item => new
            {
                item.DoctorPracticeId,
                item.BusinessDate,
                item.SegmentId,
                item.ScheduledLocalDateTime
            })
            .ToArrayAsync(cancellationToken);

        return rows
            .GroupBy(item => (item.DoctorPracticeId, item.BusinessDate))
            .ToDictionary(
                group => group.Key,
                group => new PracticeOccupancySnapshot(
                    group.Select(item => TimeOnly.FromDateTime(item.ScheduledLocalDateTime)).ToHashSet(),
                    group.Count(),
                    group.GroupBy(item => item.SegmentId)
                        .ToDictionary(segmentGroup => segmentGroup.Key, segmentGroup => segmentGroup.Count())));
    }

    public async Task<IReadOnlyDictionary<Guid, long>> ReadSuccessfulReservationCountsAsync(
        IReadOnlyCollection<Guid> doctorIds,
        DateTime sinceUtc,
        CancellationToken cancellationToken)
    {
        var ids = doctorIds.Distinct().ToArray();
        var counts = await dbContext.Reservations.AsNoTracking()
            .Where(item => ids.Contains(item.DoctorId) &&
                           item.Status == ReservationStatus.ConvertedToTicket &&
                           item.ConvertedToTicketOnUtc >= sinceUtc)
            .GroupBy(item => item.DoctorId)
            .Select(group => new { DoctorId = group.Key, Count = group.LongCount() })
            .ToDictionaryAsync(item => item.DoctorId, item => item.Count, cancellationToken);
        return ids.ToDictionary(id => id, id => counts.GetValueOrDefault(id));
    }
}
