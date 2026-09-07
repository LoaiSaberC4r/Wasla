using BuildingBlock.Domain.Primitive;

namespace BuildingBlock.Application.Abstraction
{
    /// <summary>
    /// Dispatches domain events inside the current unit of work.
    /// </summary>
    /// <remarks>
    /// Phase 1 domain event handlers are intended only for in-process business reactions that participate in the same
    /// persistence operation, such as updating tracked entities, coordinating invariants, or raising additional domain
    /// events. Handlers must not perform irreversible external side effects such as sending email or SMS, calling
    /// external APIs, publishing broker messages, writing files, charging payments, or updating an external database.
    /// If a save operation fails, previously dispatched event instances remain on the aggregate and may be dispatched
    /// again on retry. Durable external side effects should be implemented through the future Outbox integration.
    /// </remarks>
    public interface IDomainEventDispatcher
    {
        Task DispatchAsync(IDomainEvent domainEvent, CancellationToken ct);
    }
}
