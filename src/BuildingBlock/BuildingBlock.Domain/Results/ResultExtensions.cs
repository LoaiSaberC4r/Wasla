using System.Collections.Immutable;

namespace BuildingBlock.Domain.Results
{
    public static class ResultExtensions
    {
        public static Result<T> Ensure<T>(this Result<T> result, Func<T, bool> predicate, Error error)
        {
            ArgumentNullException.ThrowIfNull(predicate);
            ArgumentNullException.ThrowIfNull(error);

            return result.IsFailure
                ? result
                : predicate(result.Value)
                    ? result
                    : Result<T>.Fail(error);
        }

        public static Result<TOut> Map<TIn, TOut>(this Result<TIn> result, Func<TIn, TOut> map)
        {
            ArgumentNullException.ThrowIfNull(map);
            return result.IsSuccess ? Result<TOut>.Ok(map(result.Value)) : Result<TOut>.Fail(result.Errors);
        }

        public static Result<TOut> Bind<TIn, TOut>(this Result<TIn> result, Func<TIn, Result<TOut>> bind)
        {
            ArgumentNullException.ThrowIfNull(bind);
            return result.IsSuccess ? bind(result.Value) : Result<TOut>.Fail(result.Errors);
        }

        public static Result<T> Tap<T>(this Result<T> result, Action<T> effect)
        {
            ArgumentNullException.ThrowIfNull(effect);

            if (result.IsSuccess)
            {
                effect(result.Value);
            }

            return result;
        }

        public static TOut Match<T, TOut>(
            this Result<T> result,
            Func<T, TOut> onSuccess,
            Func<ImmutableArray<Error>, TOut> onFailure)
        {
            ArgumentNullException.ThrowIfNull(onSuccess);
            ArgumentNullException.ThrowIfNull(onFailure);

            return result.IsSuccess ? onSuccess(result.Value) : onFailure(result.Errors);
        }

        public static async ValueTask<Result<TOut>> MapAsync<TIn, TOut>(
            this Result<TIn> result,
            Func<TIn, ValueTask<TOut>> mapAsync)
        {
            ArgumentNullException.ThrowIfNull(mapAsync);
            return result.IsSuccess
                ? Result<TOut>.Ok(await mapAsync(result.Value))
                : Result<TOut>.Fail(result.Errors);
        }

        public static async ValueTask<Result<TOut>> BindAsync<TIn, TOut>(
            this Result<TIn> result,
            Func<TIn, ValueTask<Result<TOut>>> bindAsync)
        {
            ArgumentNullException.ThrowIfNull(bindAsync);
            return result.IsSuccess ? await bindAsync(result.Value) : Result<TOut>.Fail(result.Errors);
        }

        public static async ValueTask<TOut> MatchAsync<T, TOut>(
            this Result<T> result,
            Func<T, ValueTask<TOut>> onSuccess,
            Func<ImmutableArray<Error>, ValueTask<TOut>> onFailure)
        {
            ArgumentNullException.ThrowIfNull(onSuccess);
            ArgumentNullException.ThrowIfNull(onFailure);

            return result.IsSuccess ? await onSuccess(result.Value) : await onFailure(result.Errors);
        }

        public static Result<T> TapError<T>(this Result<T> result, Action<IReadOnlyList<Error>> effectOnFailure)
        {
            ArgumentNullException.ThrowIfNull(effectOnFailure);

            if (result.IsFailure)
            {
                effectOnFailure(result.Errors);
            }

            return result;
        }

        public static async ValueTask<Result<T>> EnsureAsync<T>(
            this Result<T> result,
            Func<T, ValueTask<bool>> predicateAsync,
            Error error)
        {
            ArgumentNullException.ThrowIfNull(predicateAsync);
            ArgumentNullException.ThrowIfNull(error);

            if (result.IsFailure)
            {
                return result;
            }

            return await predicateAsync(result.Value) ? result : Result<T>.Fail(error);
        }

        public static async ValueTask<Result<T>> TapAsync<T>(this ValueTask<Result<T>> task, Action<T> effect)
        {
            ArgumentNullException.ThrowIfNull(effect);

            var result = await task;
            if (result.IsSuccess)
            {
                effect(result.Value);
            }

            return result;
        }

        public static async ValueTask<Result<T>> TapErrorAsync<T>(
            this ValueTask<Result<T>> task,
            Action<IReadOnlyList<Error>> effectOnFailure)
        {
            ArgumentNullException.ThrowIfNull(effectOnFailure);

            var result = await task;
            if (result.IsFailure)
            {
                effectOnFailure(result.Errors);
            }

            return result;
        }
    }
}
