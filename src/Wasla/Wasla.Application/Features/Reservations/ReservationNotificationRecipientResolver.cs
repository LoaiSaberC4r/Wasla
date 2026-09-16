using BuildingBlock.Application.Time;
using Wasla.Application.Persistence;
using Wasla.Domain.Reservations;

namespace Wasla.Application.Features.Reservations;

public interface IReservationNotificationRecipientResolver
{
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> ResolveAsync(
        IReadOnlyCollection<Reservation> reservations,
        CancellationToken cancellationToken = default);
}

internal sealed class ReservationNotificationRecipientResolver(
    IWaslaDataStore dataStore,
    IDateTimeProvider clock)
    : IReservationNotificationRecipientResolver
{
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> ResolveAsync(
        IReadOnlyCollection<Reservation> reservations,
        CancellationToken cancellationToken = default)
    {
        if (reservations.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<string>>();
        }

        var patientIds = reservations.Select(item => item.PatientId).Distinct().ToArray();
        var familyActorIds = reservations
            .Where(item => item.BookingSource == ReservationBookingSource.FamilyMember)
            .Select(item => item.CreatedByApplicationUserId)
            .Distinct()
            .ToArray();
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var candidates = await dataStore.ListReservationNotificationRecipientsAsync(
            patientIds, familyActorIds, today, cancellationToken);
        var patientRecipients = candidates.Where(item => item.PatientId.HasValue)
            .ToLookup(item => item.PatientId!.Value, item => item.Email);
        var actorRecipients = candidates.Where(item => item.BookingActorApplicationUserId.HasValue)
            .ToLookup(item => item.BookingActorApplicationUserId!.Value, item => item.Email);

        return reservations.ToDictionary(
            reservation => reservation.Id,
            reservation => (IReadOnlyList<string>)patientRecipients[reservation.PatientId]
                .Concat(reservation.BookingSource == ReservationBookingSource.FamilyMember
                    ? actorRecipients[reservation.CreatedByApplicationUserId]
                    : [])
                .Where(IsUsableAddress)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    private static bool IsUsableAddress(string address)
        => !string.IsNullOrWhiteSpace(address) && address.Length <= 320 && address.Contains('@');
}
