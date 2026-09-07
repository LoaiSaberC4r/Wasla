using BuildingBlock.Domain.Primitive;

namespace BuildingBlock.Domain.EntitiesHelper
{
    public class AggregateRoot<TKey> : Entity<TKey>, IAggregateRoot, IHasDomainEvents
    {
        private readonly List<IDomainEvent> _domainEvents = new();

        protected AggregateRoot(TKey id)
            : base(id) { }

        protected AggregateRoot()
        { }

        protected void RaiseDomainEvent(IDomainEvent domainEvent)
        {
            _domainEvents.Add(domainEvent);
        }

        public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

        public void RemoveDomainEvents(IReadOnlyCollection<IDomainEvent> domainEvents)
        {
            ArgumentNullException.ThrowIfNull(domainEvents);

            foreach (var domainEvent in domainEvents)
            {
                _domainEvents.Remove(domainEvent);
            }
        }

        public void ClearDomainEvents() => _domainEvents.Clear();
    }
}
