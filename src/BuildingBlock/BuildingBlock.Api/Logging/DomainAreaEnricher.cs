using Serilog.Core;
using Serilog.Events;

namespace BuildingBlock.Api.Logging
{
    internal sealed class DomainAreaEnricher : ILogEventEnricher
    {
        private readonly string _area;

        public DomainAreaEnricher(string area) => _area = area;

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
            => logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("domainArea", _area));
    }
}
