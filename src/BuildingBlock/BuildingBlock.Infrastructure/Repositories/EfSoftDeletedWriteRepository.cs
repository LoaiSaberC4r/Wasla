using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlock.Infrastructure.Repositories
{
    internal sealed class EfSoftDeletedWriteRepository<TEntity, TWriteMarker> : ISoftDeletedWriteRepository<TEntity, TWriteMarker>
        where TEntity : class, IAggregateRoot, ISoftDeleteEntity
        where TWriteMarker : IWritePersistenceMarker
    {
        private readonly DbContext _context;
        private readonly DbSet<TEntity> _set;

        public EfSoftDeletedWriteRepository(IEfDbContextResolver<TWriteMarker> resolver)
        {
            ArgumentNullException.ThrowIfNull(resolver);

            _context = resolver.Resolve();
            _set = _context.Set<TEntity>();
        }

        public async Task<TEntity?> GetDeletedTrackedByIdAsync<TKey>(
            TKey id,
            CancellationToken cancellationToken = default)
            where TKey : notnull
        {
            var predicate = EfPrimaryKeyExpressionBuilder.BuildSingleKeyPredicate<TEntity, TKey>(_context, id);
            return await _set
                .IgnoreQueryFilters([BuildingBlockQueryFilterNames.SoftDelete])
                .AsTracking()
                .Where(entity => entity.IsDeleted)
                .FirstOrDefaultAsync(predicate, cancellationToken);
        }

        public void PermanentDelete(TEntity entity)
        {
            ArgumentNullException.ThrowIfNull(entity);

            if (!entity.IsDeleted || _context.Entry(entity).State == EntityState.Detached)
            {
                throw new InvalidOperationException(
                    "Only an entity already loaded as soft deleted can be permanently deleted.");
            }

            PermanentDeleteTracker.Mark(_context, entity);
            _set.Remove(entity);
        }
    }
}
