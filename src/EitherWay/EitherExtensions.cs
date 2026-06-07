namespace EitherWay;

/// <summary>
/// Extension methods for <see cref="Either{L,R}"/>.
/// </summary>
public static class EitherExtensions
{
    /// <summary>Transforms the Right value with a synchronous function.</summary>
    public static Either<L, T> Map<L, R, T>(this Either<L, R> either, Func<R, T> map) =>
        either.Match<Either<L, T>>(
            left => Either<L, T>.ToLeft(left),
            right => Either<L, T>.ToRight(map(right)));

    /// <summary>Chains a function that returns an Either.</summary>
    public static Either<L, T> FlatMap<L, R, T>(this Either<L, R> either, Func<R, Either<L, T>> map) =>
        either.Match<Either<L, T>>(
            left => Either<L, T>.ToLeft(left),
            right => map(right));

    /// <summary>Transforms the Left value.</summary>
    public static Either<L2, R> MapLeft<L, R, L2>(this Either<L, R> either, Func<L, L2> map) =>
        either.Match<Either<L2, R>>(
            left => Either<L2, R>.ToLeft(map(left)),
            right => Either<L2, R>.ToRight(right));

    /// <summary>Transforms both Left and Right values.</summary>
    public static Either<L2, R2> BiMap<L, R, L2, R2>(this Either<L, R> either, Func<L, L2> mapLeft, Func<R, R2> mapRight) =>
        either.Match<Either<L2, R2>>(
            left => Either<L2, R2>.ToLeft(mapLeft(left)),
            right => Either<L2, R2>.ToRight(mapRight(right)));

    /// <summary>
    /// Ensures the Right value satisfies a predicate. If it doesn't, the flow switches to Left.
    /// </summary>
    public static Either<L, R> Ensure<L, R>(this Either<L, R> either, Func<R, bool> predicate, L error) =>
        either.FlatMap(value => predicate(value)
            ? Either<L, R>.ToRight(value)
            : Either<L, R>.ToLeft(error));

    /// <summary>Ensures the Right value satisfies a predicate, with a lazy error factory.</summary>
    public static Either<L, R> Ensure<L, R>(this Either<L, R> either, Func<R, bool> predicate, Func<R, L> errorFactory) =>
        either.FlatMap(value => predicate(value)
            ? Either<L, R>.ToRight(value)
            : Either<L, R>.ToLeft(errorFactory(value)));

    /// <summary>Executes a side-effect action if the state is Right. Returns the value unchanged.</summary>
    public static Either<L, R> Tap<L, R>(this Either<L, R> either, Action<R> action)
    {
        if (either is Either<L, R>.Right r) action(r.Value);
        return either;
    }
}
