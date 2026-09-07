using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Behaviors;
using BuildingBlock.Application.Exceptions;
using BuildingBlock.Domain.Results;
using BuildingBlock.Infrastructure.Repositories;
using BuildingBlock.Tests.Integration.Persistence;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlock.Tests;

public sealed class Phase3ExceptionAndTransactionHardeningTests
{
    [Fact]
    public void Default_mapper_accepts_validation_only_and_rejects_broad_system_exceptions()
    {
        var mapper = new DefaultExceptionToErrorMapper();
        Assert.True(mapper.TryMap(new ValidationException("invalid"), out var validation));
        Assert.Equal(ErrorType.Validation, validation.Type);

        Assert.False(mapper.TryMap(new ArgumentException("programming defect"), out _));
        Assert.False(mapper.TryMap(new KeyNotFoundException("unexpected state"), out _));
        Assert.False(mapper.TryMap(new UnauthorizedAccessException("security defect"), out _));
        Assert.False(mapper.TryMap(new HttpRequestException("unwrapped provider failure"), out _));
        Assert.False(mapper.TryMap(new InvalidOperationException("unknown"), out _));
    }

    [Fact]
    public async Task Unknown_exceptions_and_cancellation_reach_the_global_handler()
    {
        var behavior = new ExceptionMappingBehavior<MappingRequest, Result>(
            new[] { new MatchAllMapper() });
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var cancellationException = new OperationCanceledException(cancellation.Token);
        var observedCancellation = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            behavior.Handle(
                new MappingRequest(),
                _ => Task.FromException<Result>(cancellationException),
                cancellation.Token));
        Assert.Same(cancellationException, observedCancellation);

