using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Abstraction.Caching;
using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Bootstrap;
using BuildingBlock.Application.Behaviors;
using BuildingBlock.Domain.Results;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlock.Tests;

public sealed class ResultAndTransactionTests
{
    [Fact]
    public void Result_invariants_are_enforced()
    {
        Assert.Throws<ArgumentException>(() => Result.Fail(Array.Empty<Error>()));

        var failure = Result<string>.Fail(Error.Domain("Domain.Error", "failed"));
        Assert.Throws<InvalidOperationException>(() => _ = failure.Value);

        var success = Result.Ok();
        Assert.True(success.Errors.IsEmpty);
    }

    [Fact]
    public async Task Normal_commands_do_not_use_manual_transactions()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var behavior = new TransactionBehavior<NormalRequest, Result>(provider);

        var result = await behavior.Handle(new NormalRequest(), _ => Task.FromResult(Result.Ok()), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Transactional_commands_use_marker_specific_transaction_manager()
    {
        var manager = new RecordingTransactionManager();
        using var provider = new ServiceCollection()
            .AddSingleton<IApplicationTransactionManager<WriteMarker>>(manager)
            .BuildServiceProvider();
        var behavior = new TransactionBehavior<TransactionalRequest, Result>(provider);

        var result = await behavior.Handle(
            new TransactionalRequest(),
            _ => Task.FromResult(Result.Ok()),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, manager.BeginCount);
        Assert.True(manager.Transaction!.Committed);
        Assert.False(manager.Transaction.RolledBack);
    }

    [Fact]
    public async Task Transactional_commands_roll_back_failed_result()
    {
        var manager = new RecordingTransactionManager();
        using var provider = new ServiceCollection()
            .AddSingleton<IApplicationTransactionManager<WriteMarker>>(manager)
            .BuildServiceProvider();
        var behavior = new TransactionBehavior<TransactionalRequest, Result>(provider);

        var result = await behavior.Handle(
            new TransactionalRequest(),
            _ => Task.FromResult(Result.Fail(Error.Domain("Domain.Failed", "failed"))),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(1, manager.BeginCount);
        Assert.False(manager.Transaction!.Committed);
        Assert.True(manager.Transaction.RolledBack);
    }

    [Fact]
    public async Task Transactional_commands_roll_back_exceptions()
    {
        var manager = new RecordingTransactionManager();
        using var provider = new ServiceCollection()
            .AddSingleton<IApplicationTransactionManager<WriteMarker>>(manager)
            .BuildServiceProvider();
        var behavior = new TransactionBehavior<TransactionalRequest, Result>(provider);

        await Assert.ThrowsAsync<InvalidOperationException>(() => behavior.Handle(
            new TransactionalRequest(),
            _ => Task.FromException<Result>(new InvalidOperationException("boom")),
            CancellationToken.None));

        Assert.Equal(1, manager.BeginCount);
        Assert.False(manager.Transaction!.Committed);
        Assert.True(manager.Transaction.RolledBack);
    }

    [Fact]
    public async Task Transactional_commands_roll_back_cancellation()
    {
        var manager = new RecordingTransactionManager();
        using var provider = new ServiceCollection()
            .AddSingleton<IApplicationTransactionManager<WriteMarker>>(manager)
            .BuildServiceProvider();
        var behavior = new TransactionBehavior<TransactionalRequest, Result>(provider);

        await Assert.ThrowsAsync<OperationCanceledException>(() => behavior.Handle(
            new TransactionalRequest(),
            _ => Task.FromException<Result>(new OperationCanceledException()),
            CancellationToken.None));

        Assert.Equal(1, manager.BeginCount);
        Assert.False(manager.Transaction!.Committed);
        Assert.True(manager.Transaction.RolledBack);
    }

    [Fact]
    public async Task Multiple_transactional_markers_throw_clear_exception()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var behavior = new TransactionBehavior<MultiMarkerRequest, Result>(provider);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => behavior.Handle(
            new MultiMarkerRequest(),
            _ => Task.FromResult(Result.Ok()),
            CancellationToken.None));

        Assert.Contains("multiple transactional markers", exception.Message);
        Assert.Contains("Use exactly one write marker", exception.Message);
    }

