using BuildingBlock.Application.Time;

namespace BuildingBlock.Infrastructure.Time
{
    internal sealed class SystemDateTimeProvider : IDateTimeProvider
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }
}
