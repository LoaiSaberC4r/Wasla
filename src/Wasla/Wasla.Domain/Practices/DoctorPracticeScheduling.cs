using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Results;

namespace Wasla.Domain.Practices;

public enum DoctorPracticeScheduleExceptionType
{
    DayOff = 1,
    Vacation = 2,
    CustomWorkingHours = 3,
    BlockedTimeRange = 4
}

public readonly record struct PracticeWorkingPeriod(TimeOnly StartTime, TimeOnly EndTime, int SlotDurationMinutes)
{
    public bool IsValid => StartTime < EndTime && SlotDurationMinutes is >= 5 and <= 480;
}

public sealed class DoctorPracticeSchedulePeriod : AggregateRoot<Guid>, IAuditableEntity
{
    private DoctorPracticeSchedulePeriod()
    {
    }

    private DoctorPracticeSchedulePeriod(
        Guid id,
        Guid doctorPracticeId,
        DayOfWeek dayOfWeek,
        TimeOnly startTime,
        TimeOnly endTime,
        int slotDurationMinutes,
        Guid? actorId)
        : base(id)
    {
        DoctorPracticeId = doctorPracticeId;
        DayOfWeek = dayOfWeek;
        StartTime = startTime;
        EndTime = endTime;
        SlotDurationMinutes = slotDurationMinutes;
        CreatedByApplicationUserId = actorId;
    }

    public Guid DoctorPracticeId { get; private set; }
    public DayOfWeek DayOfWeek { get; private set; }
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }
    public int SlotDurationMinutes { get; private set; }
    public Guid? CreatedByApplicationUserId { get; private set; }
    public Guid? ModifiedByApplicationUserId { get; private set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }
    public byte[] RowVersion { get; private set; } = [];

    public static Result<DoctorPracticeSchedulePeriod> Create(
        Guid id,
        Guid doctorPracticeId,
        DayOfWeek dayOfWeek,
        TimeOnly startTime,
        TimeOnly endTime,
        int slotDurationMinutes,
        Guid? actorId = null)
        => !IsValid(id, doctorPracticeId, dayOfWeek, startTime, endTime, slotDurationMinutes)
            ? Result<DoctorPracticeSchedulePeriod>.Fail(DoctorPracticeScheduleErrors.InvalidPeriod)
            : Result<DoctorPracticeSchedulePeriod>.Ok(new DoctorPracticeSchedulePeriod(
                id,
                doctorPracticeId,
                dayOfWeek,
                startTime,
                endTime,
                slotDurationMinutes,
                actorId));

    public Result Update(
        DayOfWeek dayOfWeek,
        TimeOnly startTime,
        TimeOnly endTime,
        int slotDurationMinutes,
        Guid? actorId = null)
    {
        if (!IsValid(Id, DoctorPracticeId, dayOfWeek, startTime, endTime, slotDurationMinutes))
        {
            return Result.Fail(DoctorPracticeScheduleErrors.InvalidPeriod);
        }

        DayOfWeek = dayOfWeek;
        StartTime = startTime;
        EndTime = endTime;
        SlotDurationMinutes = slotDurationMinutes;
        ModifiedByApplicationUserId = actorId;
        return Result.Ok();
    }

    public bool Overlaps(TimeOnly startTime, TimeOnly endTime)
        => StartTime < endTime && startTime < EndTime;

    public PracticeWorkingPeriod ToWorkingPeriod() => new(StartTime, EndTime, SlotDurationMinutes);

    private static bool IsValid(
        Guid id,
        Guid doctorPracticeId,
        DayOfWeek dayOfWeek,
        TimeOnly startTime,
        TimeOnly endTime,
        int slotDurationMinutes)
        => id != Guid.Empty && doctorPracticeId != Guid.Empty && Enum.IsDefined(dayOfWeek) &&
           startTime < endTime && slotDurationMinutes is >= 5 and <= 480;
}

public sealed class DoctorPracticeScheduleException : AggregateRoot<Guid>, IAuditableEntity
{
    private DoctorPracticeScheduleException()
    {
    }

    private DoctorPracticeScheduleException(
        Guid id,
        Guid doctorPracticeId,
        DateOnly date,
        DoctorPracticeScheduleExceptionType type,
        TimeOnly? startTime,
        TimeOnly? endTime,
        int? slotDurationMinutes,
        Guid? actorId)
        : base(id)
    {
        DoctorPracticeId = doctorPracticeId;
        Date = date;
        Type = type;
        StartTime = startTime;
        EndTime = endTime;
        SlotDurationMinutes = slotDurationMinutes;
        CreatedByApplicationUserId = actorId;
    }

