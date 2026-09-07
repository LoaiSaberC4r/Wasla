using BuildingBlock.Application.Abstraction.Persistence;
using BuildingBlock.Domain.Primitive;

namespace BuildingBlock.Application.Repositories
{
    public interface ISoftDeletedWriteRepository<TEntity, TWriteMarker>
        where TEntity : class, IAggregateRoot, ISoftDeleteEntity
        where TWriteMarker : IWritePersistenceMarker
    {
        Task<TEntity?> GetDeletedTrackedByIdAsync<TKey>(
            TKey id,
            CancellationToken cancellationToken = default)
            where TKey : notnull;

        /// <summary>
        /// Marks a previously soft-deleted aggregate for deliberate physical deletion at the next
        /// unit-of-work save boundary.
        /// </summary>
        void PermanentDelete(TEntity entity)
            => throw new NotSupportedException(
                "This deleted-write repository implementation does not support permanent deletion.");
    }
}
