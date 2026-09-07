using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Specification;
using BuildingBlock.Infrastructure.SpecificationEvaluator;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace BuildingBlock.Infrastructure.Repositories
{
    internal sealed class EfReadRepository<TEntity, TReadMarker> : IReadRepository<TEntity, TReadMarker>
        where TEntity : class
        where TReadMarker : IReadPersistenceMarker
    {
        private readonly DbContext _context;
        private readonly DbSet<TEntity> _set;

        public EfReadRepository(IEfDbContextResolver<TReadMarker> resolver)
        {
            _context = resolver.Resolve();
            _set = _context.Set<TEntity>();
        }

        public Task<TEntity?> GetByIdAsync<TKey>(TKey id, CancellationToken cancellationToken = default)
            where TKey : notnull
        {
            var predicate = EfPrimaryKeyExpressionBuilder.BuildSingleKeyPredicate<TEntity, TKey>(_context, id);
            return _set.AsNoTracking().FirstOrDefaultAsync(predicate, cancellationToken);
        }

        public Task<TEntity?> GetByPropertyAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken = default)
            => _set.AsNoTracking().FirstOrDefaultAsync(predicate, cancellationToken);

        public Task<bool> AnyAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken = default)
            => _set.AsNoTracking().AnyAsync(predicate, cancellationToken);

        public Task<long> LongCountAsync(
            Expression<Func<TEntity, bool>>? predicate = null,
            CancellationToken cancellationToken = default)
            => predicate is null
                ? _set.AsNoTracking().LongCountAsync(cancellationToken)
                : _set.AsNoTracking().LongCountAsync(predicate, cancellationToken);

        public async Task<IReadOnlyList<TEntity>> ListAsync(
            Specification<TEntity> specification,
            CancellationToken cancellationToken = default)
        {
            var query = SpecificationEvaluator<TEntity>.BuildQuery(
                _set.AsQueryable(),
                specification,
                forceNoTracking: true);
            return await query.ToListAsync(cancellationToken);
        }

        public async Task<TEntity?> FirstOrDefaultAsync(
            Specification<TEntity> specification,
            CancellationToken cancellationToken = default)
        {
            var query = SpecificationEvaluator<TEntity>.BuildQuery(
                _set.AsQueryable(),
                specification,
                forceNoTracking: true);
            return await query.FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<TOut>> ListAsync<TOut>(
            Specification<TEntity, TOut> specification,
            CancellationToken cancellationToken = default)
        {
            var query = SpecificationEvaluator<TEntity>.BuildQuery(
                _set.AsQueryable(),
                specification,
                forceNoTracking: true);
            return await query.ToListAsync(cancellationToken);
        }

        public async Task<TOut?> FirstOrDefaultAsync<TOut>(
            Specification<TEntity, TOut> specification,
            CancellationToken cancellationToken = default)
        {
            var query = SpecificationEvaluator<TEntity>.BuildQuery(
                _set.AsQueryable(),
                specification,
                forceNoTracking: true);
            return await query.FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<(IReadOnlyList<TEntity> Items, long TotalCount)> ListWithLongCountAsync(
            Specification<TEntity> specification,
            CancellationToken cancellationToken = default)
        {
            var baseQuery = _set.AsQueryable();

            var totalCount = 0L;
            if (specification.IsTotalCountEnabled)
            {
                var countQuery = SpecificationEvaluator<TEntity>.BuildCountQuery(baseQuery, specification);
                totalCount = await countQuery.LongCountAsync(cancellationToken);
            }

            var dataQuery = SpecificationEvaluator<TEntity>.BuildQuery(
                baseQuery,
                specification,
                forceNoTracking: true);
            var items = await dataQuery.ToListAsync(cancellationToken);

            return (items, totalCount);
        }

        public async Task<(IReadOnlyList<TOut> Items, long TotalCount)> ListWithLongCountAsync<TOut>(
            Specification<TEntity, TOut> specification,
            CancellationToken cancellationToken = default)
        {
            var baseQuery = _set.AsQueryable();

            var totalCount = 0L;
            if (specification.IsTotalCountEnabled)
            {
                var countQuery = SpecificationEvaluator<TEntity>.BuildCountQuery(baseQuery, specification);
                totalCount = await countQuery.LongCountAsync(cancellationToken);
            }

            var dataQuery = SpecificationEvaluator<TEntity>.BuildQuery(
                baseQuery,
                specification,
                forceNoTracking: true);
            var items = await dataQuery.ToListAsync(cancellationToken);

            return (items, totalCount);
        }
    }
}
