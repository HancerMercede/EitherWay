namespace EitherWay;

/// <summary>
/// Represents a value of one of two possible types: Left (typically error) or Right (typically success).
/// </summary>
/// <typeparam name="L">The type of the Left (error) value.</typeparam>
/// <typeparam name="R">The type of the Right (success) value.</typeparam>
public abstract record Either<L, R>
{
    private Either() { }

    /// <summary>Represents the error/failure state.</summary>
    public sealed record Left(L Value) : Either<L, R>;

    /// <summary>Represents the success state.</summary>
    public sealed record Right(R Value) : Either<L, R>;

    /// <summary>Wraps a value into a Left (error) state.</summary>
    public static Either<L, R> ToLeft(L value) => new Left(value);

    /// <summary>Wraps a value into a Right (success) state.</summary>
    public static Either<L, R> ToRight(R value) => new Right(value);

    /// <summary>
    /// Pattern matches on the Either value, executing one of two functions.
    /// </summary>
    public T Match<T>(Func<L, T> onLeft, Func<R, T> onRight) => this switch
    {
        Left l => onLeft(l.Value),
        Right r => onRight(r.Value),
        _ => throw new InvalidOperationException("Unexpected Either state.")
    };
}
