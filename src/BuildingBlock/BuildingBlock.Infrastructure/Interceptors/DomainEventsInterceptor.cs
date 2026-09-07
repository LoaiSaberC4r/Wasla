using BuildingBlock.Application.Abstraction;
using BuildingBlock.Domain.Primitive;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace BuildingBlock.Infrastructure.Interceptors
{
    internal sealed class DomainEventsInterceptor : SaveChangesInterceptor
    {
        private const int MaxDispatchPasses = 32;

        private static readonly Action<ILogger, string, Exception?> DispatchingDomainEvent =
            LoggerMessage.Define<string>(
                LogLevel.Debug,
                new EventId(2200, nameof(DispatchingDomainEvent)),
                "Dispatching domain event {DomainEventType}");

        private readonly IDomainEventDispatcher _dispatcher;
        private readonly ILogger<DomainEventsInterceptor> _logger;
        private readonly ConditionalWeakTable<DbContext, DispatchState> _states = new();

        public DomainEventsInterceptor(
            IDomainEventDispatcher dispatcher,
            ILogger<DomainEventsInterceptor> logger)
        {
            _dispatcher = dispatcher;
            _logger = logger;
        }

        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            if (HasDomainEvents(eventData.Context))
            {
                throw new InvalidOperationException(
                    "Synchronous SaveChanges() cannot dispatch domain events. Use SaveChangesAsync() when tracked aggregates contain domain events.");
            }

            return result;
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            await DispatchDomainEventsAsync(eventData.Context, cancellationToken);
            return result;
        }

        public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
        {
            RemoveDispatchedEvents(eventData.Context);
            return result;
        }

        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            RemoveDispatchedEvents(eventData.Context);
            return ValueTask.FromResult(result);
        }

        public override void SaveChangesFailed(DbContextErrorEventData eventData)
        {
            ClearDispatchState(eventData.Context);
        }

        public override Task SaveChangesFailedAsync(
            DbContextErrorEventData eventData,
            CancellationToken cancellationToken = default)
        {
            ClearDispatchState(eventData.Context);
            return Task.CompletedTask;
        }

        private async Task DispatchDomainEventsAsync(DbContext? context, CancellationToken ct)
        {
            if (context is null)
            {
                return;
            }

            if (_states.TryGetValue(context, out var activeState) && activeState.IsDispatching)
            {
                throw new InvalidOperationException(
                    "Recursive SaveChangesAsync() on the same DbContext is not allowed while domain events are being dispatched.");
            }

            if (!HasDomainEvents(context))
            {
                return;
            }

            var state = _states.GetOrCreateValue(context);
            state.ResetAttempt();

            try
            {
                state.IsDispatching = true;

                for (var pass = 0; pass < MaxDispatchPasses; pass++)
                {
                    ct.ThrowIfCancellationRequested();

                    var eventBatches = GetUndispatchedEventBatches(context, state);
                    if (eventBatches.Count == 0)
                    {
                        return;
                    }

                    foreach (var batch in eventBatches)
                    {
                        foreach (var domainEvent in batch.Events)
                        {
                            if (_logger.IsEnabled(LogLevel.Debug))
                            {
                                DispatchingDomainEvent(_logger, domainEvent.GetType().Name, null);
                            }

                            await _dispatcher.DispatchAsync(domainEvent, ct);
                            state.MarkDispatched(batch.Entity, domainEvent);
                        }
                    }
                }

                throw new InvalidOperationException(
                    $"Domain event dispatch exceeded {MaxDispatchPasses} passes. This usually indicates an infinite domain event loop.");
            }
            catch
            {
                state.ResetAttempt();
                throw;
            }
            finally
            {
                state.IsDispatching = false;
            }
        }

        private static bool HasDomainEvents(DbContext? context)
            => context is not null &&
               context.ChangeTracker
                   .Entries<IHasDomainEvents>()
                   .Any(entry => entry.Entity.DomainEvents.Count > 0);

        private static List<EventBatch> GetUndispatchedEventBatches(DbContext context, DispatchState state)
            => context.ChangeTracker
                .Entries<IHasDomainEvents>()
                .Select(entry => entry.Entity)
                .Select(entity => new EventBatch(
                    entity,
                    entity.DomainEvents
                        .Where(domainEvent => !state.WasDispatched(domainEvent))
                        .ToArray()))
                .Where(batch => batch.Events.Count > 0)
                .ToList();

        private void RemoveDispatchedEvents(DbContext? context)
        {
            if (context is null || !_states.TryGetValue(context, out var state))
            {
                return;
            }

            foreach (var (entity, events) in state.DispatchedEventsByEntity)
            {
                entity.RemoveDomainEvents(events);
            }

            state.ResetAttempt();
        }

        private void ClearDispatchState(DbContext? context)
        {
            if (context is null || !_states.TryGetValue(context, out var state))
            {
                return;
            }

            state.ResetAttempt();
        }

        private sealed record EventBatch(IHasDomainEvents Entity, IReadOnlyList<IDomainEvent> Events);

        private sealed class DispatchState
        {
            private readonly Dictionary<IHasDomainEvents, List<IDomainEvent>> _dispatchedEventsByEntity =
                new(ReferenceEqualityComparer.Instance);
            private readonly HashSet<IDomainEvent> _dispatchedEvents =
                new(ReferenceEqualityComparer.Instance);

            public bool IsDispatching { get; set; }

            public IReadOnlyDictionary<IHasDomainEvents, List<IDomainEvent>> DispatchedEventsByEntity
                => _dispatchedEventsByEntity;

            public bool WasDispatched(IDomainEvent domainEvent)
                => _dispatchedEvents.Contains(domainEvent);

            public void MarkDispatched(IHasDomainEvents entity, IDomainEvent domainEvent)
            {
                if (!_dispatchedEvents.Add(domainEvent))
                {
                    return;
                }

                if (!_dispatchedEventsByEntity.TryGetValue(entity, out var events))
                {
                    events = new List<IDomainEvent>();
                    _dispatchedEventsByEntity.Add(entity, events);
                }

                events.Add(domainEvent);
            }

            public void ResetAttempt()
            {
                _dispatchedEvents.Clear();
                _dispatchedEventsByEntity.Clear();
            }
        }
    }
}
