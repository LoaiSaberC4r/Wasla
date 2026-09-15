using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Results;

namespace Wasla.Domain.Practices;

public enum DoctorPracticeVisitTypeCode
{
    NewConsultation = 1,
    FollowUp = 2
}

public sealed class DoctorPracticeSegment : AggregateRoot<Guid>, IAuditableEntity
{
    private DoctorPracticeSegment()
    {
    }

    private DoctorPracticeSegment(
        Guid id,
        Guid doctorPracticeId,
        string nameAr,
        string? nameEn,
        int priority,
        int? reservedDailyQuota,
        int? quotaReleaseBeforeMinutes,
        bool isDefault,
        Guid? actorId)
        : base(id)
    {
        DoctorPracticeId = doctorPracticeId;
        NameAr = nameAr;
        NameEn = nameEn;
        Priority = priority;
        ReservedDailyQuota = reservedDailyQuota;
        QuotaReleaseBeforeMinutes = quotaReleaseBeforeMinutes;
        IsDefault = isDefault;
        IsActive = true;
        CreatedByApplicationUserId = actorId;
    }

    public Guid DoctorPracticeId { get; private set; }
    public string NameAr { get; private set; } = string.Empty;
    public string? NameEn { get; private set; }
    public int Priority { get; private set; }
    public int? ReservedDailyQuota { get; private set; }
    public int? QuotaReleaseBeforeMinutes { get; private set; }
    public bool IsDefault { get; private set; }
    public bool IsActive { get; private set; }
    public Guid? CreatedByApplicationUserId { get; private set; }
    public Guid? ModifiedByApplicationUserId { get; private set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }
    public byte[] RowVersion { get; private set; } = [];

    public static Result<DoctorPracticeSegment> CreateDefault(
        Guid id,
        Guid doctorPracticeId,
        Guid? actorId = null)
        => Create(id, doctorPracticeId, "عادي", "Normal", 0, null, null, true, actorId);

    public static Result<DoctorPracticeSegment> Create(
        Guid id,
        Guid doctorPracticeId,
        string nameAr,
        string? nameEn,
        int priority,
        int? reservedDailyQuota,
        int? quotaReleaseBeforeMinutes,
        bool isDefault = false,
        Guid? actorId = null)
    {
        var normalizedAr = nameAr?.Trim() ?? string.Empty;
        var normalizedEn = string.IsNullOrWhiteSpace(nameEn) ? null : nameEn.Trim();
        return !IsValid(
            id,
            doctorPracticeId,
            normalizedAr,
            normalizedEn,
            priority,
            reservedDailyQuota,
            quotaReleaseBeforeMinutes)
            ? Result<DoctorPracticeSegment>.Fail(DoctorPracticeSegmentErrors.Invalid)
            : Result<DoctorPracticeSegment>.Ok(new DoctorPracticeSegment(
                id,
                doctorPracticeId,
                normalizedAr,
                normalizedEn,
                priority,
                reservedDailyQuota,
                quotaReleaseBeforeMinutes,
                isDefault,
                actorId));
    }

    public Result Update(
        string nameAr,
        string? nameEn,
        int priority,
        int? reservedDailyQuota,
        int? quotaReleaseBeforeMinutes,
        bool isActive,
        Guid? actorId = null)
    {
        var normalizedAr = nameAr?.Trim() ?? string.Empty;
        var normalizedEn = string.IsNullOrWhiteSpace(nameEn) ? null : nameEn.Trim();
        if (!IsValid(
                Id,
                DoctorPracticeId,
                normalizedAr,
                normalizedEn,
                priority,
                reservedDailyQuota,
                quotaReleaseBeforeMinutes))
        {
            return Result.Fail(DoctorPracticeSegmentErrors.Invalid);
        }

        if (IsDefault && !isActive)
        {
            return Result.Fail(DoctorPracticeSegmentErrors.DefaultSegmentProtected);
        }

        NameAr = normalizedAr;
        NameEn = normalizedEn;
        Priority = priority;
        ReservedDailyQuota = reservedDailyQuota;
        QuotaReleaseBeforeMinutes = quotaReleaseBeforeMinutes;
        IsActive = isActive;
        ModifiedByApplicationUserId = actorId;
        return Result.Ok();
    }

    public Result Deactivate(Guid? actorId = null)
    {
        if (IsDefault)
        {
            return Result.Fail(DoctorPracticeSegmentErrors.DefaultSegmentProtected);
        }

        IsActive = false;
        ModifiedByApplicationUserId = actorId;
        return Result.Ok();
    }

