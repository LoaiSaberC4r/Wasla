using BuildingBlock.Application.Time;
using BuildingBlock.Domain.Primitive;
using BuildingBlock.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BuildingBlock.Infrastructure.Interceptors
{
    internal sealed class SoftDeleteEntitiesInterceptor : SaveChangesInterceptor
    {
        private readonly IDateTimeProvider _clock;

        public SoftDeleteEntitiesInterceptor(IDateTimeProvider clock)
        {
            _clock = clock;
        }

        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            ApplySoftDelete(eventData.Context);
            return result;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ApplySoftDelete(eventData.Context);
            return ValueTask.FromResult(result);
        }

        public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
        {
            PermanentDeleteTracker.Clear(eventData.Context);
            return result;
        }

        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            PermanentDeleteTracker.Clear(eventData.Context);
            return ValueTask.FromResult(result);
        }

        private void ApplySoftDelete(DbContext? dbContext)
        {
            if (dbContext is null)
            {
                return;
            }

            var now = _clock.UtcNow;

            foreach (var entry in dbContext.ChangeTracker.Entries<ISoftDeleteEntity>()
                         .Where(entry =>
                             entry.State == EntityState.Deleted &&
                             !PermanentDeleteTracker.IsMarked(dbContext, entry.Entity)))
            {
                ConvertPhysicalDelete(entry, now);
            }

            foreach (var entry in dbContext.ChangeTracker.Entries<ISoftDeleteEntity>()
                         .Where(e => e.State == EntityState.Modified && e.Property(x => x.IsDeleted).IsModified))
            {
                ApplySoftDeleteToggle(entry, now);
            }
        }

        private static void ConvertPhysicalDelete(EntityEntry<ISoftDeleteEntity> entry, DateTime now)
        {
            var wasDeleted = entry.Property(entity => entity.IsDeleted).OriginalValue;

            entry.State = EntityState.Unchanged;
            entry.Property(entity => entity.IsDeleted).CurrentValue = true;

            if (!wasDeleted)
            {
                ApplyDeleteTransition(entry, now);
            }
        }

        private static void ApplySoftDeleteToggle(EntityEntry<ISoftDeleteEntity> entry, DateTime now)
        {
            var isDeletedProperty = entry.Property(entity => entity.IsDeleted);
            var wasDeleted = isDeletedProperty.OriginalValue;
            var isDeleted = isDeletedProperty.CurrentValue;

            if (wasDeleted == isDeleted)
            {
                return;
            }

            if (isDeleted)
            {
                ApplyDeleteTransition(entry, now);
                return;
            }

            ApplyRestoreTransition(entry, now);
        }

        private static void ApplyDeleteTransition(EntityEntry<ISoftDeleteEntity> entry, DateTime now)
        {
            entry.Property(entity => entity.IsDeleted).CurrentValue = true;
            entry.Property(entity => entity.IsDeleted).IsModified = true;
            entry.Property(entity => entity.DeletedOnUtc).CurrentValue = now;
            entry.Property(entity => entity.DeletedOnUtc).IsModified = true;
            entry.Property(entity => entity.RestoredOnUtc).CurrentValue = null;
            entry.Property(entity => entity.RestoredOnUtc).IsModified = true;
        }

        private static void ApplyRestoreTransition(EntityEntry<ISoftDeleteEntity> entry, DateTime now)
        {
            entry.Property(entity => entity.IsDeleted).CurrentValue = false;
            entry.Property(entity => entity.IsDeleted).IsModified = true;
            entry.Property(entity => entity.RestoredOnUtc).CurrentValue = now;
            entry.Property(entity => entity.RestoredOnUtc).IsModified = true;
            entry.Property(entity => entity.DeletedOnUtc).IsModified = false;
        }
    }
}
