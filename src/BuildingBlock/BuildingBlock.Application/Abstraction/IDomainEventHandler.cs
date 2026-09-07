using BuildingBlock.Domain.Primitive;

namespace BuildingBlock.Application.Abstraction
{
    /// <summary>
    /// Handles a domain event inside the same unit of work that raised it.
    /// </summary>
    /// <remarks>
    /// Phase 1 handlers are restricted to in-process business reactions that remain within the current persistence
    /// operation, such as mutating tracked entities, coordinating invariants, or raising additional domain events.
    /// Do not perform irreversible external side effects from these handlers, including email, SMS, external HTTP calls,
    /// message broker publication, file writes, payment-provider calls, or writes to external databases. If persistence
    /// fails and is retried, the same event instance can be dispatched again before a later successful save removes it.
    /// Use the future Outbox integration for durable external side effects.
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Naming",
        "CA1711:Identifiers should not have incorrect suffix",
        Justification = "IDomainEventHandler is an established public contract and renaming it would be a breaking change.")]
    public interface IDomainEventHandler<in TEvent>
        where TEvent : IDomainEvent
    {
        Task Handle(TEvent domainEvent, CancellationToken ct);
    }
}
