using BuildingBlock.Domain.Results;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace BuildingBlock.Application.Abstraction.Caching
{
    internal static class ResultInspector
    {
        private static readonly ConcurrentDictionary<Type, Func<object, bool>> _isSuccess = new();

        public static bool IsSuccess<T>(T value)
        {
            if (value is null) return true;

            if (value is Result r) return r.IsSuccess;

            return IsSuccessObject(value);
        }

        public static bool IsResultType(Type type)
        {
            ArgumentNullException.ThrowIfNull(type);

            return type == typeof(Result) ||
                type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Result<>);
        }

        internal static int SuccessAccessorCount => _isSuccess.Count;

        private static bool IsSuccessObject(object value)
        {
            var t = value.GetType();
            var accessor = _isSuccess.GetOrAdd(t, BuildAccessor);
            return accessor(value);
        }

        private static Func<object, bool> BuildAccessor(Type t)
        {
            // Result<T>
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Result<>))
            {
                var prop = t.GetProperty("IsSuccess", BindingFlags.Instance | BindingFlags.Public);
                if (prop is null || prop.PropertyType != typeof(bool))
                    return _ => true;

                // (object o) => ((Result<T>)o).IsSuccess
                var obj = Expression.Parameter(typeof(object), "o");
                var cast = Expression.Convert(obj, t);
                var read = Expression.Property(cast, prop);
                var lambda = Expression.Lambda<Func<object, bool>>(read, obj);
                return lambda.Compile();
            }

            // Unknown response => treat as success
            return _ => true;
        }
    }
}
