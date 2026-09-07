using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Application.Repositories;
using BuildingBlock.Domain.Primitive;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlock.Infrastructure.Repositories
{
    internal sealed class EfUnitOfWork<TWriteMarker> : IUnitOfWork<TWriteMarker>
        where TWriteMarker : IWritePersistenceMarker
    {
        private readonly DbContext _context;
        private readonly Dictionary<Type, object> _repositories = new();

        public EfUnitOfWork(IEfDbContextResolver<TWriteMarker> resolver)
            => _context = resolver.Resolve();

        public IWriteRepository<TEntity, TWriteMarker> WriteRepository<TEntity>()
            where TEntity : class, IAggregateRoot
        {
            var type = typeof(TEntity);
            if (_repositories.TryGetValue(type, out var repo))
            {
                return (IWriteRepository<TEntity, TWriteMarker>)repo;
            }

            var instance = new EfWriteRepository<TEntity, TWriteMarker>(new InlineResolver<TWriteMarker>(_context));
            _repositories[type] = instance;
            return instance;
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => _context.SaveChangesAsync(cancellationToken);

        public async Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (_context.Database.CurrentTransaction is not null)
            {
                throw new InvalidOperationException("A transaction is already active for this DbContext.");
            }

            var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            return new EfApplicationTransaction(transaction);
        }

        private sealed class InlineResolver<TM> : IEfDbContextResolver<TM>
        {
            private readonly DbContext _ctx;

            public InlineResolver(DbContext ctx) => _ctx = ctx;

            public DbContext Resolve() => _ctx;
        }
    }
}
