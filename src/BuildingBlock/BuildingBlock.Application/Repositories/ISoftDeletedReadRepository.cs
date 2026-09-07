using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Specification;

namespace BuildingBlock.Application.Repositories
{
    public interface ISoftDeletedReadRepository<TEntity, TReadMarker>
        where TEntity : class, ISoftDeleteEntity
        where TReadMarker : IReadPersistenceMarker
    {
        Task<TEntity?> GetDeletedByIdAsync<TKey>(
            TKey id,
            CancellationToken cancellationToken = default)
            where TKey : notnull;

        Task<IReadOnlyList<TEntity>> ListDeletedAsync(
            Specification<TEntity> specification,
            CancellationToken cancellationToken = default);
    }
}
