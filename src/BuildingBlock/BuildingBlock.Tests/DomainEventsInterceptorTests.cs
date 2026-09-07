using BuildingBlock.Application.Abstraction;
using BuildingBlock.Domain.EntitiesHelper;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Infrastructure.Interceptors;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace BuildingBlock.Tests;

public sealed class DomainEventsInterceptorTests
{
    [Fact]
    public async Task Successful_save_dispatches_and_removes_events()
    {
        var dispatcher = new RecordingDispatcher();
        using var connection = CreateOpenConnection();
        await using var db = CreateContext(connection, dispatcher);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var entity = new DomainEntity(1);
        entity.RaiseFirst();
        db.Entities.Add(entity);

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Single(dispatcher.Dispatched);
        Assert.Empty(entity.DomainEvents);
    }

    [Fact]
    public async Task Save_failure_preserves_dispatched_events()
    {
        var dispatcher = new RecordingDispatcher();
        var failingSave = new FailNextSaveInterceptor();
        using var connection = CreateOpenConnection();
        await using var db = CreateContext(connection, dispatcher, failingSave);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var entity = new DomainEntity(1);
        entity.RaiseFirst();
        db.Entities.Add(entity);
        failingSave.FailNext = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.Single(dispatcher.Dispatched);
        Assert.Single(entity.DomainEvents);
    }

    [Fact]
    public async Task Handler_failure_preserves_events()
    {
        var dispatcher = new RecordingDispatcher
        {
            OnDispatch = (_, _) => throw new InvalidOperationException("handler failed")
        };
        using var connection = CreateOpenConnection();
        await using var db = CreateContext(connection, dispatcher);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var entity = new DomainEntity(1);
        entity.RaiseFirst();
        db.Entities.Add(entity);

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.Single(entity.DomainEvents);
    }

    [Fact]
    public async Task Nested_events_are_dispatched_once_and_removed_after_success()
    {
        var dispatcher = new RecordingDispatcher
        {
            OnDispatch = (domainEvent, _) =>
            {
                if (domainEvent is FirstEvent firstEvent)
                {
                    firstEvent.Entity.RaiseSecond();
                }

                return Task.CompletedTask;
            }
        };
        using var connection = CreateOpenConnection();
        await using var db = CreateContext(connection, dispatcher);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var entity = new DomainEntity(1);
        entity.RaiseFirst();
        db.Entities.Add(entity);

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Collection(
            dispatcher.Dispatched,
            domainEvent => Assert.IsType<FirstEvent>(domainEvent),
            domainEvent => Assert.IsType<SecondEvent>(domainEvent));
        Assert.Empty(entity.DomainEvents);
    }

    [Fact]
    public async Task Infinite_event_loop_throws_and_preserves_pending_events()
    {
        var dispatcher = new RecordingDispatcher
        {
            OnDispatch = (domainEvent, _) =>
            {
                if (domainEvent is LoopEvent loopEvent)
                {
                    loopEvent.Entity.RaiseLoop();
                }

                return Task.CompletedTask;
            }
        };
        using var connection = CreateOpenConnection();
        await using var db = CreateContext(connection, dispatcher);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var entity = new DomainEntity(1);
        entity.RaiseLoop();
        db.Entities.Add(entity);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.Contains("exceeded 32 passes", exception.Message);
        Assert.NotEmpty(entity.DomainEvents);
    }

