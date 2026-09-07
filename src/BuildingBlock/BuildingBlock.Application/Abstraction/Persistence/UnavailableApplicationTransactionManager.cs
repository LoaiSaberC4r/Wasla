namespace BuildingBlock.Application.Abstraction.Persistence
{
    internal sealed class UnavailableApplicationTransactionManager<TWriteMarker> : IApplicationTransactionManager<TWriteMarker>
        where TWriteMarker : IWritePersistenceMarker
    {
        public Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken ct = default)
            => throw new InvalidOperationException(
                "No application transaction manager has been registered for this write marker.");
    }
}
