using BuildingBlock.Application.Time;
using Microsoft.EntityFrameworkCore;
using Wasla.Application.Features.Reservations;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal sealed class ReservationProjectionInvalidationOutbox(
    WaslaDbContext dbContext,
    IDateTimeProvider clock)
    : IReservationProjectionInvalidationOutbox
{
    public async Task QueueAsync(
        QueueReservationProjectionInvalidation invalidation,
        CancellationToken cancellationToken = default)
    {
        var exists = dbContext.ReservationProjectionInvalidations.Local.Any(
                         item => item.IdempotencyKey == invalidation.IdempotencyKey) ||
                     await dbContext.ReservationProjectionInvalidations.AsNoTracking().AnyAsync(
                         item => item.IdempotencyKey == invalidation.IdempotencyKey,
                         cancellationToken);
        if (exists)
        {
            return;
        }

        dbContext.ReservationProjectionInvalidations.Add(new ReservationProjectionInvalidation(
            Guid.NewGuid(),
            invalidation.IdempotencyKey,
            invalidation.PracticeId,
            invalidation.DoctorId,
            invalidation.RefreshAvailability,
            invalidation.RefreshPopularity,
            EnsureUtc(clock.UtcNow)));
    }

    private static DateTime EnsureUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}
