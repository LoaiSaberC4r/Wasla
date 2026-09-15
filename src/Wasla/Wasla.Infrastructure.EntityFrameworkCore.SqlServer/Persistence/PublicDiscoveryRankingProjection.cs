namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

/// <summary>
/// Disposable read-model row. The visibility boundary makes a projected future slot enter the
/// rolling public 30-day window without a request-time schedule expansion or a midnight rebuild.
/// </summary>
public sealed class PublicPracticeAvailabilitySlot
{
    private PublicPracticeAvailabilitySlot()
    {
    }

    internal PublicPracticeAvailabilitySlot(
        Guid doctorId,
        Guid doctorPracticeId,
        DateOnly localDate,
        TimeOnly localTime,
        DateTime slotStartUtc,
        DateTime visibleFromUtc,
        DateTime refreshedOnUtc)
    {
        DoctorId = doctorId;
        DoctorPracticeId = doctorPracticeId;
        LocalDate = localDate;
        LocalTime = localTime;
        SlotStartUtc = slotStartUtc;
        VisibleFromUtc = visibleFromUtc;
        IsAvailable = true;
        RefreshedOnUtc = refreshedOnUtc;
    }

    public Guid DoctorId { get; private set; }
    public Guid DoctorPracticeId { get; private set; }
    public DateOnly LocalDate { get; private set; }
    public TimeOnly LocalTime { get; private set; }
    public DateTime SlotStartUtc { get; private set; }
    public DateTime VisibleFromUtc { get; private set; }
    public bool IsAvailable { get; private set; }
    public DateTime RefreshedOnUtc { get; private set; }
}

/// <summary>
/// Small ranking projection kept separate from future reservation domain storage.
/// </summary>
public sealed class PublicDoctorSearchRank
{
    private PublicDoctorSearchRank()
    {
    }

    internal PublicDoctorSearchRank(Guid doctorId, long popularityScore, DateTime refreshedOnUtc)
    {
        DoctorId = doctorId;
        PopularityScore = popularityScore;
        RefreshedOnUtc = refreshedOnUtc;
    }

    public Guid DoctorId { get; private set; }
    public long PopularityScore { get; private set; }
    public DateTime RefreshedOnUtc { get; private set; }

    internal void Refresh(long popularityScore, DateTime refreshedOnUtc)
    {
        PopularityScore = popularityScore;
        RefreshedOnUtc = refreshedOnUtc;
    }
}
