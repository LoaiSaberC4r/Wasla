namespace BuildingBlock.Application.Abstraction.Persistence
{
    public interface IReadModelUnitOfWork<TReadMarker>
        where TReadMarker : IReadPersistenceMarker
    {
        IReadModelWriter<TEntity, TReadMarker> Writer<TEntity>()
            where TEntity : class;

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
