using System.Text.Json;
using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Results;
using Wasla.Application.Persistence;
using Wasla.Domain.Practices;

namespace Wasla.Application.Features.Reservations;

internal sealed class ReservationScheduleGuard(
    IWaslaDataStore dataStore,
    IDateTimeProvider clock)
{
    public async Task<Result> EnsureFutureReservationsRemainValidAsync(
        Guid practiceId,
        IReadOnlyCollection<DoctorPracticeSchedulePeriod> periods,
        IReadOnlyCollection<DoctorPracticeScheduleException> exceptions,
        CancellationToken cancellationToken)
    {
        var records = await dataStore.ListFutureActiveReservationPatientsByPracticeAsync(
            practiceId, clock.UtcNow, cancellationToken);
        var affected = records.Where(record =>
        {
            var reservation = record.Reservation;
            var effective = DoctorPracticeAvailabilityCalculator.GetEffectiveWorkingPeriods(
                reservation.BusinessDate, periods, exceptions);
            var localTime = TimeOnly.FromDateTime(reservation.ScheduledLocalDateTime);
            return !effective.Any(period =>
                period.SlotDurationMinutes == reservation.SlotDurationMinutesSnapshot &&
                DoctorPracticeAvailabilityCalculator.CalculateSlotStarts(period).Contains(localTime));
        }).ToArray();
        if (affected.Length == 0)
        {
            return Result.Ok();
        }

        var details = JsonSerializer.Serialize(new
        {
            AffectedCount = affected.Length,
            Reservations = affected.Select(item => new
            {
                ReservationId = item.Reservation.Id,
                item.Reservation.ReservationReference,
                PatientId = item.Patient.Id,
                item.Patient.NameAr,
                item.Patient.NameEn,
                item.Reservation.BusinessDate,
                ScheduledTime = TimeOnly.FromDateTime(item.Reservation.ScheduledLocalDateTime)
            })
        });
        return Result.Fail(DoctorPracticeErrors.FutureReservationsExist(details));
    }
}
