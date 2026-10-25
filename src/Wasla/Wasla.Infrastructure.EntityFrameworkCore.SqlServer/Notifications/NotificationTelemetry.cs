using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Wasla.Domain.Notifications;

namespace Wasla.Infrastructure.EntityFrameworkCore.SqlServer.Notifications;

internal static partial class NotificationTelemetry
{
    private static readonly Meter Meter = new("Wasla.Notifications.Lifecycle", "1.0");
    private static readonly Counter<long> Events = Meter.CreateCounter<long>("notification_events_total");
    private static readonly Histogram<double> Lag = Meter.CreateHistogram<double>("notification_dispatch_lag_seconds", "s");
    internal static void Record(ILogger? logger, string operation, string eventType, NotificationChannel? channel, string status, string reason = "None")
    {
        // All call sites supply code-controlled operation/reason codes. No identities or payloads enter telemetry.
        if (!NotificationPolicy.KnownEvent(eventType)) eventType = "Unavailable";
        var channelName = channel?.ToString() ?? "None";
        TagList tags = new() { { "operation", operation }, { "event_type", eventType }, { "channel", channelName }, { "status", status }, { "reason_code", reason } };
        Events.Add(1, tags);
        if (logger is not null) Lifecycle(logger, operation, eventType, channelName, status, reason);
    }
    internal static void DispatchLag(TimeSpan lag) => Lag.Record(Math.Max(0, lag.TotalSeconds));
    [LoggerMessage(EventId = 4702, Level = LogLevel.Information, Message = "{Operation} event={EventType} channel={Channel} status={Status} reason={ReasonCode}.")]
    private static partial void Lifecycle(ILogger logger, string operation, string eventType, string channel, string status, string reasonCode);
}
