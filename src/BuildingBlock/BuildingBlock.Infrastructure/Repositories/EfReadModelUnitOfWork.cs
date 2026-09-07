using BuildingBlock.Application.Abstraction.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlock.Infrastructure.Repositories
{
    internal sealed class EfReadModelUnitOfWork<TReadMarker> : IReadModelUnitOfWork<TReadMarker>
        where TReadMarker : IReadPersistenceMarker
    {
        private readonly DbContext _context;
        private readonly Dictionary<Type, object> _writers = new();

        public EfReadModelUnitOfWork(IEfDbContextResolver<TReadMarker> resolver)
            => _context = resolver.Resolve();

        public IReadModelWriter<TEntity, TReadMarker> Writer<TEntity>()
            where TEntity : class
        {
            var type = typeof(TEntity);
            if (_writers.TryGetValue(type, out var writer))
            {
                return (IReadModelWriter<TEntity, TReadMarker>)writer;
            }

            var instance = new EfReadModelWriter<TEntity, TReadMarker>(_context);
            _writers[type] = instance;
            return instance;
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => _context.SaveChangesAsync(cancellationToken);
    }
}
