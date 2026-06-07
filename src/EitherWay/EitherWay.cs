namespace EitherWay;

/// <summary>
/// Represents a void return type for operations that succeed with no data.
/// Use with <c>Either&lt;string, Unit&gt;</c> for command operations.
/// </summary>
public record Unit;

/// <summary>
/// Static factory for creating <see cref="Either{L,R}"/> values with <c>string</c> as the default error type.
/// </summary>
public static class Either
{
    /// <summary>Creates an <see cref="Either{String,R}.Right"/> with the given value.</summary>
    public static Either<string, R> Ok<R>(R value) =>
        Either<string, R>.ToRight(value);

    /// <summary>Creates an <see cref="Either{String,R}.Left"/> with the given error message.</summary>
    public static Either<string, R> Fail<R>(string error) =>
        Either<string, R>.ToLeft(error);
}

/// <summary>
/// Static factory for creating <see cref="EitherAsync{L,R}"/> values with <c>string</c> as the default error type.
/// </summary>
public static class EitherAsync
{
    /// <summary>Creates an <see cref="EitherAsync{String,R}"/> from a Right value.</summary>
    public static EitherAsync<string, R> Right<R>(R value) =>
        new(() => Task.FromResult(Either<string, R>.ToRight(value)));

    /// <summary>Creates an <see cref="EitherAsync{String,R}"/> from a Left error message.</summary>
    public static EitherAsync<string, R> Left<R>(string error) =>
        new(() => Task.FromResult(Either<string, R>.ToLeft(error)));
}
