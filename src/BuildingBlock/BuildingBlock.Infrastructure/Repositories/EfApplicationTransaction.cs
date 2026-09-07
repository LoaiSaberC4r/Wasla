using BuildingBlock.Application.Abstraction.Persistence;
using Microsoft.EntityFrameworkCore.Storage;

namespace BuildingBlock.Infrastructure.Repositories
{
    internal sealed class EfApplicationTransaction : IApplicationTransaction
    {
        private readonly IDbContextTransaction _transaction;

        public EfApplicationTransaction(IDbContextTransaction transaction)
        {
            _transaction = transaction;
        }

        public Task CommitAsync(CancellationToken ct = default)
            => _transaction.CommitAsync(ct);

        public Task RollbackAsync(CancellationToken ct = default)
            => _transaction.RollbackAsync(ct);

        public ValueTask DisposeAsync()
            => _transaction.DisposeAsync();
    }
}
