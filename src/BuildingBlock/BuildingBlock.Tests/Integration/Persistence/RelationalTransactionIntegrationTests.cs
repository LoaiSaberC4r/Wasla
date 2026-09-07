using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Caching;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Behaviors;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Results;
using BuildingBlock.Infrastructure.Interceptors;
using BuildingBlock.Infrastructure.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BuildingBlock.Tests.Integration.Persistence;

public sealed class RelationalTransactionIntegrationTests
{
    [Fact]
    public async Task Transaction_behavior_commits_after_handler_save_and_then_invalidates_cache()
    {
        await using var fixture = await PersistenceTestFixture.CreateAsync(TestContext.Current.CancellationToken);
        await using var context = fixture.CreateWriteContext();
        var order = new List<string>();
        var aggregateId = Guid.NewGuid();
        var provider = CreateProvider(context, order);
        var transaction = new TransactionBehavior<CacheTransactionalCommand, Result>(provider);
        var invalidation = new CommandCacheInvalidationBehavior<CacheTransactionalCommand, Result>(
            new RecordingCacheService(order),
            NullLogger<CommandCacheInvalidationBehavior<CacheTransactionalCommand, Result>>.Instance);
        var request = new CacheTransactionalCommand();

        var result = await invalidation.Handle(
            request,
            ct => transaction.Handle(
                request,
                async innerCt =>
                {
                    order.Add("Handler");
                    var unitOfWork = new EfUnitOfWork<TestWriteMarker>(fixture.CreateWriteResolver(context));
                    await unitOfWork.WriteRepository<TestAggregate>().AddAsync(
                        NewAggregate(aggregateId, "commit", "Commit"),
                        innerCt);
                    order.Add("Save Changes");
                    await unitOfWork.SaveChangesAsync(innerCt);
                    return Result.Ok();
                },
                ct),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            new[] { "Begin Transaction", "Handler", "Save Changes", "Commit", "Invalidate Cache" },
            order);
        Assert.True(await AggregateExistsAsync(fixture, aggregateId));
    }

