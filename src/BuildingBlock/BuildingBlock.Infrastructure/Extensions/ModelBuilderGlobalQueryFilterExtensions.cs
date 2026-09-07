using BuildingBlock.Domain.Primitive;
using BuildingBlock.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Linq.Expressions;

namespace BuildingBlock.Infrastructure.Extensions
{
    public static class ModelBuilderGlobalQueryFilterExtensions
    {
        private const string AppendedQueryFilterName = "BuildingBlock.AppendQueryFilter";

        public static void ApplySoftDeleteQueryFilter(this ModelBuilder modelBuilder)
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (!CanApplySoftDeleteQueryFilter(entityType))
                {
                    continue;
                }

                if (entityType.FindDeclaredQueryFilter(BuildingBlockQueryFilterNames.SoftDelete) is not null)
                {
                    continue;
                }

                MoveAnonymousQueryFilterToNamedCompatibilityFilter(entityType);

                var parameter = Expression.Parameter(entityType.ClrType, "entity");
                var isDeleted = Expression.Property(parameter, nameof(ISoftDeleteEntity.IsDeleted));
                var filterBody = Expression.Equal(isDeleted, Expression.Constant(false));
                var filter = Expression.Lambda(filterBody, parameter);

                entityType.SetQueryFilter(BuildingBlockQueryFilterNames.SoftDelete, filter);
            }
        }

        private static bool CanApplyQueryFilter(IMutableEntityType entityType)
            => entityType.ClrType is not null &&
               !entityType.IsOwned() &&
               entityType.FindPrimaryKey() is not null;

        private static bool CanApplySoftDeleteQueryFilter(IMutableEntityType entityType)
        {
            if (!CanApplyQueryFilter(entityType) ||
                !typeof(ISoftDeleteEntity).IsAssignableFrom(entityType.ClrType))
            {
                return false;
            }

            var rootType = entityType.GetRootType();
            if (rootType != entityType)
            {
                if (typeof(ISoftDeleteEntity).IsAssignableFrom(rootType.ClrType))
                {
                    return false;
                }

                throw new InvalidOperationException(
                    $"Soft-delete query filtering for derived entity type {entityType.ClrType.Name} is not supported because the mapped root type {rootType.ClrType.Name} does not implement {nameof(ISoftDeleteEntity)}.");
            }

            return true;
        }

        private static void MoveAnonymousQueryFilterToNamedCompatibilityFilter(IMutableEntityType entityType)
        {
            var anonymousFilter = entityType
                .GetDeclaredQueryFilters()
                .SingleOrDefault(queryFilter => queryFilter.IsAnonymous)
                ?.Expression;

            if (anonymousFilter is null)
            {
                return;
            }

            var existingCompatibilityFilter = entityType
                .FindDeclaredQueryFilter(AppendedQueryFilterName)
                ?.Expression;
            var composedFilter = ComposeAnd(
                entityType.ClrType,
                existingCompatibilityFilter,
                anonymousFilter);

            entityType.SetQueryFilter((LambdaExpression?)null);
            entityType.SetQueryFilter(AppendedQueryFilterName, composedFilter);
        }

        private static LambdaExpression ComposeAnd(
            Type entityClrType,
            LambdaExpression? existingFilter,
            LambdaExpression newFilter)
        {
            if (existingFilter is null)
            {
                return newFilter;
            }

            var parameter = Expression.Parameter(entityClrType, "entity");
            var left = new ReplaceParameterVisitor(existingFilter.Parameters.Single(), parameter).Visit(existingFilter.Body)!;
            var right = new ReplaceParameterVisitor(newFilter.Parameters.Single(), parameter).Visit(newFilter.Body)!;
            return Expression.Lambda(Expression.AndAlso(left, right), parameter);
        }

        private sealed class ReplaceParameterVisitor : ExpressionVisitor
        {
            private readonly ParameterExpression _oldParameter;
            private readonly ParameterExpression _newParameter;

            public ReplaceParameterVisitor(ParameterExpression oldParameter, ParameterExpression newParameter)
            {
                _oldParameter = oldParameter;
                _newParameter = newParameter;
            }

            protected override Expression VisitParameter(ParameterExpression node)
                => node == _oldParameter ? _newParameter : base.VisitParameter(node);
        }
    }
}
