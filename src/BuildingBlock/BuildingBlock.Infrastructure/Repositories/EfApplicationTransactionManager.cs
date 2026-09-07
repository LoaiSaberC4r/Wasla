using BuildingBlock.Application.Abstraction.Persistence;

namespace BuildingBlock.Infrastructure.Repositories
{
    internal sealed class EfApplicationTransactionManager<TWriteMarker> : IApplicationTransactionManager<TWriteMarker>
        where TWriteMarker : IWritePersistenceMarker
    {
        private readonly IEfDbContextResolver<TWriteMarker> _resolver;

        public EfApplicationTransactionManager(IEfDbContextResolver<TWriteMarker> resolver)
        {
            _resolver = resolver;
        }

        public async Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken ct = default)
        {
            var context = _resolver.Resolve();
            if (context.Database.CurrentTransaction is not null)
            {
                throw new InvalidOperationException("A transaction is already active for this DbContext.");
            }

            var transaction = await context.Database.BeginTransactionAsync(ct);
            return new EfApplicationTransaction(transaction);
        }
    }
}
