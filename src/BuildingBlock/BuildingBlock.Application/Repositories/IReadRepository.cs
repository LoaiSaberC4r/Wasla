using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Domain.Specification;
using System.Linq.Expressions;

namespace BuildingBlock.Application.Repositories
{
    public interface IReadRepository<TEntity, TReadMarker>
        where TEntity : class
        where TReadMarker : IReadPersistenceMarker
    {
        Task<TEntity?> GetByIdAsync<TKey>(
            TKey id,
            CancellationToken cancellationToken = default)
            where TKey : notnull;

        Task<TEntity?> GetByPropertyAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken = default);

        Task<bool> AnyAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken = default);

        Task<long> LongCountAsync(
            Expression<Func<TEntity, bool>>? predicate = null,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<TEntity>> ListAsync(
            Specification<TEntity> specification,
            CancellationToken cancellationToken = default);

        Task<TEntity?> FirstOrDefaultAsync(
            Specification<TEntity> specification,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<TOut>> ListAsync<TOut>(
            Specification<TEntity, TOut> specification,
            CancellationToken cancellationToken = default);

        Task<TOut?> FirstOrDefaultAsync<TOut>(
            Specification<TEntity, TOut> specification,
            CancellationToken cancellationToken = default);

        Task<(IReadOnlyList<TEntity> Items, long TotalCount)> ListWithLongCountAsync(
            Specification<TEntity> specification,
            CancellationToken cancellationToken = default);

        Task<(IReadOnlyList<TOut> Items, long TotalCount)> ListWithLongCountAsync<TOut>(
            Specification<TEntity, TOut> specification,
            CancellationToken cancellationToken = default);
    }
}
