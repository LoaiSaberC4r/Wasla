namespace BuildingBlock.Domain.Primitive
{
    public interface IHasDomainEvents
    {
        IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

        void RemoveDomainEvents(IReadOnlyCollection<IDomainEvent> domainEvents);

        void ClearDomainEvents();
    }
}
