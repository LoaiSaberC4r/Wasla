namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Options;

public sealed class ArchiveOptions
{
    public bool Enabled { get; set; }
    public bool ReadModelsVerified { get; set; }
    public int BatchSize { get; set; } = 50;
    public int IntervalSeconds { get; set; } = 300;
}