        var unknownBehavior = new ExceptionMappingBehavior<MappingRequest, Result>(
            new[] { new DefaultExceptionToErrorMapper() });
        var unknown = new InvalidOperationException("unknown");
        var observedUnknown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            unknownBehavior.Handle(
                new MappingRequest(),
                _ => Task.FromException<Result>(unknown),
                CancellationToken.None));
        Assert.Same(unknown, observedUnknown);
    }

    [Fact]
    public async Task Mapper_order_is_deterministic_and_first_match_wins()
    {
        var behavior = new ExceptionMappingBehavior<MappingRequest, Result>(
            new IExceptionToErrorMapper[]
            {
                new FixedMapper(Error.Domain("First", "first")),
                new FixedMapper(Error.Domain("Second", "second"))
            });

        var result = await behavior.Handle(
            new MappingRequest(),
            _ => Task.FromException<Result>(new KnownTestException()),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("First", Assert.Single(result.Errors).Code);
    }

    [Fact]
    public async Task Cancellation_rolls_back_with_a_fresh_cleanup_token_and_disposes_transaction()
    {
        var transaction = new RecordingTransaction();
        var behavior = CreateTransactionBehavior(transaction);
        using var cancellation = new CancellationTokenSource();

        var observed = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            behavior.Handle(
                new TransactionRequest(),
                _ =>
                {
                    cancellation.Cancel();
                    return Task.FromCanceled<Result>(cancellation.Token);
                },
                cancellation.Token));

        Assert.True(observed.CancellationToken.IsCancellationRequested);
        Assert.Single(transaction.RollbackTokens);
        Assert.False(transaction.RollbackTokens[0].IsCancellationRequested);
        Assert.True(transaction.Disposed);
    }

    [Fact]
    public async Task Commit_failure_rolls_back_rethrows_original_and_never_returns_success()
    {
        var commitFailure = new IOException("provider commit detail");
        var transaction = new RecordingTransaction { CommitFailure = commitFailure };
        var behavior = CreateTransactionBehavior(transaction);

        var observed = await Assert.ThrowsAsync<IOException>(() =>
            behavior.Handle(
                new TransactionRequest(),
                _ => Task.FromResult(Result.Ok()),
                CancellationToken.None));

        Assert.Same(commitFailure, observed);
        Assert.Equal(1, transaction.CommitCount);
        Assert.Single(transaction.RollbackTokens);
        Assert.True(transaction.Disposed);
    }

    [Fact]
    public async Task Rollback_failure_after_failed_result_throws_safe_unexpected_failure()
    {
        var transaction = new RecordingTransaction
        {
            RollbackFailure = new IOException("secret provider rollback detail")
        };
        var behavior = CreateTransactionBehavior(transaction);

        var observed = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            behavior.Handle(
                new TransactionRequest(),
                _ => Task.FromResult(Result.Fail(Error.Domain("Expected", "expected"))),
                CancellationToken.None));

        Assert.Contains("outcome is unknown", observed.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", observed.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Null(observed.InnerException);
        Assert.True(transaction.Disposed);
    }

    [Fact]
    public async Task Rollback_failure_does_not_hide_original_handler_exception()
    {
        var original = new KnownTestException();
        var transaction = new RecordingTransaction
        {
            RollbackFailure = new IOException("secondary rollback detail")
        };
        var behavior = CreateTransactionBehavior(transaction);

        var observed = await Assert.ThrowsAsync<KnownTestException>(() =>
            behavior.Handle(
                new TransactionRequest(),
                _ => Task.FromException<Result>(original),
                CancellationToken.None));

        Assert.Same(original, observed);
        Assert.True(transaction.Disposed);
    }

    [Fact]
    public async Task Ef_transaction_manager_rejects_an_existing_transaction()
    {
        await using var fixture = await PersistenceTestFixture.CreateAsync(TestContext.Current.CancellationToken);
        await using var context = fixture.CreateWriteContext();
        await using var existing = await context.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var manager = new EfApplicationTransactionManager<TestWriteMarker>(
            fixture.CreateWriteResolver(context));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.BeginTransactionAsync(TestContext.Current.CancellationToken));

        Assert.Contains("already active", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static TransactionBehavior<TransactionRequest, Result> CreateTransactionBehavior(
        RecordingTransaction transaction)
    {
        var provider = new ServiceCollection()
            .AddSingleton<IApplicationTransactionManager<TransactionMarker>>(
                new RecordingTransactionManager(transaction))
            .BuildServiceProvider();
        return new TransactionBehavior<TransactionRequest, Result>(provider);
    }

    private sealed record MappingRequest : IRequest<Result>;

    private sealed record TransactionRequest : IRequest<Result>, ITransactionalCommand<TransactionMarker>;

    private sealed class TransactionMarker : IWritePersistenceMarker
    {
    }

    private sealed class KnownTestException : Exception
    {
    }

    private sealed class MatchAllMapper : IExceptionToErrorMapper
    {
        public bool TryMap(Exception exception, out Error error)
        {
            error = Error.Domain("Mapped", "mapped");
            return true;
        }
    }

    private sealed class FixedMapper : IExceptionToErrorMapper
    {
        private readonly Error _error;

        public FixedMapper(Error error)
        {
            _error = error;
        }

        public bool TryMap(Exception exception, out Error error)
        {
            error = _error;
            return exception is KnownTestException;
        }
    }

    private sealed class RecordingTransactionManager : IApplicationTransactionManager<TransactionMarker>
    {
        private readonly RecordingTransaction _transaction;

        public RecordingTransactionManager(RecordingTransaction transaction)
        {
            _transaction = transaction;
        }

        public Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken ct = default)
            => Task.FromResult<IApplicationTransaction>(_transaction);
    }

    private sealed class RecordingTransaction : IApplicationTransaction
    {
        public Exception? CommitFailure { get; init; }
        public Exception? RollbackFailure { get; init; }
        public int CommitCount { get; private set; }
        public List<CancellationToken> RollbackTokens { get; } = new();
        public bool Disposed { get; private set; }

        public Task CommitAsync(CancellationToken ct = default)
        {
            CommitCount++;
            return CommitFailure is null
                ? Task.CompletedTask
                : Task.FromException(CommitFailure);
        }

        public Task RollbackAsync(CancellationToken ct = default)
        {
            RollbackTokens.Add(ct);
            return RollbackFailure is null
                ? Task.CompletedTask
                : Task.FromException(RollbackFailure);
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
