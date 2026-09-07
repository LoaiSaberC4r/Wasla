namespace BuildingBlock.Application.Time
{
    public interface IDateTimeProvider
    {
        DateTime UtcNow { get; }
    }
}
