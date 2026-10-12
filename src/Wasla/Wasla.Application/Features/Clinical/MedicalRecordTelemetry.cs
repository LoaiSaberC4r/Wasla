using System.Diagnostics.Metrics;

namespace Wasla.Application.Features.Clinical;

public static class MedicalRecordTelemetry
{
    // Process-lifetime instruments. Labels are fixed operation/mode/status enums, never identifiers or clinical text.
    private static readonly Meter Meter = new("Wasla.MedicalRecord", "16");
    private static readonly Counter<long> Denials = Meter.CreateCounter<long>("medical_record.denied");
    private static readonly Counter<long> AuditFailures = Meter.CreateCounter<long>("medical_record.audit_failures");
    private static readonly Counter<long> Reads = Meter.CreateCounter<long>("medical_record.reads");
    private static readonly Counter<long> Requests = Meter.CreateCounter<long>("medical_record.request_operations");
    private static readonly Histogram<double> Latency = Meter.CreateHistogram<double>("medical_record.read_duration", "ms");
    private static readonly Histogram<int> PageSizes = Meter.CreateHistogram<int>("medical_record.page_size");
    public static void Denied(MedicalRecordMode mode) => Denials.Add(1, new KeyValuePair<string, object?>("mode", mode.ToString()));
    public static void AuditFailed() => AuditFailures.Add(1);
    public static void Read(MedicalRecordMode mode, double milliseconds, int pageSize)
    {
        var tag = new KeyValuePair<string, object?>("mode", mode.ToString());
        Reads.Add(1, tag); Latency.Record(milliseconds, tag); PageSizes.Record(pageSize, tag);
    }
    public static void Request(string operation, string status) => Requests.Add(1,
        new KeyValuePair<string, object?>("operation", operation), new KeyValuePair<string, object?>("status", status));
}
