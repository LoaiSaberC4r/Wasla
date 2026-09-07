using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Specification;
using System.Linq.Expressions;

namespace BuildingBlock.Application.Repositories
{
    public interface IWriteRepository<TEntity, TWriteMarker>
        where TEntity : class, IAggregateRoot
        where TWriteMarker : IWritePersistenceMarker
    {
        Task<TEntity?> GetByIdAsync<TKey>(
            TKey id,
            CancellationToken cancellationToken = default)
            where TKey : notnull;

        Task<TEntity?> GetByPropertyAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken = default);

        Task<TEntity?> FirstOrDefaultAsync(
            Specification<TEntity> specification,
            CancellationToken cancellationToken = default);

        Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);

        Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

        void Update(TEntity entity);

        void UpdateRange(IEnumerable<TEntity> entities);

        void Delete(TEntity entity);

        void DeleteRange(IEnumerable<TEntity> entities);
    }
}
