namespace EitherWay.Tests;

public class EitherTests
{
    // ── Match ────────────────────────────────────────────────

    [Fact]
    public void Match_Right_ExecutesOnRight()
    {
        var either = Either<string, int>.ToRight(42);

        var result = either.Match(left => $"error: {left}", right => $"value: {right}");

        Assert.Equal("value: 42", result);
    }

    [Fact]
    public void Match_Left_ExecutesOnLeft()
    {
        var either = Either<string, int>.ToLeft("something went wrong");

        var result = either.Match(left => $"error: {left}", right => $"value: {right}");

        Assert.Equal("error: something went wrong", result);
    }

    // ── Map ──────────────────────────────────────────────────

    [Fact]
    public void Map_Right_TransformsValue()
    {
        var either = Either<string, int>.ToRight(21);

        var result = either.Map(x => x * 2);

        Assert.Equal(Either<string, int>.ToRight(42), result);
    }

    [Fact]
    public void Map_Left_DoesNotTransform()
    {
        var either = Either<string, int>.ToLeft("error");

        var result = either.Map(x => x * 2);

        Assert.True(result is Either<string, int>.Left);
    }

    // ── FlatMap ──────────────────────────────────────────────

    [Fact]
    public void FlatMap_Right_ChainsOperation()
    {
        var either = Either<string, int>.ToRight(10);

        var result = either.FlatMap(x => Either<string, string>.ToRight($"value: {x}"));

        Assert.True(result is Either<string, string>.Right);
        Assert.Equal("value: 10", result.Match(_ => "", x => x));
    }

    [Fact]
    public void FlatMap_Left_SkipsOperation()
    {
        var either = Either<string, int>.ToLeft("error");

        var result = either.FlatMap(x => Either<string, string>.ToRight("never"));

        Assert.True(result is Either<string, string>.Left);
        Assert.Equal("error", result.Match(x => x, _ => ""));
    }

    // ── Ensure ───────────────────────────────────────────────

    [Fact]
    public void Ensure_WhenPredicatePasses_KeepsRight()
    {
        var either = Either<string, int>.ToRight(5);

        var result = either.Ensure(x => x > 0, "must be positive");

        Assert.Equal(Either<string, int>.ToRight(5), result);
    }

    [Fact]
    public void Ensure_WhenPredicateFails_ReturnsLeft()
    {
        var either = Either<string, int>.ToRight(-1);

        var result = either.Ensure(x => x > 0, "must be positive");

        Assert.Equal(Either<string, int>.ToLeft("must be positive"), result);
    }

    [Fact]
    public void Ensure_WithErrorFactory_ReturnsLazyError()
    {
        var either = Either<string, int>.ToRight(-5);

        var result = either.Ensure(x => x > 0, x => $"value {x} is not positive");

        Assert.True(result is Either<string, int>.Left);
        Assert.Equal("value -5 is not positive", result.Match(x => x, _ => ""));
    }

    // ── MapLeft ──────────────────────────────────────────────

    [Fact]
    public void MapLeft_TransformsError()
    {
        var either = Either<int, string>.ToLeft(404);

        var result = either.MapLeft(code => $"HTTP {code}");

        Assert.True(result is Either<string, string>.Left);
        Assert.Equal("HTTP 404", result.Match(x => x, _ => ""));
    }

    // ── BiMap ────────────────────────────────────────────────

    [Fact]
    public void BiMap_MapsBothSides()
    {
        var right = Either<int, string>.ToRight("hello");
        var left = Either<int, string>.ToLeft(42);

        var r1 = right.BiMap(i => $"err:{i}", s => s.Length);
        var l1 = left.BiMap(i => $"err:{i}", s => s.Length);

        Assert.True(r1 is Either<string, int>.Right);
        Assert.Equal(5, r1.Match(_ => 0, x => x));

        Assert.True(l1 is Either<string, int>.Left);
        Assert.Equal("err:42", l1.Match(x => x, _ => ""));
    }

    // ── Tap ──────────────────────────────────────────────────

    [Fact]
    public void Tap_Right_ExecutesSideEffect()
    {
        var either = Either<string, int>.ToRight(42);
        var sideEffect = 0;

        var result = either.Tap(x => sideEffect = x);

        Assert.Equal(42, sideEffect);
        Assert.Equal(either, result);
    }

    [Fact]
    public void Tap_Left_DoesNotExecuteSideEffect()
    {
        var either = Either<string, int>.ToLeft("error");
        var sideEffect = 0;

        var result = either.Tap(x => sideEffect = x);

        Assert.Equal(0, sideEffect);
        Assert.Equal(either, result);
    }

    // ── Factory Either.Ok / Either.Fail ──────────────────────

    [Fact]
    public void Factory_Ok_CreatesRight()
    {
        var either = Either.Ok(42);

        Assert.True(either is Either<string, int>.Right);
        Assert.Equal(42, either.Match(_ => 0, x => x));
    }

    [Fact]
    public void Factory_Fail_CreatesLeft()
    {
        var either = Either.Fail<int>("something broke");

        Assert.True(either is Either<string, int>.Left);
        Assert.Equal("something broke", either.Match(x => x, _ => ""));
    }

    // ── LINQ ─────────────────────────────────────────────────

    [Fact]
    public void LinqSelect_TransformsRight()
    {
        var result = from x in Either<string, int>.ToRight(21)
                     select x * 2;

        Assert.Equal(Either<string, int>.ToRight(42), result);
    }

    [Fact]
    public void LinqSelectMany_WithMultipleFrom()
    {
        var result = from a in Either<string, int>.ToRight(3)
                     from b in Either<string, int>.ToRight(4)
                     select a * b;

        Assert.Equal(Either<string, int>.ToRight(12), result);
    }

    [Fact]
    public void LinqSelectMany_LeftShortCircuits()
    {
        var result = from a in Either<string, int>.ToRight(3)
                     from b in Either<string, int>.ToLeft("fail")
                     select a * b;

        Assert.Equal(Either<string, int>.ToLeft("fail"), result);
    }

    // ── Unit ─────────────────────────────────────────────────

    [Fact]
    public void Unit_CanBeCreated()
    {
        var unit = new Unit();
        Assert.NotNull(unit);
    }

    [Fact]
    public void Either_WithUnit_Ok_ReturnsRight()
    {
        var either = Either.Ok(new Unit());

        Assert.True(either is Either<string, Unit>.Right);
    }

    [Fact]
    public void Either_WithUnit_Fail_ReturnsLeft()
    {
        var either = Either.Fail<Unit>("operation failed");

        Assert.True(either is Either<string, Unit>.Left);
        Assert.Equal("operation failed", either.Match(x => x, _ => ""));
    }
}