    public int ProtectedCapacity(
        int alreadyUsedReservedCapacity,
        DateOnly workingDate,
        IReadOnlyCollection<PracticeWorkingPeriod> effectiveWorkingPeriods,
        DateTimeOffset now,
        TimeZoneInfo practiceTimeZone)
    {
        if (!IsActive || ReservedDailyQuota is null || ReservedDailyQuota <= alreadyUsedReservedCapacity)
        {
            return 0;
        }

        if (QuotaReleaseBeforeMinutes is null || effectiveWorkingPeriods.Count == 0)
        {
            return ReservedDailyQuota.Value - alreadyUsedReservedCapacity;
        }

        var endOfWorkingDay = effectiveWorkingPeriods.Max(period => period.EndTime);
        var localCutoff = workingDate.ToDateTime(endOfWorkingDay)
            .AddMinutes(-QuotaReleaseBeforeMinutes.Value);
        var cutoff = new DateTimeOffset(localCutoff, practiceTimeZone.GetUtcOffset(localCutoff));
        return now >= cutoff ? 0 : ReservedDailyQuota.Value - alreadyUsedReservedCapacity;
    }

    private static bool IsValid(
        Guid id,
        Guid doctorPracticeId,
        string nameAr,
        string? nameEn,
        int priority,
        int? reservedDailyQuota,
        int? quotaReleaseBeforeMinutes)
        => id != Guid.Empty && doctorPracticeId != Guid.Empty &&
           nameAr.Length is > 0 and <= 200 && nameEn?.Length <= 200 &&
           priority is >= -1000 and <= 1000 &&
           reservedDailyQuota is null or > 0 and <= 10000 &&
           quotaReleaseBeforeMinutes is null or >= 0 and <= 10080 &&
           reservedDailyQuota.HasValue == quotaReleaseBeforeMinutes.HasValue;
}

public sealed class DoctorPracticeVisitType : AggregateRoot<Guid>, IAuditableEntity
{
    private DoctorPracticeVisitType()
    {
    }

    private DoctorPracticeVisitType(
        Guid id,
        Guid doctorPracticeId,
        DoctorPracticeVisitTypeCode type,
        string nameAr,
        string? nameEn,
        Guid? actorId)
        : base(id)
    {
        DoctorPracticeId = doctorPracticeId;
        Type = type;
        NameAr = nameAr;
        NameEn = nameEn;
        IsActive = true;
        CreatedByApplicationUserId = actorId;
    }

    public Guid DoctorPracticeId { get; private set; }
    public DoctorPracticeVisitTypeCode Type { get; private set; }
    public string NameAr { get; private set; } = string.Empty;
    public string? NameEn { get; private set; }
    public bool IsActive { get; private set; }
    public Guid? CreatedByApplicationUserId { get; private set; }
    public Guid? ModifiedByApplicationUserId { get; private set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }
    public byte[] RowVersion { get; private set; } = [];

    public static Result<DoctorPracticeVisitType> CreateDefault(
        Guid id,
        Guid doctorPracticeId,
        DoctorPracticeVisitTypeCode type,
        Guid? actorId = null)
        => type switch
        {
            DoctorPracticeVisitTypeCode.NewConsultation => Create(
                id, doctorPracticeId, type, "كشف جديد", "New consultation", actorId),
            DoctorPracticeVisitTypeCode.FollowUp => Create(
                id, doctorPracticeId, type, "متابعة", "Follow-up", actorId),
            _ => Result<DoctorPracticeVisitType>.Fail(DoctorPracticeVisitTypeErrors.Invalid)
        };

    public static Result<DoctorPracticeVisitType> Create(
        Guid id,
        Guid doctorPracticeId,
        DoctorPracticeVisitTypeCode type,
        string nameAr,
        string? nameEn,
        Guid? actorId = null)
    {
        var normalizedAr = nameAr?.Trim() ?? string.Empty;
        var normalizedEn = string.IsNullOrWhiteSpace(nameEn) ? null : nameEn.Trim();
        return !IsValid(id, doctorPracticeId, type, normalizedAr, normalizedEn)
            ? Result<DoctorPracticeVisitType>.Fail(DoctorPracticeVisitTypeErrors.Invalid)
            : Result<DoctorPracticeVisitType>.Ok(new DoctorPracticeVisitType(
                id, doctorPracticeId, type, normalizedAr, normalizedEn, actorId));
    }

    public Result Update(
        string nameAr,
        string? nameEn,
        bool isActive,
        Guid? actorId = null)
    {
        var normalizedAr = nameAr?.Trim() ?? string.Empty;
        var normalizedEn = string.IsNullOrWhiteSpace(nameEn) ? null : nameEn.Trim();
        if (!IsValid(Id, DoctorPracticeId, Type, normalizedAr, normalizedEn))
        {
            return Result.Fail(DoctorPracticeVisitTypeErrors.Invalid);
        }

        NameAr = normalizedAr;
        NameEn = normalizedEn;
        IsActive = isActive;
        ModifiedByApplicationUserId = actorId;
        return Result.Ok();
    }

    private static bool IsValid(
        Guid id,
        Guid doctorPracticeId,
        DoctorPracticeVisitTypeCode type,
        string nameAr,
        string? nameEn)
        => id != Guid.Empty && doctorPracticeId != Guid.Empty && Enum.IsDefined(type) &&
           nameAr.Length is > 0 and <= 200 && nameEn?.Length <= 200;
}

public sealed class DoctorPracticeSegmentVisitTypePrice : AggregateRoot<Guid>, IAuditableEntity
{
    private DoctorPracticeSegmentVisitTypePrice()
    {
    }

