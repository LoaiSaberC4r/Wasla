using System.Diagnostics.Metrics;
using Wasla.Domain.Archives;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Persistence;

internal static class ArchiveTelemetry
{
    private static readonly Meter Meter = new("Wasla.Archive", "18.2");
    private static readonly Counter<long> Scanned = Meter.CreateCounter<long>("wasla.archive.scanned");
    private static readonly Counter<long> Moved = Meter.CreateCounter<long>("wasla.archive.moved");
    private static readonly Counter<long> Excluded = Meter.CreateCounter<long>("wasla.archive.excluded");
    private static readonly Counter<long> Exceptions = Meter.CreateCounter<long>("wasla.archive.integrity_exceptions");
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("wasla.archive.batch.duration", "s");
    internal static void Batch(ArchiveRun run, TimeSpan elapsed)
    {
        Scanned.Add(run.Scanned, new KeyValuePair<string, object?>("mode", run.Mode));
        Moved.Add(run.ArchivedTickets, new KeyValuePair<string, object?>("entityType", "Ticket"));
        Moved.Add(run.ArchivedReservations, new KeyValuePair<string, object?>("entityType", "Reservation"));
        Excluded.Add(run.Excluded, new KeyValuePair<string, object?>("mode", run.Mode));
        Duration.Record(elapsed.TotalSeconds, new("mode", run.Mode), new("outcome", run.Outcome));
    }
    internal static void Integrity(string code) => Exceptions.Add(1, new KeyValuePair<string, object?>("failureCode", code));
}
