namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Options;

public sealed class EmailOutboxOptions
{
    public const string SectionName = "EmailOutbox";

    public bool Enabled { get; set; } = true;
    public int PollingIntervalSeconds { get; set; } = 15;
    public int BatchSize { get; set; } = 20;
    public int MaxAttempts { get; set; } = 5;
    public int ClaimLeaseSeconds { get; set; } = 300;
}
