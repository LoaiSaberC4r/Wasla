using BuildingBlock.Application.Abstraction.Caching;
using BuildingBlock.Application.Diagnostics;
using BuildingBlock.Application.Abstraction.Persistence;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace BuildingBlock.Application.Behaviors
{
    internal sealed class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private static readonly TimeSpan RollbackTimeout = TimeSpan.FromSeconds(30);
        private static readonly Action<ILogger, string, Exception?> RollbackCleanupFailed =
            LoggerMessage.Define<string>(
                LogLevel.Error,
                new EventId(1300, nameof(RollbackCleanupFailed)),
                "Transaction rollback cleanup failed with {ExceptionType}.");

        private static readonly ConcurrentDictionary<Type, Type?> TransactionMarkerCache = new();
        private static readonly ConcurrentDictionary<Type, ITransactionStarter> TransactionStarters = new();
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TransactionBehavior<TRequest, TResponse>>? _logger;

        public TransactionBehavior(
            IServiceProvider serviceProvider,
            ILogger<TransactionBehavior<TRequest, TResponse>>? logger = null)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            var markerType = TransactionMarkerCache.GetOrAdd(typeof(TRequest), ResolveTransactionMarker);

            if (markerType is null)
            {
                return await next(cancellationToken);
            }

            if (!ResultInspector.IsResultType(typeof(TResponse)))
            {
                throw new InvalidOperationException(
                    $"Transactional request {typeof(TRequest).Name} must return Result or Result<T>.");
            }

            var starter = TransactionStarters.GetOrAdd(markerType, CreateTransactionStarter);
            await using var transaction = await starter.BeginAsync(_serviceProvider, cancellationToken);
            BuildingBlockDiagnostics.RecordTransactionStarted();

            TResponse response;
            try
            {
                response = await next(cancellationToken);
            }
            catch (Exception exception)
            {
                await RollbackPreservingOriginalExceptionAsync(transaction, exception);
                throw;
            }

            if (!ResultInspector.IsSuccess(response))
            {
                try
                {
                    await RollbackForCleanupAsync(transaction);
                    BuildingBlockDiagnostics.RecordTransactionRolledBack();
                    return response;
                }
                catch (Exception exception)
                {
                    BuildingBlockDiagnostics.RecordTransactionRollbackFailed();
                    LogRollbackFailure(exception);
                    throw new InvalidOperationException(
                        "Transaction rollback failed after a failed command result; the transaction outcome is unknown.");
                }
            }

            try
            {
                await transaction.CommitAsync(cancellationToken);
                BuildingBlockDiagnostics.RecordTransactionCommitted();
                return response;
            }
            catch (Exception exception)
            {
                BuildingBlockDiagnostics.RecordTransactionCommitFailed();
                await RollbackPreservingOriginalExceptionAsync(transaction, exception);
                throw;
            }
        }

        private async Task RollbackPreservingOriginalExceptionAsync(
            IApplicationTransaction transaction,
            Exception originalException)
        {
            try
            {
                await RollbackForCleanupAsync(transaction);
                BuildingBlockDiagnostics.RecordTransactionRolledBack();
            }
            catch (Exception rollbackException)
            {
                BuildingBlockDiagnostics.RecordTransactionRollbackFailed();
                LogRollbackFailure(rollbackException);

                // The original handler/commit exception remains the observable failure. The rollback
                // failure is recorded and logged using type-only metadata so provider details do not leak.
                _ = originalException;
            }
        }

        private static async Task RollbackForCleanupAsync(IApplicationTransaction transaction)
        {
            using var cleanupTimeout = new CancellationTokenSource(RollbackTimeout);
            await transaction.RollbackAsync(cleanupTimeout.Token);
        }

        private void LogRollbackFailure(Exception exception)
        {
            if (_logger?.IsEnabled(LogLevel.Error) == true)
            {
                RollbackCleanupFailed(_logger, exception.GetType().Name, null);
            }
        }

        private static Type? ResolveTransactionMarker(Type requestType)
        {
            var transactionalInterfaces = requestType
                .GetInterfaces()
                .Where(type =>
                    type.IsGenericType &&
                    type.GetGenericTypeDefinition() == typeof(ITransactionalCommand<>))
                .ToArray();

            if (transactionalInterfaces.Length == 0)
            {
                return null;
            }

            if (transactionalInterfaces.Length > 1)
            {
                throw new InvalidOperationException(
                    $"Request {requestType.Name} implements multiple transactional markers. Use exactly one write marker.");
            }

            return transactionalInterfaces[0].GetGenericArguments()[0];
        }

        private static ITransactionStarter CreateTransactionStarter(Type markerType)
        {
            if (!typeof(IWritePersistenceMarker).IsAssignableFrom(markerType))
            {
                throw new InvalidOperationException(
                    $"Transaction marker {markerType.Name} must implement {nameof(IWritePersistenceMarker)}.");
            }

            return (ITransactionStarter)Activator.CreateInstance(
                typeof(TransactionStarter<>).MakeGenericType(markerType))!;
        }

    }

    internal interface ITransactionStarter
    {
        Task<IApplicationTransaction> BeginAsync(IServiceProvider provider, CancellationToken cancellationToken);
    }

    internal sealed class TransactionStarter<TWriteMarker> : ITransactionStarter
        where TWriteMarker : IWritePersistenceMarker
    {
        public Task<IApplicationTransaction> BeginAsync(IServiceProvider provider, CancellationToken cancellationToken)
            => provider.GetRequiredService<IApplicationTransactionManager<TWriteMarker>>()
                .BeginTransactionAsync(cancellationToken);
    }
}
