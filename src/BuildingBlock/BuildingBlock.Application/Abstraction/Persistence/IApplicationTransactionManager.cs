namespace BuildingBlock.Application.Abstraction.Persistence
{
    public interface IApplicationTransactionManager<TWriteMarker>
        where TWriteMarker : IWritePersistenceMarker
    {
        Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken ct = default);
    }
}
