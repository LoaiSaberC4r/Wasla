using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Domain.Primitive;

namespace BuildingBlock.Tests.Integration.Persistence;

internal sealed class TestReadMarker : IReadPersistenceMarker
{
}

internal sealed class TestWriteMarker : IWritePersistenceMarker
{
}

internal sealed class OtherTestReadMarker : IReadPersistenceMarker
{
}

internal sealed class OtherTestWriteMarker : IWritePersistenceMarker
{
}

internal sealed class TestAggregate : IAggregateRoot, ISoftDeleteEntity, IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public Guid Id { get; set; }

    public string ExternalId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsVisible { get; set; } = true;

    public int Version { get; set; }

    public bool EventWasHandled { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedOnUtc { get; set; }

    public DateTime? RestoredOnUtc { get; set; }

    public List<TestChild> Children { get; set; } = new();

    public TestProfile? Profile { get; set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void RaiseTouchedEvent()
        => _domainEvents.Add(new TestAggregateTouchedEvent(Id));

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

internal sealed class TestChild
{
    public Guid Id { get; set; }

    public Guid TestAggregateId { get; set; }

    public string Label { get; set; } = string.Empty;

    public TestAggregate? Aggregate { get; set; }
}

internal sealed class TestProfile
{
    public Guid Id { get; set; }

    public Guid TestAggregateId { get; set; }

    public string Summary { get; set; } = string.Empty;

    public TestAggregate? Aggregate { get; set; }

    public TestProfileDetail? Detail { get; set; }
}

internal sealed class TestProfileDetail
{
    public Guid Id { get; set; }

    public Guid TestProfileId { get; set; }

    public string Notes { get; set; } = string.Empty;

    public TestProfile? Profile { get; set; }
}

internal sealed class TestCompositeAggregate : IAggregateRoot
{
    public string Partition { get; set; } = string.Empty;

    public int LocalId { get; set; }

    public string Name { get; set; } = string.Empty;
}

internal sealed class TestReadModel
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsVisible { get; set; } = true;
}

internal sealed class OtherTestReadModel
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

internal sealed class TestCompositeReadModel
{
    public string Partition { get; set; } = string.Empty;

    public int LocalId { get; set; }

    public string Name { get; set; } = string.Empty;
}

internal sealed record TestAggregateTouchedEvent(Guid AggregateId) : IDomainEvent;
