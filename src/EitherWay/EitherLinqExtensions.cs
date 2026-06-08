namespace EitherWay;

/// <summary>
/// LINQ query syntax support for <see cref="Either{L,R}"/> and <see cref="EitherAsync{L,R}"/>.
/// Enables <c>from x in either select x</c> syntax.
/// </summary>
public static class EitherLinqExtensions
{
    // ── Either<L, R> ──────────────────────────────────────────────

    /// <summary>LINQ Select — maps the Right value.</summary>
    public static Either<L, T> Select<L, R, T>(this Either<L, R> either, Func<R, T> selector) =>
        either.Map(selector);

    /// <summary>LINQ SelectMany — enables multiple <c>from</c> clauses.</summary>
    public static Either<L, TResult> SelectMany<L, R, T, TResult>(
        this Either<L, R> either,
        Func<R, Either<L, T>> binder,
        Func<R, T, TResult> projector) =>
        either.FlatMap(r => binder(r).Map(t => projector(r, t)));

    // ── EitherAsync<L, R> ─────────────────────────────────────────

    /// <summary>LINQ Select on EitherAsync — maps the Right value.</summary>
    public static EitherAsync<L, T> Select<L, R, T>(this EitherAsync<L, R> asyncEither, Func<R, T> selector) =>
        asyncEither.Map(selector);

    /// <summary>LINQ SelectMany on EitherAsync — enables multiple <c>from</c> clauses.</summary>
    public static EitherAsync<L, TResult> SelectMany<L, R, T, TResult>(
        this EitherAsync<L, R> asyncEither,
        Func<R, EitherAsync<L, T>> binder,
        Func<R, T, TResult> projector)
    {
        return new EitherAsync<L, TResult>(async () =>
        {
            var result = await asyncEither.Run();
            return await result.Match<Task<Either<L, TResult>>>(
                left => Task.FromResult(Either<L, TResult>.ToLeft(left)),
                async right =>
                {
                    var next = binder(right);
                    var nextResult = await next.Run();
                    return nextResult.Match(
                        left => Either<L, TResult>.ToLeft(left),
                        t => Either<L, TResult>.ToRight(projector(right, t)));
                });
        });
    }

    // Note: Where is deliberately omitted because query comprehension syntax
    // provides only the predicate (Func<R, bool>) — there's nowhere to pass the
    // error value. Use .Ensure(predicate, error) in method chains instead.
}
