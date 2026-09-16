using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Results;

namespace Wasla.Domain.Practices;

public static class DoctorPracticePlatformDefaults
{
    public const bool AllowOnlineBooking = true;
    public const bool AllowWalkIn = true;
    public const int DefaultSlotDurationMinutes = 20;
    public const int CheckInGracePeriodMinutes = 15;
    public const int PatientSelfCancellationCutoffMinutes = 120;
    public const int MaximumTicketCallAttempts = 3;
    public const int NoShowAfterPassedPatientsCount = 3;
    public const string TimeZoneId = "Africa/Cairo";
    public const string PrimaryColor = "#176B87";
    public const string SecondaryColor = "#64CCC5";
    public const string BackgroundColor = "#FFFFFF";
    public const string TextColor = "#102A43";
}

public sealed class DoctorPracticeConfiguration : AggregateRoot<Guid>, IAuditableEntity
{
    private DoctorPracticeConfiguration()
    {
    }

    private DoctorPracticeConfiguration(Guid id, Guid doctorPracticeId, Guid? actorId)
        : base(id)
    {
        DoctorPracticeId = doctorPracticeId;
        AllowOnlineBooking = DoctorPracticePlatformDefaults.AllowOnlineBooking;
        AllowWalkIn = DoctorPracticePlatformDefaults.AllowWalkIn;
        DefaultSlotDurationMinutes = DoctorPracticePlatformDefaults.DefaultSlotDurationMinutes;
        CheckInGracePeriodMinutes = DoctorPracticePlatformDefaults.CheckInGracePeriodMinutes;
        PatientSelfCancellationCutoffMinutes = DoctorPracticePlatformDefaults.PatientSelfCancellationCutoffMinutes;
        MaximumTicketCallAttempts = DoctorPracticePlatformDefaults.MaximumTicketCallAttempts;
        NoShowAfterPassedPatientsCount = DoctorPracticePlatformDefaults.NoShowAfterPassedPatientsCount;
        TimeZoneId = DoctorPracticePlatformDefaults.TimeZoneId;
        CreatedByApplicationUserId = actorId;
    }

    public Guid DoctorPracticeId { get; private set; }
    public bool AllowOnlineBooking { get; private set; }
    public bool AllowWalkIn { get; private set; }
    public int DefaultSlotDurationMinutes { get; private set; }
    public int CheckInGracePeriodMinutes { get; private set; }
    public int PatientSelfCancellationCutoffMinutes { get; private set; }
    public int? MaximumDailyPatients { get; private set; }
    public int MaximumTicketCallAttempts { get; private set; }
    public int NoShowAfterPassedPatientsCount { get; private set; }
    public string TimeZoneId { get; private set; } = string.Empty;
    public Guid? CreatedByApplicationUserId { get; private set; }
    public Guid? ModifiedByApplicationUserId { get; private set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }
    public byte[] RowVersion { get; private set; } = [];

    public static Result<DoctorPracticeConfiguration> CreateDefault(
        Guid id,
        Guid doctorPracticeId,
        Guid? actorId = null)
        => id == Guid.Empty || doctorPracticeId == Guid.Empty
            ? Result<DoctorPracticeConfiguration>.Fail(DoctorPracticeConfigurationErrors.Invalid)
            : Result<DoctorPracticeConfiguration>.Ok(new DoctorPracticeConfiguration(id, doctorPracticeId, actorId));

    public Result Update(
        bool allowOnlineBooking,
        bool allowWalkIn,
        int defaultSlotDurationMinutes,
        int checkInGracePeriodMinutes,
        int patientSelfCancellationCutoffMinutes,
        int? maximumDailyPatients,
        int maximumTicketCallAttempts,
        string timeZoneId,
        Guid? actorId = null)
        => Update(
            allowOnlineBooking,
            allowWalkIn,
            defaultSlotDurationMinutes,
            checkInGracePeriodMinutes,
            patientSelfCancellationCutoffMinutes,
            maximumDailyPatients,
            maximumTicketCallAttempts,
            NoShowAfterPassedPatientsCount,
            timeZoneId,
            actorId);

