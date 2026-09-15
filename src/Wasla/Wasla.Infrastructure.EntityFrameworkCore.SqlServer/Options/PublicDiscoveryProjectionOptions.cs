namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Options;

public sealed class PublicDiscoveryProjectionOptions
{
    public const string SectionName = "PublicDiscoveryProjection";

    public bool Enabled { get; init; } = true;
    public int RefreshIntervalHours { get; init; } = 24;
}
