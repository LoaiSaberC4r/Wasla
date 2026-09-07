using System.Collections.Immutable;
using System.Diagnostics;

namespace BuildingBlock.Domain.Results
{
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public sealed class Result
    {
        private Result(bool isSuccess, ImmutableArray<Error> errors)
        {
            if (isSuccess && !errors.IsEmpty)
            {
                throw new InvalidOperationException("A successful result cannot contain errors.");
            }

            if (!isSuccess && errors.IsEmpty)
            {
                throw new InvalidOperationException("A failure result must contain at least one error.");
            }

            IsSuccess = isSuccess;
            Errors = errors;
        }

        public bool IsSuccess { get; }

        public bool IsFailure => !IsSuccess;

        public ImmutableArray<Error> Errors { get; }

        public static Result Ok() => new(true, ImmutableArray<Error>.Empty);

        public static Result Fail(Error error)
        {
            ArgumentNullException.ThrowIfNull(error);
            return new Result(false, ImmutableArray.Create(error));
        }

        public static Result Fail(IEnumerable<Error> errors)
            => new(false, NormalizeFailureErrors(errors));

        public static Result NotFound(string code, string message)
            => Fail(Error.NotFound(code, message));

        public static Result Conflict(string code, string message)
            => Fail(Error.Conflict(code, message));

        public static Result<T> NotFound<T>(string code, string message)
            => Result<T>.Fail(Error.NotFound(code, message));

        public static Result<T> Conflict<T>(string code, string message)
            => Result<T>.Fail(Error.Conflict(code, message));

        public static Result FirstFailureOrOk(params Result[] results)
        {
            ArgumentNullException.ThrowIfNull(results);

            foreach (var result in results)
            {
                ArgumentNullException.ThrowIfNull(result);
                if (result.IsFailure)
                {
                    return result;
                }
            }

            return Ok();
        }

        public static Result Combine(params Result[] results)
            => FirstFailureOrOk(results);

        internal static ImmutableArray<Error> NormalizeFailureErrors(IEnumerable<Error> errors)
        {
            ArgumentNullException.ThrowIfNull(errors);

            var normalized = errors.ToImmutableArray();
            if (normalized.IsEmpty)
            {
                throw new ArgumentException("A failure result must contain at least one error.", nameof(errors));
            }

            if (normalized.Any(error => error is null))
            {
                throw new ArgumentException("Failure errors cannot contain null values.", nameof(errors));
            }

            return normalized;
        }

        public override string ToString() => DebuggerDisplay;

        private string DebuggerDisplay => IsSuccess
            ? "Result: Success"
            : $"Result: Failure ({Errors.Length} errors)";

    }

    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1000:Do not declare static members on generic types",
        Justification = "Result<T> static factories are established public API and must be preserved for compatibility.")]
    public sealed class Result<T>
    {
        private readonly T _value;

        private Result(T value)
        {
            IsSuccess = true;
            _value = value;
            Errors = ImmutableArray<Error>.Empty;
        }

        private Result(ImmutableArray<Error> errors)
        {
            if (errors.IsEmpty)
            {
                throw new InvalidOperationException("A failure result must contain at least one error.");
            }

            IsSuccess = false;
            _value = default!;
            Errors = errors;
        }

        public bool IsSuccess { get; }

        public bool IsFailure => !IsSuccess;

        public T Value => IsSuccess
            ? _value
            : throw new InvalidOperationException("No value is available for a failure result.");

        public ImmutableArray<Error> Errors { get; }

        public static Result<T> Ok(T value) => new(value);

        public static Result<T> Fail(Error error)
        {
            ArgumentNullException.ThrowIfNull(error);
            return new Result<T>(ImmutableArray.Create(error));
        }

        public static Result<T> Fail(IEnumerable<Error> errors)
            => new(Result.NormalizeFailureErrors(errors));

        public static Result<IReadOnlyList<T>> Combine(params Result<T>[] results)
        {
            ArgumentNullException.ThrowIfNull(results);

            var list = new List<T>(results.Length);
            foreach (var result in results)
            {
                ArgumentNullException.ThrowIfNull(result);
                if (result.IsFailure)
                {
                    return Result<IReadOnlyList<T>>.Fail(result.Errors);
                }

                list.Add(result.Value);
            }

            return Result<IReadOnlyList<T>>.Ok(list);
        }

        public void Deconstruct(out bool isSuccess, out T value, out ImmutableArray<Error> errors)
        {
            isSuccess = IsSuccess;
            value = _value;
            errors = Errors;
        }

        public override string ToString() => DebuggerDisplay;

        private string DebuggerDisplay => IsSuccess
            ? $"Result<{typeof(T).Name}>: {(_value is null ? "null" : _value)}"
            : $"Result<{typeof(T).Name}>: Failure ({Errors.Length} errors)";
    }
}