    public Guid DoctorPracticeId { get; private set; }
    public DateOnly Date { get; private set; }
    public DoctorPracticeScheduleExceptionType Type { get; private set; }
    public TimeOnly? StartTime { get; private set; }
    public TimeOnly? EndTime { get; private set; }
    public int? SlotDurationMinutes { get; private set; }
    public Guid? CreatedByApplicationUserId { get; private set; }
    public Guid? ModifiedByApplicationUserId { get; private set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }
    public byte[] RowVersion { get; private set; } = [];

    public static Result<DoctorPracticeScheduleException> Create(
        Guid id,
        Guid doctorPracticeId,
        DateOnly date,
        DoctorPracticeScheduleExceptionType type,
        TimeOnly? startTime,
        TimeOnly? endTime,
        int? slotDurationMinutes,
        Guid? actorId = null)
        => !IsValid(id, doctorPracticeId, date, type, startTime, endTime, slotDurationMinutes)
            ? Result<DoctorPracticeScheduleException>.Fail(DoctorPracticeScheduleErrors.InvalidException)
            : Result<DoctorPracticeScheduleException>.Ok(new DoctorPracticeScheduleException(
                id,
                doctorPracticeId,
                date,
                type,
                startTime,
                endTime,
                slotDurationMinutes,
                actorId));

    public Result Update(
        DateOnly date,
        DoctorPracticeScheduleExceptionType type,
        TimeOnly? startTime,
        TimeOnly? endTime,
        int? slotDurationMinutes,
        Guid? actorId = null)
    {
        if (!IsValid(Id, DoctorPracticeId, date, type, startTime, endTime, slotDurationMinutes))
        {
            return Result.Fail(DoctorPracticeScheduleErrors.InvalidException);
        }

        Date = date;
        Type = type;
        StartTime = startTime;
        EndTime = endTime;
        SlotDurationMinutes = slotDurationMinutes;
        ModifiedByApplicationUserId = actorId;
        return Result.Ok();
    }

    private static bool IsValid(
        Guid id,
        Guid doctorPracticeId,
        DateOnly date,
        DoctorPracticeScheduleExceptionType type,
        TimeOnly? startTime,
        TimeOnly? endTime,
        int? slotDurationMinutes)
    {
        if (id == Guid.Empty || doctorPracticeId == Guid.Empty || date == default || !Enum.IsDefined(type))
        {
            return false;
        }

        return type switch
        {
            DoctorPracticeScheduleExceptionType.DayOff or DoctorPracticeScheduleExceptionType.Vacation
                => startTime is null && endTime is null && slotDurationMinutes is null,
            DoctorPracticeScheduleExceptionType.CustomWorkingHours
                => startTime < endTime && slotDurationMinutes is >= 5 and <= 480,
            DoctorPracticeScheduleExceptionType.BlockedTimeRange
                => startTime < endTime && slotDurationMinutes is null,
            _ => false
        };
    }
}

public static class DoctorPracticeAvailabilityCalculator
{
    public static IReadOnlyList<PracticeWorkingPeriod> GetEffectiveWorkingPeriods(
        DateOnly date,
        IEnumerable<DoctorPracticeSchedulePeriod> recurringPeriods,
        IEnumerable<DoctorPracticeScheduleException> exceptions)
    {
        ArgumentNullException.ThrowIfNull(recurringPeriods);
        ArgumentNullException.ThrowIfNull(exceptions);

        var dateExceptions = exceptions.Where(item => item.Date == date).ToArray();
        if (dateExceptions.Any(item => item.Type is
                DoctorPracticeScheduleExceptionType.DayOff or DoctorPracticeScheduleExceptionType.Vacation))
        {
            return [];
        }

        var custom = dateExceptions
            .Where(item => item.Type == DoctorPracticeScheduleExceptionType.CustomWorkingHours)
            .Select(item => new PracticeWorkingPeriod(
                item.StartTime!.Value,
                item.EndTime!.Value,
                item.SlotDurationMinutes!.Value))
            .ToArray();
        var source = custom.Length > 0
            ? custom
            : recurringPeriods
                .Where(item => item.DayOfWeek == date.DayOfWeek)
                .Select(item => item.ToWorkingPeriod())
                .ToArray();

        var blocked = dateExceptions
            .Where(item => item.Type == DoctorPracticeScheduleExceptionType.BlockedTimeRange)
            .Select(item => (Start: item.StartTime!.Value, End: item.EndTime!.Value))
            .OrderBy(item => item.Start)
            .ToArray();

        IEnumerable<PracticeWorkingPeriod> effective = source;
        foreach (var range in blocked)
        {
            effective = effective.SelectMany(period => Subtract(period, range.Start, range.End)).ToArray();
        }

        return effective.OrderBy(item => item.StartTime).ToArray();
    }

