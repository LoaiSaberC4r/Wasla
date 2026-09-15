using BuildingBlock.Application.Time;
using Microsoft.EntityFrameworkCore;
using Wasla.Application.Features.PublicDiscovery;
using Wasla.Domain.Practices;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed class PublicDiscoveryRankingProjectionRefresher(
    WaslaDbContext dbContext,
    IDateTimeProvider clock,
    IPracticeReservationOccupancyReader occupancyReader,
    IPublicDoctorPopularityReader popularityReader)
    : IPublicDiscoveryRankingProjectionRefresher
{
    // A daily maintenance pass replenishes this well before the public 30-day horizon reaches it.
    internal const int ProjectionCoverageDays = 60;
    internal const int RefreshBatchSize = 100;

    public async Task RefreshPracticesAsync(
        IReadOnlyCollection<Guid> practiceIds,
        CancellationToken cancellationToken)
    {
        var ids = practiceIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return;
        }

        var practices = await (
            from practice in dbContext.DoctorPractices.AsNoTracking()
            join configuration in dbContext.DoctorPracticeConfigurations.AsNoTracking()
                on practice.Id equals configuration.DoctorPracticeId
            where ids.Contains(practice.Id)
            select new ProjectionPractice(
                practice.Id,
                practice.DoctorId,
                configuration.TimeZoneId,
                configuration.MaximumDailyPatients))
            .ToListAsync(cancellationToken);

        var periods = await dbContext.DoctorPracticeSchedulePeriods.AsNoTracking()
            .Where(item => ids.Contains(item.DoctorPracticeId))
            .ToListAsync(cancellationToken);
        var exceptions = await dbContext.DoctorPracticeScheduleExceptions.AsNoTracking()
            .Where(item => ids.Contains(item.DoctorPracticeId))
            .ToListAsync(cancellationToken);

        var nowUtc = EnsureUtc(clock.UtcNow);
        var windows = practices.ToDictionary(
            item => item.Id,
            item => LocalWindow(item.TimeZoneId, nowUtc));
        var occupancy = practices.Count == 0
            ? new Dictionary<(Guid, DateOnly), PracticeOccupancySnapshot>()
            : await occupancyReader.ReadAsync(
                practices.Select(item => item.Id).ToArray(),
                windows.Values.Min(item => item.Today),
                windows.Values.Max(item => item.ThroughDate),
                cancellationToken);

        var periodLookup = periods.ToLookup(item => item.DoctorPracticeId);
        var exceptionLookup = exceptions.ToLookup(item => item.DoctorPracticeId);
        var projected = new List<PublicPracticeAvailabilitySlot>();
        foreach (var practice in practices)
        {
            var window = windows[practice.Id];
            for (var offset = 0; offset < ProjectionCoverageDays; offset++)
            {
                var date = window.Today.AddDays(offset);
                var generated = DoctorPracticeAvailabilityCalculator.CalculateAvailableSlotStarts(
                    date,
                    periodLookup[practice.Id].ToArray(),
                    exceptionLookup[practice.Id].ToArray());
                var dayOccupancy = occupancy.GetValueOrDefault(
                    (practice.Id, date), PracticeOccupancySnapshot.Empty);
                var effectiveCapacity = practice.MaximumDailyPatients.HasValue
                    ? Math.Min(generated.Count, practice.MaximumDailyPatients.Value)
                    : generated.Count;
                if (effectiveCapacity - dayOccupancy.TotalReservations <= 0)
                {
                    continue;
                }

                foreach (var time in generated.Where(item => !dayOccupancy.OccupiedSlots.Contains(item)))
                {
                    var localSlot = date.ToDateTime(time, DateTimeKind.Unspecified);
                    var localVisibility = date
                        .AddDays(-(PublicDiscoveryPolicy.PublicBookingHorizonDays - 1))
                        .ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
                    projected.Add(new PublicPracticeAvailabilitySlot(
                        practice.DoctorId,
                        practice.Id,
                        date,
                        time,
                        ToUtc(localSlot, window.TimeZone),
                        ToUtc(localVisibility, window.TimeZone),
                        nowUtc));
                }
            }
        }

        await dbContext.PublicPracticeAvailabilitySlots
            .Where(item => ids.Contains(item.DoctorPracticeId))
            .ExecuteDeleteAsync(cancellationToken);
        dbContext.PublicPracticeAvailabilitySlots.AddRange(projected);
        await dbContext.SaveChangesAsync(cancellationToken);

        await RefreshDoctorsAsync(
            practices.Select(item => item.DoctorId).Distinct().ToArray(),
            cancellationToken);
    }

    public async Task RefreshDoctorsAsync(
        IReadOnlyCollection<Guid> doctorIds,
        CancellationToken cancellationToken)
    {
        var ids = doctorIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return;
        }

        var nowUtc = EnsureUtc(clock.UtcNow);
        var popularity = await popularityReader.ReadSuccessfulReservationCountsAsync(
            ids,
            nowUtc.AddDays(-90),
            cancellationToken);
        var existing = await dbContext.PublicDoctorSearchRanks
            .Where(item => ids.Contains(item.DoctorId))
            .ToDictionaryAsync(item => item.DoctorId, cancellationToken);
        foreach (var doctorId in ids)
        {
            var score = popularity.GetValueOrDefault(doctorId);
            if (existing.TryGetValue(doctorId, out var rank))
            {
                rank.Refresh(score, nowUtc);
            }
            else
            {
                dbContext.PublicDoctorSearchRanks.Add(
                    new PublicDoctorSearchRank(doctorId, score, nowUtc));
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RefreshAllAsync(CancellationToken cancellationToken)
    {
        var practiceIds = await dbContext.DoctorPractices.AsNoTracking()
            .OrderBy(item => item.Id)
            .Select(item => item.Id)
            .ToArrayAsync(cancellationToken);
        foreach (var batch in practiceIds.Chunk(RefreshBatchSize))
        {
            await RefreshPracticesAsync(batch, cancellationToken);
        }

        var doctorIds = await dbContext.Doctors.AsNoTracking()
            .Select(item => item.Id)
            .ToArrayAsync(cancellationToken);
        await RefreshDoctorsAsync(doctorIds, cancellationToken);
    }

    private static ProjectionWindow LocalWindow(string timeZoneId, DateTime nowUtc)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var localNow = TimeZoneInfo.ConvertTime(new DateTimeOffset(nowUtc), timeZone);
        var today = DateOnly.FromDateTime(localNow.DateTime);
        return new ProjectionWindow(
            today,
            today.AddDays(ProjectionCoverageDays - 1),
            timeZone);
    }

    private static DateTime ToUtc(DateTime localDateTime, TimeZoneInfo timeZone)
        => new DateTimeOffset(localDateTime, timeZone.GetUtcOffset(localDateTime)).UtcDateTime;

    private static DateTime EnsureUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();

    private sealed record ProjectionPractice(
        Guid Id,
        Guid DoctorId,
        string TimeZoneId,
        int? MaximumDailyPatients);

    private sealed record ProjectionWindow(
        DateOnly Today,
        DateOnly ThroughDate,
        TimeZoneInfo TimeZone);
}
