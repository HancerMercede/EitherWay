namespace EitherWay.Tests;

public class EitherAsyncTests
{
    // ── Factory ──────────────────────────────────────────────

    [Fact]
    public async Task Factory_Right_CreatesRightValue()
    {
        var asyncEither = EitherAsync.Right(42);

        var result = await asyncEither.Run();

        Assert.True(result is Either<string, int>.Right);
        Assert.Equal(42, result.Match(_ => 0, x => x));
    }

    [Fact]
    public async Task Factory_Left_CreatesLeftValue()
    {
        var asyncEither = EitherAsync.Left<int>("fail");

        var result = await asyncEither.Run();

        Assert.True(result is Either<string, int>.Left);
        Assert.Equal("fail", result.Match(x => x, _ => ""));
    }

    // ── Map ──────────────────────────────────────────────────

    [Fact]
    public async Task Map_Right_TransformsValue()
    {
        var asyncEither = EitherAsync.Right(21);

        var result = await asyncEither.Map(x => x * 2).Run();

        Assert.Equal(Either<string, int>.ToRight(42), result);
    }

    [Fact]
    public async Task Map_Left_DoesNotTransform()
    {
        var asyncEither = EitherAsync.Left<int>("error");

        var result = await asyncEither.Map(x => x * 2).Run();

        Assert.True(result is Either<string, int>.Left);
        Assert.Equal("error", result.Match(x => x, _ => ""));
    }

    // ── FlatMap ──────────────────────────────────────────────

    [Fact]
    public async Task FlatMap_Right_ChainsOperation()
    {
        var asyncEither = EitherAsync.Right(10);

        var result = await asyncEither
            .FlatMap(x => Task.FromResult(Either<string, string>.ToRight($"value: {x}")))
            .Run();

        Assert.True(result is Either<string, string>.Right);
        Assert.Equal("value: 10", result.Match(_ => "", x => x));
    }

    [Fact]
    public async Task FlatMap_Left_SkipsOperation()
    {
        var asyncEither = EitherAsync.Left<int>("error");

        var result = await asyncEither
            .FlatMap(x => Task.FromResult(Either<string, string>.ToRight("never")))
            .Run();

        Assert.True(result is Either<string, string>.Left);
        Assert.Equal("error", result.Match(x => x, _ => ""));
    }

    // ── Ensure ───────────────────────────────────────────────

    [Fact]
    public async Task Ensure_WhenPredicatePasses_KeepsRight()
    {
        var asyncEither = EitherAsync.Right(5);

        var result = await asyncEither.Ensure(x => x > 0, "must be positive").Run();

        Assert.Equal(Either<string, int>.ToRight(5), result);
    }

    [Fact]
    public async Task Ensure_WhenPredicateFails_ReturnsLeft()
    {
        var asyncEither = EitherAsync.Right(-1);

        var result = await asyncEither.Ensure(x => x > 0, "must be positive").Run();

        Assert.Equal(Either<string, int>.ToLeft("must be positive"), result);
    }

    // ── Try ──────────────────────────────────────────────────

    [Fact]
    public async Task Try_OnRight_Success()
    {
        var asyncEither = EitherAsync.Right(42);

        var result = await asyncEither
            .Try(x => Task.FromResult(x.ToString()), ex => ex.Message)
            .Run();

        Assert.True(result is Either<string, string>.Right);
        Assert.Equal("42", result.Match(_ => "", x => x));
    }

    [Fact]
    public async Task Try_OnRight_WithDiscard()
    {
        var asyncEither = EitherAsync.Right("ignored");

        var result = await asyncEither
            .Try(_ => Task.FromResult(42), ex => ex.Message)
            .Run();

        Assert.True(result is Either<string, int>.Right);
        Assert.Equal(42, result.Match(_ => 0, x => x));
    }

    [Fact]
    public async Task Try_OnLeft_DoesNotExecute()
    {
        var asyncEither = EitherAsync.Left<int>("pre-existing error");

        var result = await asyncEither
            .Try(x => Task.FromResult("never"), ex => ex.Message)
            .Run();

        Assert.True(result is Either<string, string>.Left);
        Assert.Equal("pre-existing error", result.Match(x => x, _ => ""));
    }

    // ── MatchAsync ───────────────────────────────────────────

    [Fact]
    public async Task MatchAsync_Right_ExecutesOnRight()
    {
        var asyncEither = EitherAsync.Right(42);

        var result = await asyncEither.MatchAsync(
            left => $"error: {left}",
            right => $"value: {right}");

        Assert.Equal("value: 42", result);
    }

    [Fact]
    public async Task MatchAsync_Left_ExecutesOnLeft()
    {
        var asyncEither = EitherAsync.Left<int>("error");

        var result = await asyncEither.MatchAsync(
            left => $"error: {left}",
            right => $"value: {right}");

        Assert.Equal("error: error", result);
    }

    // ── Tap ──────────────────────────────────────────────────

    [Fact]
    public async Task Tap_Right_ExecutesSideEffect()
    {
        var asyncEither = EitherAsync.Right(42);
        var sideEffect = 0;

        var result = await asyncEither.Tap(x => sideEffect = x).Run();

        Assert.Equal(42, sideEffect);
        Assert.True(result is Either<string, int>.Right);
        Assert.Equal(42, result.Match(_ => 0, x => x));
    }

    [Fact]
    public async Task Tap_Left_DoesNotExecuteSideEffect()
    {
        var asyncEither = EitherAsync.Left<int>("error");
        var sideEffect = 0;

        var result = await asyncEither.Tap(x => sideEffect = x).Run();

        Assert.Equal(0, sideEffect);
        Assert.True(result is Either<string, int>.Left);
        Assert.Equal("error", result.Match(x => x, _ => ""));
    }

    // ── LINQ ─────────────────────────────────────────────────

    [Fact]
    public async Task LinqSelect_TransformsRight()
    {
        var result = await (from x in EitherAsync.Right(21)
                            select x * 2).Run();

        Assert.Equal(Either<string, int>.ToRight(42), result);
    }

    [Fact]
    public async Task LinqSelectMany_WithMultipleFrom()
    {
        var result = await (from a in EitherAsync.Right(3)
                            from b in EitherAsync.Right(4)
                            select a * b).Run();

        Assert.Equal(Either<string, int>.ToRight(12), result);
    }

    [Fact]
    public async Task LinqSelectMany_LeftShortCircuits()
    {
        var result = await (from a in EitherAsync.Right(3)
                            from b in EitherAsync.Left<int>("fail")
                            select a * b).Run();

        Assert.Equal(Either<string, int>.ToLeft("fail"), result);
    }

    // ── EitherAsync.Try (static) ─────────────────────────────

    [Fact]
    public async Task StaticTry_Success_ReturnsRight()
    {
        var asyncEither = EitherAsync<string, int>.Try(
            () => Task.FromResult(42),
            ex => ex.Message);

        var result = await asyncEither.Run();

        Assert.True(result is Either<string, int>.Right);
        Assert.Equal(42, result.Match(_ => 0, x => x));
    }

    [Fact]
    public async Task StaticTry_Exception_ReturnsLeft()
    {
        var asyncEither = EitherAsync<string, int>.Try(
            () => throw new InvalidOperationException("boom"),
            ex => ex.Message);

        var result = await asyncEither.Run();

        Assert.True(result is Either<string, int>.Left);
        Assert.Equal("boom", result.Match(x => x, _ => ""));
    }
}
