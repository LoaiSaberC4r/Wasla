using BuildingBlock.Domain.Results;
using Wasla.Application.Features.PublicDiscovery;
using Wasla.Domain.Clinical;
using Wasla.Domain.Common;
using Wasla.Domain.Practices;
using Wasla.Domain.Reservations;
using Wasla.Domain.Security;

namespace Wasla.Application.Features.Reservations;

internal sealed partial class ReservationApplicationService
{
    private async Task<Result<IReadOnlyDictionary<DateOnly, IReadOnlyList<ReservationAvailableSlotResponse>>>>
        BuildFollowUpAvailabilityAsync(Guid practiceId, Guid? patientId, Guid eligibilityId,
            ReservationAvailabilityChannel channel, CancellationToken ct)
    {
        var eligibility = await followUp.FindAsync(eligibilityId, ct);
        if (eligibility is null) return Result<IReadOnlyDictionary<DateOnly, IReadOnlyList<ReservationAvailableSlotResponse>>>.Fail(FollowUpErrors.NotFound);
        var targetPatientId = patientId ?? eligibility.PatientId;
        if (channel == ReservationAvailabilityChannel.Reception && patientId is null)
            return Result<IReadOnlyDictionary<DateOnly, IReadOnlyList<ReservationAvailableSlotResponse>>>.Fail(FollowUpErrors.InvalidPatient);
        var actor = await ResolveCreateActorAsync(targetPatientId, practiceId,
            channel == ReservationAvailabilityChannel.Patient ? ReservationOperationScope.Patient : ReservationOperationScope.Reception, ct);
        if (actor.IsFailure) return Result<IReadOnlyDictionary<DateOnly, IReadOnlyList<ReservationAvailableSlotResponse>>>.Fail(actor.Errors);
        var permission = channel == ReservationAvailabilityChannel.Patient
            ? await RequirePermissionAsync(PermissionNames.FollowUpEligibilityViewOwn, ct)
            : await receptionAuthorization.AuthorizeAsync(practiceId, PermissionNames.FollowUpEligibilityViewBookingEligibility, ct);
        if (permission.IsFailure) return Result<IReadOnlyDictionary<DateOnly, IReadOnlyList<ReservationAvailableSlotResponse>>>.Fail(permission.Errors);
        var practice = await dataStore.FindDoctorPracticeAsync(practiceId, ct);
        if (practice is null || !practice.IsActive) return Result<IReadOnlyDictionary<DateOnly, IReadOnlyList<ReservationAvailableSlotResponse>>>.Fail(FollowUpErrors.InvalidPractice);
        var doctor = await dataStore.FindDoctorByIdAsync(practice.DoctorId, ct);
        var doctorUser = doctor is null ? null : await dataStore.FindUserByIdAsync(doctor.ApplicationUserId, ct);
        if (doctor?.ApprovalStatus != DoctorApprovalStatus.Approved || doctorUser is not { IsActive: true })
            return Result<IReadOnlyDictionary<DateOnly, IReadOnlyList<ReservationAvailableSlotResponse>>>.Fail(FollowUpErrors.InvalidDoctor);
        var today = await followUp.TodayAsync(practiceId, ct);
        var scope = eligibility.ValidateScope(targetPatientId, practiceId, practice.DoctorId);
        if (scope.IsFailure) return Result<IReadOnlyDictionary<DateOnly, IReadOnlyList<ReservationAvailableSlotResponse>>>.Fail(scope.Errors);
        var usable = eligibility.ValidateAvailable(today, today);
        if (usable.IsFailure) return Result<IReadOnlyDictionary<DateOnly, IReadOnlyList<ReservationAvailableSlotResponse>>>.Fail(usable.Errors);
        var configuration = await dataStore.FindDoctorPracticeConfigurationAsync(practiceId, ct);
        if (configuration is null || channel == ReservationAvailabilityChannel.Patient && !configuration.AllowOnlineBooking)
            return Result<IReadOnlyDictionary<DateOnly, IReadOnlyList<ReservationAvailableSlotResponse>>>.Fail(ReservationErrors.OnlineBookingDisabled);
        var through = today.AddDays(ReservationPolicy.MaximumAdvanceBookingDays - 1);
        if (eligibility.ValidUntil < through) through = eligibility.ValidUntil;
        var periods = await dataStore.ListDoctorPracticeSchedulePeriodsAsync(practiceId, ct);
        var exceptions = await dataStore.ListDoctorPracticeScheduleExceptionsAsync(practiceId, ct);
        var occupancy = await occupancyReader.ReadAsync([practiceId], today, through, ct);
        var conflicts = await dataStore.ListReservationAvailabilityConflictsAsync(targetPatientId, today, through, Guid.Empty, ct);
        var segments = await dataStore.ListDoctorPracticeSegmentsAsync(practiceId, ct);
        var visitTypes = await dataStore.ListDoctorPracticeVisitTypesAsync(practiceId, ct);
        var prices = await dataStore.ListDoctorPracticePricesAsync(practiceId, ct);
        var followUpTypes = visitTypes.Where(v => v.IsActive && v.Type == DoctorPracticeVisitTypeCode.FollowUp).Select(v => v.Id).ToHashSet();
        var pricedSegments = prices.Where(p => followUpTypes.Contains(p.VisitTypeId)).Select(p => p.SegmentId).ToHashSet();
        var activeSegments = segments.Where(s => s.IsActive).ToArray();
        var zone = TimeZoneInfo.FindSystemTimeZoneById(configuration.TimeZoneId);
        var now = new DateTimeOffset(EnsureUtc(clock.UtcNow));
        var localNow = TimeZoneInfo.ConvertTime(now, zone);
        var hasFutureConsultation = conflicts.Any(c => c.Status == ReservationStatus.Active && c.DoctorId == practice.DoctorId &&
            c.ScheduledStartUtc > clock.UtcNow && c.VisitTypeCodeSnapshot == nameof(DoctorPracticeVisitTypeCode.NewConsultation));
        var result = new SortedDictionary<DateOnly, IReadOnlyList<ReservationAvailableSlotResponse>>();
        for (var date = today; date <= through; date = date.AddDays(1))
        {
            var effective = DoctorPracticeAvailabilityCalculator.GetEffectiveWorkingPeriods(date, periods, exceptions);
            var generated = effective.SelectMany(period => DoctorPracticeAvailabilityCalculator.CalculateSlotStarts(period)
                .Select(time => new ReservationAvailableSlotResponse(date, time, period.SlotDurationMinutes))).DistinctBy(s => s.Time).OrderBy(s => s.Time).ToArray();
            var day = occupancy.GetValueOrDefault((practiceId, date), PracticeOccupancySnapshot.Empty);
            var capacity = configuration.EffectiveDailyCapacity(generated.Length);
            var protectedCapacity = activeSegments.Where(s => !s.IsDefault).Sum(s => s.ProtectedCapacity(
                day.SegmentReservationCounts.GetValueOrDefault(s.Id), date, effective, now, zone));
            var general = Math.Max(0, capacity - day.TotalReservations - protectedCapacity);
            var canChooseSegment = activeSegments.Any(s => pricedSegments.Contains(s.Id) &&
                (general > 0 || s.ProtectedCapacity(day.SegmentReservationCounts.GetValueOrDefault(s.Id), date, effective, now, zone) > 0));
            var blocked = hasFutureConsultation || conflicts.Any(c => c.DoctorPracticeId == practiceId && c.BusinessDate == date &&
                c.Status is ReservationStatus.Active or ReservationStatus.NoShow);
            result[date] = blocked || !canChooseSegment || day.TotalReservations >= capacity ? [] : generated.Where(slot =>
            {
                if (date == today && slot.Time <= TimeOnly.FromDateTime(localNow.DateTime) || day.OccupiedSlots.Contains(slot.Time)) return false;
                var localStart = DateTime.SpecifyKind(date.ToDateTime(slot.Time), DateTimeKind.Unspecified);
                if (zone.IsInvalidTime(localStart) || zone.IsAmbiguousTime(localStart)) return false;
                var start = TimeZoneInfo.ConvertTimeToUtc(localStart, zone);
                var end = start.AddMinutes(slot.DurationMinutes);
                return !conflicts.Any(c => c.Status is ReservationStatus.Active or ReservationStatus.ConvertedToTicket &&
                    c.ScheduledStartUtc < end && c.ScheduledEndUtc > start);
            }).ToArray();
        }
        return Result<IReadOnlyDictionary<DateOnly, IReadOnlyList<ReservationAvailableSlotResponse>>>.Ok(result);
    }
}
