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

    /// <summary>
    /// Safely executes an asynchronous task, catching exceptions into <c>EitherAsync&lt;Exception, R&gt;</c>.
    /// The raw exception becomes the Left value. Use <c>MapLeft</c> to project it to your error type.
    /// </summary>
    public static EitherAsync<Exception, R> Try<R>(Func<Task<R>> action) =>
        EitherAsync<Exception, R>.Try(action);

    /// <summary>
    /// Safely executes an asynchronous task, catching exceptions into a Left value.
    /// The error is used directly if an exception occurs — you don't need a handler.
    /// </summary>
    public static EitherAsync<L, R> Try<L, R>(Func<Task<R>> action, L error) =>
        EitherAsync<L, R>.Try(action, error);

    /// <summary>
    /// Safely executes an asynchronous task, catching exceptions into a Left value.
    /// You control how the exception is mapped to your error type.
    /// </summary>
    public static EitherAsync<L, R> Try<L, R>(Func<Task<R>> action, Func<Exception, L> errorHandler) =>
        EitherAsync<L, R>.Try(action, errorHandler);
}