    public Result Update(
        bool allowOnlineBooking,
        bool allowWalkIn,
        int defaultSlotDurationMinutes,
        int checkInGracePeriodMinutes,
        int patientSelfCancellationCutoffMinutes,
        int? maximumDailyPatients,
        int maximumTicketCallAttempts,
        int noShowAfterPassedPatientsCount,
        string timeZoneId,
        Guid? actorId = null)
    {
        var normalizedTimeZone = timeZoneId?.Trim() ?? string.Empty;
        if (defaultSlotDurationMinutes is < 5 or > 480 ||
            checkInGracePeriodMinutes is < 0 or > 1440 ||
            patientSelfCancellationCutoffMinutes is < 0 or > 43200 ||
            maximumDailyPatients is <= 0 or > 10000 ||
            maximumTicketCallAttempts is < 1 or > 100 ||
            noShowAfterPassedPatientsCount is < 1 or > 100 ||
            normalizedTimeZone.Length is 0 or > 100)
        {
            return Result.Fail(DoctorPracticeConfigurationErrors.Invalid);
        }

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(normalizedTimeZone);
        }
        catch (TimeZoneNotFoundException)
        {
            return Result.Fail(DoctorPracticeConfigurationErrors.InvalidTimeZone);
        }
        catch (InvalidTimeZoneException)
        {
            return Result.Fail(DoctorPracticeConfigurationErrors.InvalidTimeZone);
        }

        AllowOnlineBooking = allowOnlineBooking;
        AllowWalkIn = allowWalkIn;
        DefaultSlotDurationMinutes = defaultSlotDurationMinutes;
        CheckInGracePeriodMinutes = checkInGracePeriodMinutes;
        PatientSelfCancellationCutoffMinutes = patientSelfCancellationCutoffMinutes;
        MaximumDailyPatients = maximumDailyPatients;
        MaximumTicketCallAttempts = maximumTicketCallAttempts;
        NoShowAfterPassedPatientsCount = noShowAfterPassedPatientsCount;
        TimeZoneId = normalizedTimeZone;
        ModifiedByApplicationUserId = actorId;
        return Result.Ok();
    }

    public int EffectiveDailyCapacity(int scheduleCapacity)
        => MaximumDailyPatients.HasValue
            ? Math.Min(scheduleCapacity, MaximumDailyPatients.Value)
            : scheduleCapacity;

    public bool IsLate(DateTimeOffset appointmentTime, DateTimeOffset arrivalTime)
        => arrivalTime > appointmentTime.AddMinutes(CheckInGracePeriodMinutes);

    public bool CanPatientCancel(DateTimeOffset appointmentTime, DateTimeOffset now)
        => now <= appointmentTime.AddMinutes(-PatientSelfCancellationCutoffMinutes);
}

public static class DoctorPracticeConfigurationErrors
{
    public static Error Invalid => Error.Validation(
        "DoctorPracticeConfiguration.Invalid",
        DoctorPracticeErrors.Text("DoctorPracticeConfigurationInvalid"));
    public static Error InvalidTimeZone => Error.Validation(
        "DoctorPracticeConfiguration.InvalidTimeZone",
        DoctorPracticeErrors.Text("DoctorPracticeConfigurationInvalidTimeZone"));
    public static Error NotFound => Error.NotFound(
        "DoctorPracticeConfiguration.NotFound",
        DoctorPracticeErrors.Text("DoctorPracticeConfigurationNotFound"));
    public static Error ConcurrencyConflict => Error.Conflict(
        "DoctorPracticeConfiguration.ConcurrencyConflict",
        DoctorPracticeErrors.Text("DoctorPracticeConfigurationConcurrencyConflict"));
}

