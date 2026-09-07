using System.Linq.Expressions;

namespace BuildingBlock.Application.Abstraction.Persistence
{
    public interface IReadModelWriter<TEntity, TReadMarker>
        where TEntity : class
        where TReadMarker : IReadPersistenceMarker
    {
        Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);

        Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

        void Update(TEntity entity);

        void UpdateRange(IEnumerable<TEntity> entities);

        void Remove(TEntity entity);

        void RemoveRange(IEnumerable<TEntity> entities);

        Task<TEntity?> FindByIdAsync<TKey>(
            TKey id,
            CancellationToken cancellationToken = default)
            where TKey : notnull;

        Task<bool> AnyAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken = default);
    }
}
