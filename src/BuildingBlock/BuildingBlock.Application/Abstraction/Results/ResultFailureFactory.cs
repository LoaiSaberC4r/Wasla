using BuildingBlock.Domain.Results;
using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace BuildingBlock.Application.Abstraction.Results
{
    internal static class ResultFailureFactory
    {
        private static readonly ConcurrentDictionary<Type, Func<Error, object>> SingleErrorFactories = new();
        private static readonly ConcurrentDictionary<Type, Func<IEnumerable<Error>, object>> ManyErrorFactories = new();

        public static object Create(Type responseType, Error error)
        {
            ArgumentNullException.ThrowIfNull(responseType);
            ArgumentNullException.ThrowIfNull(error);

            var factory = SingleErrorFactories.GetOrAdd(responseType, BuildSingleErrorFactory);
            return factory(error);
        }

        public static object Create(Type responseType, IEnumerable<Error> errors)
        {
            ArgumentNullException.ThrowIfNull(responseType);
            ArgumentNullException.ThrowIfNull(errors);

            var factory = ManyErrorFactories.GetOrAdd(responseType, BuildManyErrorFactory);
            return factory(errors);
        }

        internal static int SingleErrorFactoryCount => SingleErrorFactories.Count;

        internal static int ManyErrorFactoryCount => ManyErrorFactories.Count;

        private static Func<Error, object> BuildSingleErrorFactory(Type responseType)
        {
            if (responseType == typeof(Result))
            {
                return error => Result.Fail(error);
            }

            EnsureGenericResultType(responseType);

            var method = responseType.GetMethod(nameof(Result<object>.Fail), new[] { typeof(Error) })
                ?? throw new InvalidOperationException($"Type {responseType} does not expose a Result failure factory.");

            var errorParameter = Expression.Parameter(typeof(Error), "error");
            var call = Expression.Call(method, errorParameter);
            var convert = Expression.Convert(call, typeof(object));
            return Expression.Lambda<Func<Error, object>>(convert, errorParameter).Compile();
        }

        private static Func<IEnumerable<Error>, object> BuildManyErrorFactory(Type responseType)
        {
            if (responseType == typeof(Result))
            {
                return errors => Result.Fail(errors);
            }

            EnsureGenericResultType(responseType);

            var method = responseType.GetMethod(nameof(Result<object>.Fail), new[] { typeof(IEnumerable<Error>) })
                ?? throw new InvalidOperationException($"Type {responseType} does not expose a Result failure factory.");

            var errorsParameter = Expression.Parameter(typeof(IEnumerable<Error>), "errors");
            var call = Expression.Call(method, errorsParameter);
            var convert = Expression.Convert(call, typeof(object));
            return Expression.Lambda<Func<IEnumerable<Error>, object>>(convert, errorsParameter).Compile();
        }

        private static void EnsureGenericResultType(Type responseType)
        {
            if (!responseType.IsGenericType || responseType.GetGenericTypeDefinition() != typeof(Result<>))
            {
                throw new InvalidOperationException($"Response type {responseType} is not a Result type.");
            }
        }
    }
}
