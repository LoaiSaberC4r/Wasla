using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Domain.Specification;
using BuildingBlock.Infrastructure.SpecificationEvaluator;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace BuildingBlock.Infrastructure.Repositories
{
    internal sealed class EfWriteRepository<TEntity, TWriteMarker> : IWriteRepository<TEntity, TWriteMarker>
        where TEntity : class, IAggregateRoot
        where TWriteMarker : IWritePersistenceMarker
    {
        private readonly DbContext _context;
        private readonly DbSet<TEntity> _set;

        public EfWriteRepository(IEfDbContextResolver<TWriteMarker> resolver)
        {
            _context = resolver.Resolve();
            _set = _context.Set<TEntity>();
        }

        public Task<TEntity?> GetByIdAsync<TKey>(TKey id, CancellationToken cancellationToken = default)
            where TKey : notnull
        {
            var predicate = EfPrimaryKeyExpressionBuilder.BuildSingleKeyPredicate<TEntity, TKey>(_context, id);
            return _set.AsTracking().FirstOrDefaultAsync(predicate, cancellationToken);
        }

        public Task<TEntity?> GetByPropertyAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken = default)
            => _set.AsTracking().FirstOrDefaultAsync(predicate, cancellationToken);

        public Task<TEntity?> FirstOrDefaultAsync(
            Specification<TEntity> specification,
            CancellationToken cancellationToken = default)
        {
            var query = SpecificationEvaluator<TEntity>.BuildQuery(_set.AsQueryable(), specification, forceTracking: true);
            return query.FirstOrDefaultAsync(cancellationToken);
        }

        public Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
            => _set.AddAsync(entity, cancellationToken).AsTask();

        public Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
            => _set.AddRangeAsync(entities, cancellationToken);

        public void Update(TEntity entity) => _set.Update(entity);

        public void UpdateRange(IEnumerable<TEntity> entities) => _set.UpdateRange(entities);

        public void Delete(TEntity entity) => _set.Remove(entity);

        public void DeleteRange(IEnumerable<TEntity> entities) => _set.RemoveRange(entities);
    }
}
