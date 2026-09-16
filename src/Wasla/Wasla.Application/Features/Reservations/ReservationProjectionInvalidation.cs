namespace Wasla.Application.Features.Reservations;

public sealed record QueueReservationProjectionInvalidation(
    string IdempotencyKey,
    Guid PracticeId,
    Guid DoctorId,
    bool RefreshAvailability,
    bool RefreshPopularity);

/// <summary>
/// Stores projection work in the current reservation transaction. A separate
/// processor can only observe and execute it after that transaction commits.
/// </summary>
public interface IReservationProjectionInvalidationOutbox
{
    Task QueueAsync(
        QueueReservationProjectionInvalidation invalidation,
        CancellationToken cancellationToken = default);
}
