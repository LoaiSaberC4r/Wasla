using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace BuildingBlock.Infrastructure.Repositories
{
    internal static class EfPrimaryKeyExpressionBuilder
    {
        public static Expression<Func<TEntity, bool>> BuildSingleKeyPredicate<TEntity, TKey>(
            DbContext dbContext,
            TKey key)
            where TEntity : class
            where TKey : notnull
        {
            ArgumentNullException.ThrowIfNull(dbContext);
            ArgumentNullException.ThrowIfNull(key);

            var entityType = dbContext.Model.FindEntityType(typeof(TEntity))
                ?? throw new InvalidOperationException(
                    $"Entity {typeof(TEntity).Name} is not part of the EF Core model.");

            var primaryKey = entityType.FindPrimaryKey()
                ?? throw new InvalidOperationException(
                    $"Entity {typeof(TEntity).Name} has no primary key configured.");

            if (primaryKey.Properties.Count > 1)
            {
                throw new NotSupportedException(
                    $"Entity {typeof(TEntity).Name} has a composite primary key. Use a Specification for this query.");
            }

            var keyProperty = primaryKey.Properties[0];
            var propertyType = keyProperty.ClrType;
            var keyType = typeof(TKey);

            if (!AreCompatibleKeyTypes(propertyType, keyType))
            {
                throw new ArgumentException(
                    $"Key type {keyType.Name} is not compatible with primary key {keyProperty.Name} ({propertyType.Name}) for entity {typeof(TEntity).Name}.",
                    nameof(key));
            }

            var entity = Expression.Parameter(typeof(TEntity), "entity");
            var property = Expression.Call(
                typeof(EF),
                nameof(EF.Property),
                new[] { propertyType },
                entity,
                Expression.Constant(keyProperty.Name));
            var value = Expression.Constant(key, propertyType);
            var equals = Expression.Equal(property, value);

            return Expression.Lambda<Func<TEntity, bool>>(equals, entity);
        }

        private static bool AreCompatibleKeyTypes(Type propertyType, Type keyType)
        {
            var nonNullablePropertyType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
            var nonNullableKeyType = Nullable.GetUnderlyingType(keyType) ?? keyType;
            return nonNullablePropertyType == nonNullableKeyType;
        }
    }
}
