using BuildingBlock.Domain.Results;
using System.Collections.Immutable;
using System.Reflection;

namespace BuildingBlock.Application.Diagnostics
{
    internal static class ResultDiagnostics
    {
        public static bool TryInspect(object? response, out ResultInfo info)
        {
            info = default;

            if (response is Result result)
            {
                info = new ResultInfo(true, result.IsFailure, result.Errors);
                return true;
            }

            var type = response?.GetType();
            if (type is null ||
                !type.IsGenericType ||
                type.GetGenericTypeDefinition() != typeof(Result<>))
            {
                return false;
            }

            var isFailureProperty = type.GetProperty("IsFailure", BindingFlags.Public | BindingFlags.Instance);
            var errorsProperty = type.GetProperty("Errors", BindingFlags.Public | BindingFlags.Instance);
            if (isFailureProperty is null || errorsProperty is null)
            {
                return false;
            }

            var isFailure = (bool)(isFailureProperty.GetValue(response) ?? false);
            var errors = errorsProperty.GetValue(response) switch
            {
                ImmutableArray<Error> immutable => immutable,
                IReadOnlyList<Error> list => list.ToImmutableArray(),
                IEnumerable<Error> enumerable => enumerable.ToImmutableArray(),
                _ => ImmutableArray<Error>.Empty
            };

            info = new ResultInfo(true, isFailure, errors);
            return true;
        }
    }

    internal readonly record struct ResultInfo(
        bool IsResult,
        bool IsFailure,
        IReadOnlyList<Error> Errors)
    {
        public Error? PrimaryError => Errors.Count > 0 ? Errors[0] : null;

        public int ErrorCount => Errors.Count;
    }
}
