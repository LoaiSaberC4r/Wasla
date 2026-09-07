using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Domain.Primitive;

namespace BuildingBlock.Application.Repositories
{
    public interface IUnitOfWork<TWriteMarker>
        where TWriteMarker : IWritePersistenceMarker
    {
        IWriteRepository<TEntity, TWriteMarker> WriteRepository<TEntity>()
            where TEntity : class, IAggregateRoot;

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

        Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    }
}
