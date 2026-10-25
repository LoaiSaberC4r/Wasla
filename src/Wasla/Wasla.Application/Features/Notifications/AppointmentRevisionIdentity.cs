using Wasla.Domain.Reservations;

namespace Wasla.Application.Features.Notifications;

public static class AppointmentRevisionIdentity
{
    public static string? Current(Reservation reservation)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        if (reservation.AppointmentRevisionHistoryId is { } current) return current.ToString("N");
        // Untouched pre-migration rows have no pointer. Read existing immutable history;
        // restoring one must not invent a new appointment revision or a historical reminder.
        return reservation.History.Where(h => h.EventType == ReservationHistoryEventType.Created ||
                h.EventType == ReservationHistoryEventType.Rescheduled && h.NewScheduledStartUtc == reservation.ScheduledStartUtc)
            .OrderBy(h => h.EventType).ThenBy(h => h.OccurredOnUtc).ThenBy(h => h.Id).LastOrDefault()?.Id.ToString("N");
    }
}