    private DoctorPracticeSegmentVisitTypePrice(
        Guid id,
        Guid doctorPracticeId,
        Guid segmentId,
        Guid visitTypeId,
        decimal price,
        Guid? actorId)
        : base(id)
    {
        DoctorPracticeId = doctorPracticeId;
        SegmentId = segmentId;
        VisitTypeId = visitTypeId;
        Price = price;
        CreatedByApplicationUserId = actorId;
    }

    public Guid DoctorPracticeId { get; private set; }
    public Guid SegmentId { get; private set; }
    public Guid VisitTypeId { get; private set; }
    public decimal Price { get; private set; }
    public Guid? CreatedByApplicationUserId { get; private set; }
    public Guid? ModifiedByApplicationUserId { get; private set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? ModifiedOnUtc { get; set; }
    public byte[] RowVersion { get; private set; } = [];

    public static Result<DoctorPracticeSegmentVisitTypePrice> Create(
        Guid id,
        Guid doctorPracticeId,
        Guid segmentId,
        Guid visitTypeId,
        decimal price,
        Guid? actorId = null)
        => id == Guid.Empty || doctorPracticeId == Guid.Empty || segmentId == Guid.Empty ||
           visitTypeId == Guid.Empty || price <= 0 || price > 10000000
            ? Result<DoctorPracticeSegmentVisitTypePrice>.Fail(DoctorPracticePricingErrors.Invalid)
            : Result<DoctorPracticeSegmentVisitTypePrice>.Ok(new DoctorPracticeSegmentVisitTypePrice(
                id, doctorPracticeId, segmentId, visitTypeId, price, actorId));

    public Result Update(decimal price, Guid? actorId = null)
    {
        if (price <= 0 || price > 10000000)
        {
            return Result.Fail(DoctorPracticePricingErrors.Invalid);
        }

        Price = price;
        ModifiedByApplicationUserId = actorId;
        return Result.Ok();
    }
}

public readonly record struct PracticeQueueEntry(int SegmentPriority, DateTimeOffset CheckInTime);

public sealed class DoctorPracticeQueueComparer : IComparer<PracticeQueueEntry>
{
    public static DoctorPracticeQueueComparer Instance { get; } = new();

    private DoctorPracticeQueueComparer()
    {
    }

    public int Compare(PracticeQueueEntry left, PracticeQueueEntry right)
    {
        var priority = right.SegmentPriority.CompareTo(left.SegmentPriority);
        return priority != 0 ? priority : left.CheckInTime.CompareTo(right.CheckInTime);
    }
}

public static class DoctorPracticeSegmentErrors
{
    public static Error Invalid => Error.Validation(
        "DoctorPracticeSegment.Invalid",
        DoctorPracticeErrors.Text("DoctorPracticeSegmentInvalid"));
    public static Error NotFound => Error.NotFound(
        "DoctorPracticeSegment.NotFound",
        DoctorPracticeErrors.Text("DoctorPracticeSegmentNotFound"));
    public static Error DefaultSegmentProtected => Error.Conflict(
        "DoctorPracticeSegment.DefaultSegmentProtected",
        DoctorPracticeErrors.Text("DoctorPracticeSegmentDefaultProtected"));
    public static Error ConcurrencyConflict => Error.Conflict(
        "DoctorPracticeSegment.ConcurrencyConflict",
        DoctorPracticeErrors.Text("DoctorPracticeSegmentConcurrencyConflict"));
}

public static class DoctorPracticeVisitTypeErrors
{
    public static Error Invalid => Error.Validation(
        "DoctorPracticeVisitType.Invalid",
        DoctorPracticeErrors.Text("DoctorPracticeVisitTypeInvalid"));
    public static Error NotFound => Error.NotFound(
        "DoctorPracticeVisitType.NotFound",
        DoctorPracticeErrors.Text("DoctorPracticeVisitTypeNotFound"));
    public static Error ConcurrencyConflict => Error.Conflict(
        "DoctorPracticeVisitType.ConcurrencyConflict",
        DoctorPracticeErrors.Text("DoctorPracticeVisitTypeConcurrencyConflict"));
}

public static class DoctorPracticePricingErrors
{
    public static Error Invalid => Error.Validation(
        "DoctorPracticePricing.Invalid",
        DoctorPracticeErrors.Text("DoctorPracticePricingInvalid"));
    public static Error ScopeMismatch => Error.Validation(
        "DoctorPracticePricing.ScopeMismatch",
        DoctorPracticeErrors.Text("DoctorPracticePricingScopeMismatch"));
    public static Error Duplicate => Error.Conflict(
        "DoctorPracticePricing.Duplicate",
        DoctorPracticeErrors.Text("DoctorPracticePricingDuplicate"));
    public static Error NotFound => Error.NotFound(
        "DoctorPracticePricing.NotFound",
        DoctorPracticeErrors.Text("DoctorPracticePricingNotFound"));
    public static Error ConcurrencyConflict => Error.Conflict(
        "DoctorPracticePricing.ConcurrencyConflict",
        DoctorPracticeErrors.Text("DoctorPracticePricingConcurrencyConflict"));
}
