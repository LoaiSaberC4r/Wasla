using BuildingBlock.Domain.Enums;
using BuildingBlock.Domain.Specification;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlock.Infrastructure.SpecificationEvaluator
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1000:Do not declare static members on generic types",
        Justification = "SpecificationEvaluator<TEntity> is an established public helper API and must be preserved for compatibility.")]
    internal static class SpecificationEvaluator<TEntity> where TEntity : class
    {
        // ============== Data Query (full spec: includes/order/paging...) ==============
        public static IQueryable<TEntity> BuildQuery(
            IQueryable<TEntity> inputQuery,
            Specification<TEntity> spec,
            bool forceTracking = false,
            bool forceNoTracking = false)
            => BuildCore(inputQuery, spec, forCount: false, forceTracking, forceNoTracking);

        public static IQueryable<TOut> BuildQuery<TOut>(
            IQueryable<TEntity> inputQuery,
            Specification<TEntity, TOut> spec,
            bool forceTracking = false,
            bool forceNoTracking = false)
        {
            var entityQuery = BuildCore(inputQuery, spec, forCount: false, forceTracking, forceNoTracking);
            return entityQuery.Select(spec.Selector);
        }

        // ============== Count Query (filters only; no includes/order/paging) ==============
        public static IQueryable<TEntity> BuildCountQuery(
            IQueryable<TEntity> inputQuery,
            Specification<TEntity> spec)
            => BuildCore(inputQuery, spec, forCount: true);

        public static IQueryable<TEntity> BuildCountQuery<TOut>(
            IQueryable<TEntity> inputQuery,
            Specification<TEntity, TOut> spec)
            => BuildCore(inputQuery, spec, forCount: true);

        // ----------------------- Internal -----------------------
        private static IQueryable<TEntity> BuildCore(
            IQueryable<TEntity> inputQuery,
            Specification<TEntity> spec,
            bool forCount,
            bool forceTracking = false,
            bool forceNoTracking = false)
        {
            IQueryable<TEntity> query = inputQuery;

            // Criteria
            if (spec.Criteria is not null)
                query = query.Where(spec.Criteria);

            if (forCount)
                return query;

            // Tracking
            query = (forceNoTracking, forceTracking) switch
            {
                (true, _) => query.AsNoTracking(),
                (_, true) => query.AsTracking(),
                _ => spec.Tracking switch
                {
                    TrackingBehavior.NoTracking => query.AsNoTracking(),
                    TrackingBehavior.NoTrackingWithIdentityResolution => query.AsNoTrackingWithIdentityResolution(),
                    TrackingBehavior.TrackAll => query.AsTracking(),
                    _ => query.AsNoTracking()
                }
            };

            // Includes
            if (spec.IncludeExpressions.Count > 0)
            {
                foreach (var includeExpression in spec.IncludeExpressions)
                    query = query.Include(includeExpression);
            }

            // Ordering
            if (spec.OrderExpressions.Count > 0)
            {
                IOrderedQueryable<TEntity>? ordered = null;

                foreach (var order in spec.OrderExpressions)
                {
                    ordered = ordered is null
                        ? order.ApplyInitial(query)
                        : order.ApplyThen(ordered);
                }

                query = ordered!;
            }

            // Distinct
            if (spec.IsDistinct)
                query = query.Distinct();

            // Paging
            if (spec.IsPagingEnabled)
                query = query.Skip(spec.Skip).Take(spec.Take);

            // Split / Single query toggle
            if (spec.IsSplitQuery)
                query = query.AsSplitQuery();
            else if (spec.IsSingleQuery)
                query = query.AsSingleQuery();

            foreach (var queryTag in spec.QueryTags)
            {
                query = query.TagWith(queryTag);
            }

            return query;
        }
    }
}
