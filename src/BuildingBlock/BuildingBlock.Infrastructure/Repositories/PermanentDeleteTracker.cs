using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;

namespace BuildingBlock.Infrastructure.Repositories
{
    internal static class PermanentDeleteTracker
    {
        private static readonly ConditionalWeakTable<DbContext, HashSet<object>> MarkedEntities = new();

        public static void Mark(DbContext context, object entity)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(entity);

            lock (MarkedEntities)
            {
                MarkedEntities
                    .GetValue(context, static _ => new HashSet<object>(ReferenceEqualityComparer.Instance))
                    .Add(entity);
            }
        }

        public static bool IsMarked(DbContext context, object entity)
        {
            lock (MarkedEntities)
            {
                return MarkedEntities.TryGetValue(context, out var entities) && entities.Contains(entity);
            }
        }

        public static void Clear(DbContext? context)
        {
            if (context is null)
            {
                return;
            }

            lock (MarkedEntities)
            {
                MarkedEntities.Remove(context);
            }
        }
    }
}