    [Fact]
    public async Task Failed_result_rolls_back_saved_changes_and_skips_cache_invalidation()
    {
        await using var fixture = await PersistenceTestFixture.CreateAsync(TestContext.Current.CancellationToken);
        await using var context = fixture.CreateWriteContext();
        var order = new List<string>();
        var aggregateId = Guid.NewGuid();
        var provider = CreateProvider(context, order);
        var transaction = new TransactionBehavior<CacheTransactionalCommand, Result>(provider);
        var invalidation = new CommandCacheInvalidationBehavior<CacheTransactionalCommand, Result>(
            new RecordingCacheService(order),
            NullLogger<CommandCacheInvalidationBehavior<CacheTransactionalCommand, Result>>.Instance);
        var request = new CacheTransactionalCommand();

        var result = await invalidation.Handle(
            request,
            ct => transaction.Handle(
                request,
                async innerCt =>
                {
                    order.Add("Handler");
                    var unitOfWork = new EfUnitOfWork<TestWriteMarker>(fixture.CreateWriteResolver(context));
                    await unitOfWork.WriteRepository<TestAggregate>().AddAsync(
                        NewAggregate(aggregateId, "failure", "Failure"),
                        innerCt);
                    order.Add("Save Changes");
                    await unitOfWork.SaveChangesAsync(innerCt);
                    return Result.Fail(Error.Domain("Test.Failed", "failed"));
                },
                ct),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(new[] { "Begin Transaction", "Handler", "Save Changes", "Rollback" }, order);
        Assert.False(await AggregateExistsAsync(fixture, aggregateId));
    }

    [Fact]
    public async Task Exceptions_and_cancellation_roll_back_saved_changes()
    {
        await using var fixture = await PersistenceTestFixture.CreateAsync(TestContext.Current.CancellationToken);
        await using var context = fixture.CreateWriteContext();
        var exceptionOrder = new List<string>();
        var exceptionId = Guid.NewGuid();
        var transaction = new TransactionBehavior<TransactionalCommand, Result>(
            CreateProvider(context, exceptionOrder));

        await Assert.ThrowsAsync<InvalidOperationException>(() => transaction.Handle(
            new TransactionalCommand(),
            async ct =>
            {
                exceptionOrder.Add("Handler");
                var unitOfWork = new EfUnitOfWork<TestWriteMarker>(fixture.CreateWriteResolver(context));
                await unitOfWork.WriteRepository<TestAggregate>().AddAsync(
                    NewAggregate(exceptionId, "exception", "Exception"),
                    ct);
                exceptionOrder.Add("Save Changes");
                await unitOfWork.SaveChangesAsync(ct);
                throw new InvalidOperationException("handler failed");
            },
            CancellationToken.None));

        Assert.Equal(new[] { "Begin Transaction", "Handler", "Save Changes", "Rollback" }, exceptionOrder);
        Assert.False(await AggregateExistsAsync(fixture, exceptionId));

        context.ChangeTracker.Clear();
        var cancellationOrder = new List<string>();
        var cancellationId = Guid.NewGuid();
        var cancellationBehavior = new TransactionBehavior<TransactionalCommand, Result>(
            CreateProvider(context, cancellationOrder));

        await Assert.ThrowsAsync<OperationCanceledException>(() => cancellationBehavior.Handle(
            new TransactionalCommand(),
            async ct =>
            {
                cancellationOrder.Add("Handler");
                var unitOfWork = new EfUnitOfWork<TestWriteMarker>(fixture.CreateWriteResolver(context));
                await unitOfWork.WriteRepository<TestAggregate>().AddAsync(
                    NewAggregate(cancellationId, "cancellation", "Cancellation"),
                    ct);
                cancellationOrder.Add("Save Changes");
                await unitOfWork.SaveChangesAsync(ct);
                throw new OperationCanceledException(ct);
            },
            CancellationToken.None));

        Assert.Equal(new[] { "Begin Transaction", "Handler", "Save Changes", "Rollback" }, cancellationOrder);
        Assert.False(await AggregateExistsAsync(fixture, cancellationId));
    }

    [Fact]
    public async Task Domain_events_can_mutate_tracked_entities_inside_the_same_transaction()
    {
        await using var database = await SqliteTestDatabase.CreateAsync(TestContext.Current.CancellationToken);
        var dispatcher = new MutatingDomainEventDispatcher();
        await using var context = new TestWriteDbContext(database.CreateOptions<TestWriteDbContext>(builder =>
            builder.AddInterceptors(new DomainEventsInterceptor(
                dispatcher,
                NullLogger<DomainEventsInterceptor>.Instance))));
        dispatcher.Context = context;
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var order = new List<string>();
        var provider = CreateProvider(context, order);
        var transaction = new TransactionBehavior<TransactionalCommand, Result>(provider);
        var aggregateId = Guid.NewGuid();

        var result = await transaction.Handle(
            new TransactionalCommand(),
            async ct =>
            {
                order.Add("Handler");
                var aggregate = NewAggregate(aggregateId, "domain-event", "Domain Event");
                aggregate.RaiseTouchedEvent();
                var unitOfWork = new EfUnitOfWork<TestWriteMarker>(new TestWriteResolver(context));
                await unitOfWork.WriteRepository<TestAggregate>().AddAsync(aggregate, ct);
                order.Add("Save Changes");
                await unitOfWork.SaveChangesAsync(ct);
                return Result.Ok();
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(dispatcher.RemainingDomainEvents);

        context.ChangeTracker.Clear();
        var saved = await context.Aggregates.SingleAsync(entity => entity.Id == aggregateId, TestContext.Current.CancellationToken);

        Assert.True(saved.EventWasHandled);
        Assert.Equal(new[] { "Begin Transaction", "Handler", "Save Changes", "Commit" }, order);
    }

    private static ServiceProvider CreateProvider(DbContext context, IList<string> order)
        => new ServiceCollection()
            .AddSingleton<IApplicationTransactionManager<TestWriteMarker>>(
                new RecordingEfTransactionManager(context, order))
            .BuildServiceProvider();

    private static TestAggregate NewAggregate(Guid id, string externalId, string name)
        => new()
        {
            Id = id,
            ExternalId = externalId,
            Name = name
        };

    private static async Task<bool> AggregateExistsAsync(PersistenceTestFixture fixture, Guid id)
    {
        await using var context = fixture.CreateWriteContext();
        return await context.Aggregates.AnyAsync(entity => entity.Id == id);
    }

    private sealed record TransactionalCommand : ICommand, ITransactionalCommand<TestWriteMarker>;

    private sealed record CacheTransactionalCommand :
        ICommand,
        ITransactionalCommand<TestWriteMarker>,
        ICacheInvalidator
    {
        public IEnumerable<string> Tags => new[] { "test-aggregates" };
    }

    private sealed class RecordingEfTransactionManager : IApplicationTransactionManager<TestWriteMarker>
    {
        private readonly DbContext _context;
        private readonly IList<string> _order;

        public RecordingEfTransactionManager(DbContext context, IList<string> order)
        {
            _context = context;
            _order = order;
        }

        public async Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken ct = default)
        {
            _order.Add("Begin Transaction");
            var transaction = await _context.Database.BeginTransactionAsync(ct);
            return new RecordingEfTransaction(transaction, _order);
        }
    }

    private sealed class RecordingEfTransaction : IApplicationTransaction
    {
        private readonly IDbContextTransaction _transaction;
        private readonly IList<string> _order;

        public RecordingEfTransaction(IDbContextTransaction transaction, IList<string> order)
        {
            _transaction = transaction;
            _order = order;
        }

        public async Task CommitAsync(CancellationToken ct = default)
        {
            _order.Add("Commit");
            await _transaction.CommitAsync(ct);
        }

        public async Task RollbackAsync(CancellationToken ct = default)
        {
            _order.Add("Rollback");
            await _transaction.RollbackAsync(ct);
        }

        public ValueTask DisposeAsync() => _transaction.DisposeAsync();
    }

    private sealed class RecordingCacheService : ICacheService
    {
        private readonly IList<string> _order;

        public RecordingCacheService(IList<string> order)
        {
            _order = order;
        }

        public Task<(bool found, T? value)> TryGetAsync<T>(string key, CancellationToken ct = default)
            => Task.FromResult<(bool found, T? value)>((false, default));

        public Task SetAsync<T>(
            string key,
            T value,
            TimeSpan ttl,
            IEnumerable<string> tags,
            CancellationToken ct = default)
            => Task.CompletedTask;

        public Task RemoveAsync(string key, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task InvalidateByTagsAsync(IEnumerable<string> tags, CancellationToken ct = default)
        {
            _order.Add("Invalidate Cache");
            return Task.CompletedTask;
        }

        public Task ClearAllAsync(CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class MutatingDomainEventDispatcher : IDomainEventDispatcher
    {
        public TestWriteDbContext? Context { get; set; }

        public IReadOnlyCollection<IDomainEvent> RemainingDomainEvents
            => Context?.ChangeTracker
                .Entries<IHasDomainEvents>()
                .SelectMany(entry => entry.Entity.DomainEvents)
                .ToArray() ?? Array.Empty<IDomainEvent>();

        public Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
        {
            if (domainEvent is TestAggregateTouchedEvent touched)
            {
                var aggregate = Context?.ChangeTracker
                    .Entries<TestAggregate>()
                    .Select(entry => entry.Entity)
                    .SingleOrDefault(entity => entity.Id == touched.AggregateId);

                if (aggregate is not null)
                {
                    aggregate.EventWasHandled = true;
                }
            }

            return Task.CompletedTask;
        }
    }
}