    [Fact]
    public async Task Failed_save_can_be_retried_and_dispatches_event_again_before_removal()
    {
        var dispatcher = new RecordingDispatcher();
        var failingSave = new FailNextSaveInterceptor();
        using var connection = CreateOpenConnection();
        await using var db = CreateContext(connection, dispatcher, failingSave);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var entity = new DomainEntity(1);
        entity.RaiseFirst();
        db.Entities.Add(entity);
        failingSave.FailNext = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, dispatcher.Dispatched.Count);
        Assert.Empty(entity.DomainEvents);
    }

    [Fact]
    public async Task Concurrent_dbcontexts_do_not_share_dispatching_state()
    {
        var dispatcher = new RecordingDispatcher();
        var interceptor = new DomainEventsInterceptor(
            dispatcher,
            NullLogger<DomainEventsInterceptor>.Instance);
        var aStarted = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseA = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

        dispatcher.OnDispatch = async (domainEvent, _) =>
        {
            if (domainEvent is NamedEvent { Name: "A" })
            {
                aStarted.SetResult(null);
                await releaseA.Task;
            }
        };

        using var connectionA = CreateOpenConnection();
        using var connectionB = CreateOpenConnection();
        await using var contextA = CreateContext(connectionA, dispatcher, interceptor: interceptor);
        await using var contextB = CreateContext(connectionB, dispatcher, interceptor: interceptor);
        await contextA.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        await contextB.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var entityA = new DomainEntity(1);
        var entityB = new DomainEntity(1);
        entityA.RaiseNamed("A");
        entityB.RaiseNamed("B");
        contextA.Entities.Add(entityA);
        contextB.Entities.Add(entityB);

        var saveA = contextA.SaveChangesAsync(TestContext.Current.CancellationToken);
        await aStarted.Task;

        await contextB.SaveChangesAsync(TestContext.Current.CancellationToken);
        releaseA.SetResult(null);
        await saveA;

        Assert.Contains(dispatcher.Dispatched, domainEvent => domainEvent is NamedEvent { Name: "A" });
        Assert.Contains(dispatcher.Dispatched, domainEvent => domainEvent is NamedEvent { Name: "B" });
        Assert.Empty(entityA.DomainEvents);
        Assert.Empty(entityB.DomainEvents);
    }

    [Fact]
    public void Synchronous_save_with_domain_events_throws_clear_exception()
    {
        var dispatcher = new RecordingDispatcher();
        using var connection = CreateOpenConnection();
        using var db = CreateContext(connection, dispatcher);
        db.Database.EnsureCreated();

        var entity = new DomainEntity(1);
        entity.RaiseFirst();
        db.Entities.Add(entity);

        var exception = Assert.Throws<InvalidOperationException>(() => db.SaveChanges());

        Assert.Contains("Use SaveChangesAsync", exception.Message);
        Assert.Empty(dispatcher.Dispatched);
        Assert.Single(entity.DomainEvents);
    }

    [Fact]
    public async Task Recursive_save_from_domain_event_handler_is_rejected_and_event_is_retained()
    {
        using var connection = CreateOpenConnection();
        var dispatcher = new RecordingDispatcher();
        await using var db = CreateContext(connection, dispatcher);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var entity = new DomainEntity(1);
        entity.RaiseFirst();
        db.Entities.Add(entity);
        dispatcher.OnDispatch = (_, cancellationToken) => db.SaveChangesAsync(cancellationToken);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            db.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.Contains("Recursive SaveChangesAsync", exception.Message, StringComparison.Ordinal);
        Assert.Single(entity.DomainEvents);
        Assert.Single(dispatcher.Dispatched);
    }

    private static DomainEventsDbContext CreateContext(
        SqliteConnection connection,
        IDomainEventDispatcher dispatcher,
        SaveChangesInterceptor? saveInterceptor = null,
        DomainEventsInterceptor? interceptor = null)
    {
        interceptor ??= new DomainEventsInterceptor(
            dispatcher,
            NullLogger<DomainEventsInterceptor>.Instance);

        var builder = new DbContextOptionsBuilder<DomainEventsDbContext>()
            .UseSqlite(connection);

        if (saveInterceptor is null)
        {
            builder.AddInterceptors(interceptor);
        }
        else
        {
            builder.AddInterceptors(interceptor, saveInterceptor);
        }

        return new DomainEventsDbContext(builder.Options);
    }

    private static SqliteConnection CreateOpenConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    private sealed class DomainEventsDbContext : DbContext
    {
        public DomainEventsDbContext(DbContextOptions<DomainEventsDbContext> options)
            : base(options)
        {
        }

        public DbSet<DomainEntity> Entities => Set<DomainEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DomainEntity>().HasKey(entity => entity.Id);
            modelBuilder.Entity<DomainEntity>().Ignore(entity => entity.DomainEvents);
        }
    }

    private sealed class DomainEntity : AggregateRoot<int>
    {
        private DomainEntity()
        {
        }

        public DomainEntity(int id)
            : base(id)
        {
        }

        public void RaiseFirst() => RaiseDomainEvent(new FirstEvent(this));

        public void RaiseSecond() => RaiseDomainEvent(new SecondEvent(this));

        public void RaiseLoop() => RaiseDomainEvent(new LoopEvent(this));

        public void RaiseNamed(string name) => RaiseDomainEvent(new NamedEvent(name));
    }

    private sealed record FirstEvent(DomainEntity Entity) : IDomainEvent;

    private sealed record SecondEvent(DomainEntity Entity) : IDomainEvent;

    private sealed record LoopEvent(DomainEntity Entity) : IDomainEvent;

    private sealed record NamedEvent(string Name) : IDomainEvent;

    private sealed class RecordingDispatcher : IDomainEventDispatcher
    {
        private readonly object _gate = new();
        private readonly List<IDomainEvent> _dispatched = new();

        public Func<IDomainEvent, CancellationToken, Task>? OnDispatch { get; set; }

        public IReadOnlyList<IDomainEvent> Dispatched
        {
            get
            {
                lock (_gate)
                {
                    return _dispatched.ToArray();
                }
            }
        }

        public async Task DispatchAsync(IDomainEvent domainEvent, CancellationToken ct)
        {
            lock (_gate)
            {
                _dispatched.Add(domainEvent);
            }

            if (OnDispatch is not null)
            {
                await OnDispatch(domainEvent, ct);
            }
        }
    }

    private sealed class FailNextSaveInterceptor : SaveChangesInterceptor
    {
        public bool FailNext { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (FailNext)
            {
                FailNext = false;
                throw new InvalidOperationException("save failed");
            }

            return ValueTask.FromResult(result);
        }
    }
}
