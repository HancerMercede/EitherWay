namespace EitherWay;

/// <summary>
/// Extension methods for <see cref="EitherAsync{L,R}"/>.
/// </summary>
public static class EitherAsyncExtensions
{
    /// <summary>Transforms the Right value with a synchronous function.</summary>
    public static EitherAsync<L, T> Map<L, R, T>(this EitherAsync<L, R> asyncEither, Func<R, T> map) =>
        asyncEither.FlatMap(r => Task.FromResult(Either<L, T>.ToRight(map(r))));

    /// <summary>Chains a synchronous function that returns an Either.</summary>
    public static EitherAsync<L, T> FlatMap<L, R, T>(this EitherAsync<L, R> asyncEither, Func<R, Either<L, T>> map) =>
        asyncEither.FlatMap(r => Task.FromResult(map(r)));

    /// <summary>Transforms the Left value.</summary>
    public static EitherAsync<L2, R> MapLeft<L, R, L2>(this EitherAsync<L, R> asyncEither, Func<L, L2> map)
    {
        return new EitherAsync<L2, R>(async () =>
        {
            var result = await asyncEither.Run();
            return result.Match<Either<L2, R>>(
                left => Either<L2, R>.ToLeft(map(left)),
                right => Either<L2, R>.ToRight(right));
        });
    }

    /// <summary>Transforms both Left and Right values.</summary>
    public static EitherAsync<L2, R2> BiMap<L, R, L2, R2>(this EitherAsync<L, R> asyncEither, Func<L, L2> mapLeft, Func<R, R2> mapRight)
    {
        return new EitherAsync<L2, R2>(async () =>
        {
            var result = await asyncEither.Run();
            return result.Match<Either<L2, R2>>(
                left => Either<L2, R2>.ToLeft(mapLeft(left)),
                right => Either<L2, R2>.ToRight(mapRight(right)));
        });
    }

    /// <summary>
    /// Ensures the Right value satisfies a predicate. If not, the flow switches to Left.
    /// </summary>
    public static EitherAsync<L, R> Ensure<L, R>(this EitherAsync<L, R> asyncEither, Func<R, bool> predicate, L error) =>
        asyncEither.FlatMap(r => Task.FromResult(
            predicate(r)
                ? Either<L, R>.ToRight(r)
                : Either<L, R>.ToLeft(error)));

    /// <summary>
    /// Ensures the Right value satisfies a predicate, with a lazy error factory.
    /// </summary>
    public static EitherAsync<L, R> Ensure<L, R>(this EitherAsync<L, R> asyncEither, Func<R, bool> predicate, Func<R, L> errorFactory) =>
        asyncEither.FlatMap(r => Task.FromResult(
            predicate(r)
                ? Either<L, R>.ToRight(r)
                : Either<L, R>.ToLeft(errorFactory(r))));

    /// <summary>
    /// Ensures the Right value satisfies a predicate, with a lazy error factory that ignores the value.
    /// </summary>
    public static EitherAsync<L, R> Ensure<L, R>(this EitherAsync<L, R> asyncEither, Func<R, bool> predicate, Func<L> errorFactory) =>
        asyncEither.Ensure(predicate, _ => errorFactory());

    /// <summary>
    /// Safely executes an asynchronous operation, catching exceptions into a Left value.
    /// Receives the Right value from the previous step (use <c>_</c> to discard).
    /// </summary>
    public static EitherAsync<L, T> FlatMap<L, R, T>(this EitherAsync<L, R> asyncEither, Func<R, Task<T>> action, Func<Exception, L> errorHandler)
    {
        return new EitherAsync<L, T>(async () =>
        {
            var result = await asyncEither.Run();
            return await result.Match<Task<Either<L, T>>>(
                left => Task.FromResult(Either<L, T>.ToLeft(left)),
                async right =>
                {
                    try
                    {
                        var value = await action(right);
                        return Either<L, T>.ToRight(value);
                    }
                    catch (Exception ex)
                    {
                        return Either<L, T>.ToLeft(errorHandler(ex));
                    }
                });
        });
    }

    /// <summary>Executes a side-effect action if the state is Right. Returns the original value unchanged.</summary>
    public static EitherAsync<L, R> Tap<L, R>(this EitherAsync<L, R> asyncEither, Action<R> action)
    {
        return new EitherAsync<L, R>(async () =>
        {
            var result = await asyncEither.Run();
            if (result is Either<L, R>.Right r) action(r.Value);
            return result;
        });
    }

    /// <summary>Pattern matches on the async result, returning a task.</summary>
    public static async Task<T> MatchAsync<L, R, T>(this EitherAsync<L, R> asyncEither, Func<L, T> onLeft, Func<R, T> onRight)
    {
        var result = await asyncEither.Run();
        return result.Match(onLeft, onRight);
    }

    /// <summary>Pattern matches on the async result, with async handlers.</summary>
    public static async Task<T> MatchAsync<L, R, T>(this EitherAsync<L, R> asyncEither, Func<L, Task<T>> onLeft, Func<R, Task<T>> onRight)
    {
        var result = await asyncEither.Run();
        return await result.Match(onLeft, onRight);
    }
}
