using BuildingBlock.Application.Diagnostics;
using System.Diagnostics.Metrics;

namespace BuildingBlock.Tests;

public sealed class Phase4MetricsTests
{
    [Fact]
    public void Core_metric_names_units_and_tags_are_stable()
    {
        using var listener = new MeterListener();
        var instruments = new Dictionary<string, Instrument>();
        var measurements = new List<Measurement>();
        var gate = new object();

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == BuildingBlockDiagnostics.MeterName)
            {
                instruments[instrument.Name] = instrument;
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            lock (gate)
            {
                measurements.Add(new Measurement(instrument.Name, value, tags.ToArray()));
            }
        });
        listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
        {
            lock (gate)
            {
                measurements.Add(new Measurement(instrument.Name, value, tags.ToArray()));
            }
        });
        listener.Start();

        BuildingBlockDiagnostics.RecordRequest("command", "success", 12.5);
        BuildingBlockDiagnostics.RecordValidationFailures("command", 2);
        BuildingBlockDiagnostics.RecordCacheHit();
        BuildingBlockDiagnostics.RecordCacheMiss();
        BuildingBlockDiagnostics.RecordCacheSet();
        BuildingBlockDiagnostics.RecordCacheInvalidation();
        BuildingBlockDiagnostics.RecordCacheInvalidationFailure();
        BuildingBlockDiagnostics.RecordTransactionStarted();
        BuildingBlockDiagnostics.RecordTransactionCommitted();
        BuildingBlockDiagnostics.RecordTransactionRolledBack();
        BuildingBlockDiagnostics.RecordTransactionCommitFailed();
        BuildingBlockDiagnostics.RecordTransactionRollbackFailed();
        BuildingBlockDiagnostics.RecordDomainEventDispatched();
        BuildingBlockDiagnostics.RecordDomainEventFailure("InvalidOperationException");
        BuildingBlockDiagnostics.RecordEmailSent();
        BuildingBlockDiagnostics.RecordEmailFailure("Connection");
        BuildingBlockDiagnostics.RecordMediaFileSaved(123);
        BuildingBlockDiagnostics.RecordMediaFailure("save", "Validation");
        BuildingBlockDiagnostics.RecordQrCodeGenerated("png");
        BuildingBlockDiagnostics.RecordQrCodeFailure("svg", "Validation");

        listener.RecordObservableInstruments();
        Measurement[] snapshot;
        lock (gate)
        {
            snapshot = measurements.ToArray();
        }

        Assert.Equal("ms", instruments["buildingblock.request.duration"].Unit);
        Assert.Equal("By", instruments["buildingblock.media.file.size"].Unit);
        Assert.Contains(snapshot, m => m.Name == "buildingblock.requests.total");
        Assert.Contains(snapshot, m => m.Name == "buildingblock.request.duration");
        Assert.Contains(snapshot, m => m.Name == "buildingblock.validation.failures");
        Assert.Contains(snapshot, m => m.Name == "buildingblock.cache.hits");
        Assert.Contains(snapshot, m => m.Name == "buildingblock.cache.misses");
        Assert.Contains(snapshot, m => m.Name == "buildingblock.cache.sets");
        Assert.Contains(snapshot, m => m.Name == "buildingblock.cache.invalidations");
        Assert.Contains(snapshot, m => m.Name == "buildingblock.cache.invalidation.failures");
        Assert.Contains(snapshot, m => m.Name == "buildingblock.transactions.started");
        Assert.Contains(snapshot, m => m.Name == "buildingblock.transactions.committed");
        Assert.Contains(snapshot, m => m.Name == "buildingblock.transactions.rolled_back");
        Assert.Contains(snapshot, m => m.Name == "buildingblock.transactions.commit_failures");
        Assert.Contains(snapshot, m => m.Name == "buildingblock.transactions.rollback_failures");
        Assert.Contains(snapshot, m => m.Name == "buildingblock.domain_events.dispatched");
        Assert.Contains(snapshot, m => m.Name == "buildingblock.domain_events.failures");
        Assert.Contains(snapshot, m => m.Name == "buildingblock.email.sent");
        Assert.Contains(snapshot, m => m.Name == "buildingblock.email.failures");
        Assert.Contains(snapshot, m => m.Name == "buildingblock.media.files.saved");
        Assert.Contains(snapshot, m => m.Name == "buildingblock.media.failures");
        Assert.Contains(snapshot, m => m.Name == "buildingblock.qrcode.generated");
        Assert.Contains(snapshot, m => m.Name == "buildingblock.qrcode.failures");
        Assert.DoesNotContain(snapshot.SelectMany(m => m.Tags), tag =>
            tag.Key.Contains("user", StringComparison.OrdinalIgnoreCase) ||
            tag.Key.Contains("email", StringComparison.OrdinalIgnoreCase) && tag.Value?.ToString()?.Contains('@') == true);
    }

    private sealed record Measurement(
        string Name,
        object Value,
        KeyValuePair<string, object?>[] Tags);
}
