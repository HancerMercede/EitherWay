namespace EitherWay;

/// <summary>
/// Lazy asynchronous Either. The operation does not execute until <see cref="Run"/> is called.
/// </summary>
/// <typeparam name="L">The type of the Left (error) value.</typeparam>
/// <typeparam name="R">The type of the Right (success) value.</typeparam>
/// <param name="Run">A function that, when invoked, returns a task resolving to an Either value.</param>
public record EitherAsync<L, R>(Func<Task<Either<L, R>>> Run)
{
    /// <summary>
    /// Transforms the Right value using a synchronous function.
    /// If the current state is Left, the function is skipped.
    /// </summary>
    public EitherAsync<L, T> Map<T>(Func<R, T> map) =>
        FlatMap(r => Task.FromResult(Either<L, T>.ToRight(map(r))));

    /// <summary>
    /// Chains an asynchronous operation that returns an Either.
    /// If the current state is Left, the operation is skipped.
    /// </summary>
    public EitherAsync<L, T> FlatMap<T>(Func<R, Task<Either<L, T>>> map)
    {
        return new EitherAsync<L, T>(async () =>
        {
            var result = await Run();
            return result switch
            {
                Either<L, R>.Left l => Either<L, T>.ToLeft(l.Value),
                Either<L, R>.Right r => await map(r.Value),
                _ => throw new InvalidOperationException("Unexpected Either state.")
            };
        });
    }

    /// <summary>
    /// Creates an EitherAsync from a Right value.
    /// </summary>
    public static EitherAsync<L, R> FromRight(R value) =>
        new(() => Task.FromResult(Either<L, R>.ToRight(value)));

    /// <summary>
    /// Creates an EitherAsync from a Left value.
    /// </summary>
    public static EitherAsync<L, R> FromLeft(L value) =>
        new(() => Task.FromResult(Either<L, R>.ToLeft(value)));

    /// <summary>
    /// Safely executes an asynchronous task, catching exceptions into a Left value.
    /// You control how the exception is mapped to your error type.
    /// </summary>
    public static EitherAsync<L, R> Try(Func<Task<R>> action, Func<Exception, L> errorHandler)
    {
        return new EitherAsync<L, R>(async () =>
        {
            try
            {
                var result = await action();
                return Either<L, R>.ToRight(result);
            }
            catch (Exception ex)
            {
                return Either<L, R>.ToLeft(errorHandler(ex));
            }
        });
    }

    /// <summary>
    /// Safely executes an asynchronous task, catching exceptions into <c>EitherAsync&lt;Exception, T&gt;</c>.
    /// The raw exception becomes the Left value. Use <c>MapLeft</c> to project it to your error type.
    /// </summary>
    public static EitherAsync<Exception, T> Try<T>(Func<Task<T>> action)
    {
        return new EitherAsync<Exception, T>(async () =>
        {
            try
            {
                var result = await action();
                return Either<Exception, T>.ToRight(result);
            }
            catch (Exception ex)
            {
                return Either<Exception, T>.ToLeft(ex);
            }
        });
    }

    /// <summary>
    /// Safely executes an asynchronous task, catching exceptions into a Left value.
    /// The error is used directly if an exception occurs — you don't need a handler.
    /// </summary>
#pragma warning disable CS0693
    public static EitherAsync<L, R> Try<L, R>(Func<Task<R>> action, L error)
#pragma warning restore CS0693
    {
        return new EitherAsync<L, R>(async () =>
        {
            try
            {
                var result = await action();
                return Either<L, R>.ToRight(result);
            }
            catch
            {
                return Either<L, R>.ToLeft(error);
            }
        });
    }
}