    [Fact]
    public async Task Transactional_request_with_non_result_response_throws_clear_exception()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var behavior = new TransactionBehavior<TransactionalTextRequest, string>(provider);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => behavior.Handle(
            new TransactionalTextRequest(),
            _ => Task.FromResult("ok"),
            CancellationToken.None));

        Assert.Contains("must return Result or Result<T>", exception.Message);
    }

    [Fact]
    public void Application_behavior_registration_produces_required_order()
    {
        var services = new ServiceCollection();
        services.AddBuildingBlockApplicationBehaviors();

        var behaviorTypes = services
            .Where(descriptor => descriptor.ServiceType.IsGenericType &&
                descriptor.ServiceType.GetGenericTypeDefinition() == typeof(IPipelineBehavior<,>))
            .Select(descriptor => descriptor.ImplementationType)
            .ToArray();

        Assert.Equal(
            new[]
            {
                typeof(TracingBehavior<,>),
                typeof(LoggingBehavior<,>),
                typeof(ExceptionMappingBehavior<,>),
                typeof(ValidationBehavior<,>),
                typeof(CommandCacheInvalidationBehavior<,>),
                typeof(TransactionBehavior<,>),
                typeof(QueryCacheBehavior<,>)
            },
            behaviorTypes);
    }

    [Fact]
    public async Task Cache_invalidation_runs_after_transaction_commit()
    {
        var order = new List<string>();
        var manager = new RecordingTransactionManager(order);
        using var provider = new ServiceCollection()
            .AddSingleton<IApplicationTransactionManager<WriteMarker>>(manager)
            .BuildServiceProvider();
        var transaction = new TransactionBehavior<CacheTransactionalRequest, Result>(provider);
        var cache = new RecordingCacheService(order);
        var invalidation = new CommandCacheInvalidationBehavior<CacheTransactionalRequest, Result>(
            cache,
            new RecordingLogger<CommandCacheInvalidationBehavior<CacheTransactionalRequest, Result>>());

        var result = await invalidation.Handle(
            new CacheTransactionalRequest(),
            _ => transaction.Handle(
                new CacheTransactionalRequest(),
                _ =>
                {
                    order.Add("Handler");
                    return Task.FromResult(Result.Ok());
                },
                CancellationToken.None),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "Begin Transaction", "Handler", "Commit", "Invalidate Cache" }, order);
    }

    [Fact]
    public async Task Failed_transactional_result_rolls_back_without_cache_invalidation()
    {
        var order = new List<string>();
        var manager = new RecordingTransactionManager(order);
        using var provider = new ServiceCollection()
            .AddSingleton<IApplicationTransactionManager<WriteMarker>>(manager)
            .BuildServiceProvider();
        var transaction = new TransactionBehavior<CacheTransactionalRequest, Result>(provider);
        var cache = new RecordingCacheService(order);
        var invalidation = new CommandCacheInvalidationBehavior<CacheTransactionalRequest, Result>(
            cache,
            new RecordingLogger<CommandCacheInvalidationBehavior<CacheTransactionalRequest, Result>>());

        var result = await invalidation.Handle(
            new CacheTransactionalRequest(),
            _ => transaction.Handle(
                new CacheTransactionalRequest(),
                _ =>
                {
                    order.Add("Handler");
                    return Task.FromResult(Result.Fail(Error.Domain("Domain.Failed", "failed")));
                },
                CancellationToken.None),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(new[] { "Begin Transaction", "Handler", "Rollback" }, order);
    }

    [Fact]
    public async Task Transactional_exception_rolls_back_without_cache_invalidation()
    {
        var order = new List<string>();
        var manager = new RecordingTransactionManager(order);
        using var provider = new ServiceCollection()
            .AddSingleton<IApplicationTransactionManager<WriteMarker>>(manager)
            .BuildServiceProvider();
        var transaction = new TransactionBehavior<CacheTransactionalRequest, Result>(provider);
        var cache = new RecordingCacheService(order);
        var invalidation = new CommandCacheInvalidationBehavior<CacheTransactionalRequest, Result>(
            cache,
            new RecordingLogger<CommandCacheInvalidationBehavior<CacheTransactionalRequest, Result>>());

        await Assert.ThrowsAsync<InvalidOperationException>(() => invalidation.Handle(
            new CacheTransactionalRequest(),
            _ => transaction.Handle(
                new CacheTransactionalRequest(),
                _ =>
                {
                    order.Add("Handler");
                    return Task.FromException<Result>(new InvalidOperationException("boom"));
                },
                CancellationToken.None),
            CancellationToken.None));

        Assert.Equal(new[] { "Begin Transaction", "Handler", "Rollback" }, order);
    }

    [Fact]
    public async Task Successful_non_transactional_command_still_invalidates_cache()
    {
        var order = new List<string>();
        using var provider = new ServiceCollection().BuildServiceProvider();
        var transaction = new TransactionBehavior<CacheNormalRequest, Result>(provider);
        var cache = new RecordingCacheService(order);
        var invalidation = new CommandCacheInvalidationBehavior<CacheNormalRequest, Result>(
            cache,
            new RecordingLogger<CommandCacheInvalidationBehavior<CacheNormalRequest, Result>>());

        var result = await invalidation.Handle(
            new CacheNormalRequest(),
            _ => transaction.Handle(
                new CacheNormalRequest(),
                _ =>
                {
                    order.Add("Handler");
                    return Task.FromResult(Result.Ok());
                },
                CancellationToken.None),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "Handler", "Invalidate Cache" }, order);
    }

    [Fact]
    public async Task Validation_failure_does_not_open_transaction_or_invalidate_cache()
    {
        var order = new List<string>();
        var manager = new RecordingTransactionManager(order);
        using var provider = new ServiceCollection()
            .AddSingleton<IApplicationTransactionManager<WriteMarker>>(manager)
            .BuildServiceProvider();
        var validation = new ValidationBehavior<CacheTransactionalRequest, Result>(
            new[] { new AlwaysInvalidValidator() });
        var transaction = new TransactionBehavior<CacheTransactionalRequest, Result>(provider);
        var cache = new RecordingCacheService(order);
        var invalidation = new CommandCacheInvalidationBehavior<CacheTransactionalRequest, Result>(
            cache,
            new RecordingLogger<CommandCacheInvalidationBehavior<CacheTransactionalRequest, Result>>());

        var result = await validation.Handle(
            new CacheTransactionalRequest(),
            _ => invalidation.Handle(
                new CacheTransactionalRequest(),
                _ => transaction.Handle(
                    new CacheTransactionalRequest(),
                    _ =>
                    {
                        order.Add("Handler");
                        return Task.FromResult(Result.Ok());
                    },
                    CancellationToken.None),
                CancellationToken.None),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(order);
    }

    [Fact]
    public async Task Cache_invalidation_exception_is_logged_and_success_result_is_preserved()
    {
        var order = new List<string>();
        var manager = new RecordingTransactionManager(order);
        using var provider = new ServiceCollection()
            .AddSingleton<IApplicationTransactionManager<WriteMarker>>(manager)
            .BuildServiceProvider();
        var transaction = new TransactionBehavior<CacheTransactionalRequest, Result>(provider);
        var cache = new RecordingCacheService(order) { ThrowOnInvalidation = true };
        var logger = new RecordingLogger<CommandCacheInvalidationBehavior<CacheTransactionalRequest, Result>>();
        var invalidation = new CommandCacheInvalidationBehavior<CacheTransactionalRequest, Result>(
            cache,
            logger);

        var result = await invalidation.Handle(
            new CacheTransactionalRequest(),
            _ => transaction.Handle(
                new CacheTransactionalRequest(),
                _ =>
                {
                    order.Add("Handler");
                    return Task.FromResult(Result.Ok());
                },
                CancellationToken.None),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "Begin Transaction", "Handler", "Commit", "Invalidate Cache" }, order);
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Error);
    }

    private sealed record NormalRequest : ICommand;

    private sealed record TransactionalRequest : ICommand, ITransactionalCommand<WriteMarker>;

    private sealed record CacheTransactionalRequest : ICommand, ITransactionalCommand<WriteMarker>, ICacheInvalidator
    {
        public IEnumerable<string> Tags => new[] { "records" };
    }

    private sealed record CacheNormalRequest : ICommand, ICacheInvalidator
    {
        public IEnumerable<string> Tags => new[] { "records" };
    }

    private sealed record MultiMarkerRequest :
        ICommand,
        ITransactionalCommand<WriteMarker>,
        ITransactionalCommand<OtherWriteMarker>;

    private sealed record TransactionalTextRequest : IRequest<string>, ITransactionalCommand<WriteMarker>;

    private sealed class WriteMarker : IWritePersistenceMarker
    {
    }

    private sealed class OtherWriteMarker : IWritePersistenceMarker
    {
    }

    private sealed class RecordingTransactionManager : IApplicationTransactionManager<WriteMarker>
    {
        private readonly IList<string>? _order;

        public RecordingTransactionManager(IList<string>? order = null)
        {
            _order = order;
        }

        public int BeginCount { get; private set; }

        public RecordingTransaction? Transaction { get; private set; }

        public Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken ct = default)
        {
            _order?.Add("Begin Transaction");
            BeginCount++;
            Transaction = new RecordingTransaction(_order);
            return Task.FromResult<IApplicationTransaction>(Transaction);
        }
    }

    private sealed class RecordingTransaction : IApplicationTransaction
    {
        private readonly IList<string>? _order;

        public RecordingTransaction(IList<string>? order = null)
        {
            _order = order;
        }

        public bool Committed { get; private set; }

        public bool RolledBack { get; private set; }

        public Task CommitAsync(CancellationToken ct = default)
        {
            _order?.Add("Commit");
            Committed = true;
            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken ct = default)
        {
            _order?.Add("Rollback");
            RolledBack = true;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
            => ValueTask.CompletedTask;
    }

    private sealed class RecordingCacheService : ICacheService
    {
        private readonly IList<string> _order;

        public RecordingCacheService(IList<string> order)
        {
            _order = order;
        }

        public bool ThrowOnInvalidation { get; set; }

        public Task<(bool found, T? value)> TryGetAsync<T>(string key, CancellationToken ct = default)
            => Task.FromResult<(bool found, T? value)>((false, default));

        public Task SetAsync<T>(string key, T value, TimeSpan ttl, IEnumerable<string> tags, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task RemoveAsync(string key, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task InvalidateByTagsAsync(IEnumerable<string> tags, CancellationToken ct = default)
        {
            _order.Add("Invalidate Cache");

            if (ThrowOnInvalidation)
            {
                throw new InvalidOperationException("cache failed");
            }

            return Task.CompletedTask;
        }

        public Task ClearAllAsync(CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class AlwaysInvalidValidator : AbstractValidator<CacheTransactionalRequest>
    {
        public AlwaysInvalidValidator()
        {
            RuleFor(request => request).Must(_ => false).WithMessage("invalid");
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        private readonly List<(LogLevel Level, Exception? Exception)> _entries = new();

        public IReadOnlyList<(LogLevel Level, Exception? Exception)> Entries => _entries;

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _entries.Add((logLevel, exception));
        }
    }
}
