using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Primitive;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BuildingBlock.Infrastructure.Interceptors
{
    internal sealed class AuditableEntitiesInterceptor : SaveChangesInterceptor
    {
        private static readonly HashSet<string> IgnoredProperties =
            new(StringComparer.Ordinal)
            {
                nameof(IAuditableEntity.CreatedOnUtc),
                nameof(IAuditableEntity.ModifiedOnUtc)
            };

        private readonly IDateTimeProvider _clock;

        public AuditableEntitiesInterceptor(IDateTimeProvider clock)
        {
            _clock = clock;
        }

        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            ApplyAudit(eventData.Context);
            return result;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ApplyAudit(eventData.Context);
            return ValueTask.FromResult(result);
        }

        private void ApplyAudit(DbContext? dbContext)
        {
            if (dbContext is null)
            {
                return;
            }

            var now = _clock.UtcNow;
            var entries = dbContext.ChangeTracker
                .Entries<IAuditableEntity>()
                .Where(entry => entry.State is EntityState.Added or EntityState.Modified);

            foreach (var entry in entries)
            {
                if (entry.State == EntityState.Added)
                {
                    if (entry.Property(entity => entity.CreatedOnUtc).CurrentValue == default)
                    {
                        entry.Property(entity => entity.CreatedOnUtc).CurrentValue = now;
                    }

                    entry.Property(entity => entity.ModifiedOnUtc).IsModified = false;
                    continue;
                }

                if (!HasMeaningfulChanges(entry))
                {
                    entry.Property(entity => entity.CreatedOnUtc).IsModified = false;
                    entry.Property(entity => entity.ModifiedOnUtc).IsModified = false;

                    if (!entry.Properties.Any(property => property.IsModified))
                    {
                        entry.State = EntityState.Unchanged;
                    }

                    continue;
                }

                entry.Property(entity => entity.ModifiedOnUtc).CurrentValue = now;
                entry.Property(entity => entity.CreatedOnUtc).IsModified = false;
            }
        }

        private static bool HasMeaningfulChanges(EntityEntry<IAuditableEntity> entry)
            => entry.Properties.Any(property => property.IsModified && !IgnoredProperties.Contains(property.Metadata.Name));
    }
}
