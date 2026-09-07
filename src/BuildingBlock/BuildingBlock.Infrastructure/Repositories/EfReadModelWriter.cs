using BuildingBlock.Application.Abstraction.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace BuildingBlock.Infrastructure.Repositories
{
    internal sealed class EfReadModelWriter<TEntity, TReadMarker> : IReadModelWriter<TEntity, TReadMarker>
        where TEntity : class
        where TReadMarker : IReadPersistenceMarker
    {
        private readonly DbContext _context;
        private readonly DbSet<TEntity> _set;

        public EfReadModelWriter(IEfDbContextResolver<TReadMarker> resolver)
        {
            _context = resolver.Resolve();
            _set = _context.Set<TEntity>();
        }

        internal EfReadModelWriter(DbContext context)
        {
            _context = context;
            _set = _context.Set<TEntity>();
        }

        public Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
            => _set.AddAsync(entity, cancellationToken).AsTask();

        public Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
            => _set.AddRangeAsync(entities, cancellationToken);

        public void Update(TEntity entity) => _set.Update(entity);

        public void UpdateRange(IEnumerable<TEntity> entities) => _set.UpdateRange(entities);

        public void Remove(TEntity entity) => _set.Remove(entity);

        public void RemoveRange(IEnumerable<TEntity> entities) => _set.RemoveRange(entities);

        public Task<TEntity?> FindByIdAsync<TKey>(
            TKey id,
            CancellationToken cancellationToken = default)
            where TKey : notnull
        {
            var predicate = EfPrimaryKeyExpressionBuilder.BuildSingleKeyPredicate<TEntity, TKey>(_context, id);
            return _set.FirstOrDefaultAsync(predicate, cancellationToken);
        }

        public Task<bool> AnyAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken = default)
            => _set.AnyAsync(predicate, cancellationToken);
    }
}
