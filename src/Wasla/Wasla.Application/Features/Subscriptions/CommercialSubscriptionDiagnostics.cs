using System.Diagnostics.Metrics;

namespace Wasla.Application.Features.Subscriptions;

internal static class CommercialSubscriptionDiagnostics
{
    private static readonly Meter Meter = new("Wasla.CommercialSubscriptions", "1.2");
    private static readonly Counter<long> Denials = Meter.CreateCounter<long>("wasla.commercial.denials");
    private static readonly Counter<long> Continuations = Meter.CreateCounter<long>("wasla.commercial.continuations");
    private static readonly Counter<long> CapacityConflicts = Meter.CreateCounter<long>("wasla.commercial.capacity_conflicts");
    internal static void Denied(string feature, string reason) => Denials.Add(1, new("feature", feature), new("reason", reason));
    internal static void Continuity(string feature, bool allowed) => Continuations.Add(1, new("feature", feature), new("allowed", allowed));
    internal static void Capacity(string kind) => CapacityConflicts.Add(1, new KeyValuePair<string, object?>("kind", kind));
}
