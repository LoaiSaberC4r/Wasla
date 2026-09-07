using BuildingBlock.Application.Abstraction;
using BuildingBlock.Application.Diagnostics;
using BuildingBlock.Domain.Primitive;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace BuildingBlock.Infrastructure.DomainEvents
{
    internal sealed class DomainEventDispatcher : IDomainEventDispatcher
    {
        private static readonly ConcurrentDictionary<Type, HandlerDispatch> Dispatchers = new();

        private readonly IServiceProvider _serviceProvider;

        public DomainEventDispatcher(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        internal static int DispatchDelegateCount => Dispatchers.Count;

        public async Task DispatchAsync(IDomainEvent domainEvent, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(domainEvent);

            try
            {
                var dispatch = Dispatchers.GetOrAdd(domainEvent.GetType(), BuildDispatch);
                var handlers = _serviceProvider.GetServices(dispatch.HandlerType);

                foreach (var handler in handlers)
                {
                    if (handler is null)
                    {
                        continue;
                    }

                    ct.ThrowIfCancellationRequested();

                    var task = dispatch.Invoke(handler, domainEvent, ct);
                    if (task is null)
                    {
                        throw new InvalidOperationException($"Domain event handler {handler.GetType().FullName} returned null.");
                    }

                    await task;
                }

                BuildingBlockDiagnostics.RecordDomainEventDispatched();
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                BuildingBlockDiagnostics.RecordDomainEventFailure(exception.GetType().Name);
                throw;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                BuildingBlockDiagnostics.RecordDomainEventFailure("Cancelled");
                throw;
            }
        }

        private static HandlerDispatch BuildDispatch(Type domainEventType)
        {
            var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEventType);
            var handleMethod = handlerType.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.Handle))
                ?? throw new InvalidOperationException($"Handler type {handlerType} does not expose Handle.");

            var handlerParameter = Expression.Parameter(typeof(object), "handler");
            var domainEventParameter = Expression.Parameter(typeof(IDomainEvent), "domainEvent");
            var cancellationTokenParameter = Expression.Parameter(typeof(CancellationToken), "ct");

            var call = Expression.Call(
                Expression.Convert(handlerParameter, handlerType),
                handleMethod,
                Expression.Convert(domainEventParameter, domainEventType),
                cancellationTokenParameter);

            var dispatcher = Expression
                .Lambda<Func<object, IDomainEvent, CancellationToken, Task>>(
                    call,
                    handlerParameter,
                    domainEventParameter,
                    cancellationTokenParameter)
                .Compile();

            return new HandlerDispatch(handlerType, dispatcher);
        }

        private sealed record HandlerDispatch(
            Type HandlerType,
            Func<object, IDomainEvent, CancellationToken, Task> Invoke);
    }
}