    public static IReadOnlyList<TimeOnly> CalculateSlotStarts(PracticeWorkingPeriod period)
    {
        if (!period.IsValid)
        {
            throw new ArgumentException("The working period is invalid.", nameof(period));
        }

        var slots = new List<TimeOnly>();
        var cursor = period.StartTime.ToTimeSpan();
        var end = period.EndTime.ToTimeSpan();
        var duration = TimeSpan.FromMinutes(period.SlotDurationMinutes);
        while (cursor + duration <= end)
        {
            slots.Add(TimeOnly.FromTimeSpan(cursor));
            cursor += duration;
        }

        return slots;
    }

    public static IReadOnlyList<TimeOnly> CalculateAvailableSlotStarts(
        DateOnly date,
        IEnumerable<DoctorPracticeSchedulePeriod> recurringPeriods,
        IEnumerable<DoctorPracticeScheduleException> exceptions)
    {
        ArgumentNullException.ThrowIfNull(recurringPeriods);
        ArgumentNullException.ThrowIfNull(exceptions);

        var dateExceptions = exceptions.Where(item => item.Date == date).ToArray();
        if (dateExceptions.Any(item => item.Type is
                DoctorPracticeScheduleExceptionType.DayOff or DoctorPracticeScheduleExceptionType.Vacation))
        {
            return [];
        }

        var customPeriods = dateExceptions
            .Where(item => item.Type == DoctorPracticeScheduleExceptionType.CustomWorkingHours)
            .Select(item => new PracticeWorkingPeriod(
                item.StartTime!.Value,
                item.EndTime!.Value,
                item.SlotDurationMinutes!.Value))
            .ToArray();
        var periods = customPeriods.Length > 0
            ? customPeriods
            : recurringPeriods
                .Where(item => item.DayOfWeek == date.DayOfWeek)
                .Select(item => item.ToWorkingPeriod())
                .ToArray();
        var blocked = dateExceptions
            .Where(item => item.Type == DoctorPracticeScheduleExceptionType.BlockedTimeRange)
            .Select(item => (Start: item.StartTime!.Value, End: item.EndTime!.Value))
            .ToArray();

        return periods
            .SelectMany(period => CalculateSlotStarts(period).Select(start => (
                Start: start,
                End: TimeOnly.FromTimeSpan(start.ToTimeSpan().Add(
                    TimeSpan.FromMinutes(period.SlotDurationMinutes))))))
            .Where(slot => !blocked.Any(range => slot.Start < range.End && range.Start < slot.End))
            .Select(slot => slot.Start)
            .Distinct()
            .OrderBy(slot => slot)
            .ToArray();
    }

    private static IEnumerable<PracticeWorkingPeriod> Subtract(
        PracticeWorkingPeriod period,
        TimeOnly blockStart,
        TimeOnly blockEnd)
    {
        if (blockStart >= period.EndTime || blockEnd <= period.StartTime)
        {
            yield return period;
            yield break;
        }

        if (blockStart > period.StartTime)
        {
            yield return period with { EndTime = blockStart };
        }

        if (blockEnd < period.EndTime)
        {
            yield return period with { StartTime = blockEnd };
        }
    }
}

public static class DoctorPracticeScheduleErrors
{
    public static Error InvalidPeriod => Error.Validation(
        "DoctorPracticeSchedule.InvalidPeriod",
        DoctorPracticeErrors.Text("DoctorPracticeScheduleInvalidPeriod"));
    public static Error PeriodOverlap => Error.Conflict(
        "DoctorPracticeSchedule.PeriodOverlap",
        DoctorPracticeErrors.Text("DoctorPracticeSchedulePeriodOverlap"));
    public static Error DoctorCrossPracticeOverlap => Error.Conflict(
        "DoctorPracticeSchedule.DoctorCrossPracticeOverlap",
        DoctorPracticeErrors.Text("DoctorPracticeScheduleDoctorCrossPracticeOverlap"));
    public static Error InvalidException => Error.Validation(
        "DoctorPracticeSchedule.InvalidException",
        DoctorPracticeErrors.Text("DoctorPracticeScheduleInvalidException"));
    public static Error NotFound => Error.NotFound(
        "DoctorPracticeSchedule.NotFound",
        DoctorPracticeErrors.Text("DoctorPracticeScheduleNotFound"));
    public static Error ConcurrencyConflict => Error.Conflict(
        "DoctorPracticeSchedule.ConcurrencyConflict",
        DoctorPracticeErrors.Text("DoctorPracticeScheduleConcurrencyConflict"));
}
