using Microsoft.EntityFrameworkCore;

namespace BuildingBlock.Infrastructure.Repositories
{
    internal interface IEfDbContextResolver<in TMarker>
    {
        DbContext Resolve();
    }

    internal sealed class EfDbContextResolver<TMarker, TContext> : IEfDbContextResolver<TMarker>
        where TContext : DbContext
    {
        private readonly TContext _context;

        public EfDbContextResolver(TContext context)
        {
            _context = context;
        }

        public DbContext Resolve() => _context;
    }
}
