using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Specification;
using BuildingBlock.Infrastructure.Persistence;
using BuildingBlock.Infrastructure.SpecificationEvaluator;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlock.Infrastructure.Repositories
{
    internal sealed class EfSoftDeletedReadRepository<TEntity, TReadMarker> : ISoftDeletedReadRepository<TEntity, TReadMarker>
        where TEntity : class, ISoftDeleteEntity
        where TReadMarker : IReadPersistenceMarker
    {
        private readonly DbContext _context;
        private readonly DbSet<TEntity> _set;

        public EfSoftDeletedReadRepository(IEfDbContextResolver<TReadMarker> resolver)
        {
            ArgumentNullException.ThrowIfNull(resolver);

            _context = resolver.Resolve();
            _set = _context.Set<TEntity>();
        }

        public async Task<TEntity?> GetDeletedByIdAsync<TKey>(
            TKey id,
            CancellationToken cancellationToken = default)
            where TKey : notnull
        {
            var predicate = EfPrimaryKeyExpressionBuilder.BuildSingleKeyPredicate<TEntity, TKey>(_context, id);
            return await _set
                .IgnoreQueryFilters([BuildingBlockQueryFilterNames.SoftDelete])
                .AsNoTracking()
                .Where(entity => entity.IsDeleted)
                .FirstOrDefaultAsync(predicate, cancellationToken);
        }

        public async Task<IReadOnlyList<TEntity>> ListDeletedAsync(
            Specification<TEntity> specification,
            CancellationToken cancellationToken = default)
        {
            var query = SpecificationEvaluator<TEntity>.BuildQuery(
                _set
                    .IgnoreQueryFilters([BuildingBlockQueryFilterNames.SoftDelete])
                    .Where(entity => entity.IsDeleted),
                specification,
                forceNoTracking: true);
            return await query.ToListAsync(cancellationToken);
        }
    }
}
