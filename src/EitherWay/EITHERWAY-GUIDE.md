# EitherWay — C# Guide

**Errors as values. Not exceptions.**

EitherWay is a functional error-handling library for C# that eliminates try-catch sprawl and makes failure paths explicit in your method signatures. Built on the Either monad pattern, it brings Railway-Oriented Programming to .NET with minimal ceremony.

```
dotnet add package EitherWay
```

Includes ASP.NET Core controller extensions (`HandleResult`, `HandleCreated`, async overloads) — no extra package needed.

---

## Table of Contents

1. [Philosophy](#philosophy)
2. [Core Types](#core-types)
3. [Either — Sync API](#either--sync-api)
4. [EitherAsync — Async API](#eitherasync--async-api)
5. [Pattern Comparison](#pattern-comparison)
6. [🚨 Static Try vs Extension Try — clave](#-static-try-vs-extension-try--clave)
7. [Controller Extensions](#controller-extensions)
8. [LINQ Support](#linq-support)
9. [Migration Guide](#migration-guide)
10. [FAQ](#faq)
11. [API Reference](#api-reference)

---

## Philosophy

Exceptions are invisible control flow. They jump across layers, bubble up unexpectedly, and hide in places you don't expect. EitherWay flips that:

- **Errors are values.** A method that can fail says so in its return type.
- **No try-catch noise.** Business logic stays clean; error handling stays explicit.
- **Short-circuit by design.** Once something fails, the pipeline stops. You don't check `if (error)` at every step.
- **The compiler is your safety net.** If you don't handle the error case, it won't compile.

### Types vs Exceptions

| | Exceptions | EitherWay |
|---|---|---|
| **Signature** | `Company Get(int id)` | `Either<Error, Company> Get(int id)` |
| **Control flow** | Invisible — jumps up the call stack | Visible — follows the pipeline |
| **Composition** | try-catch nesting | Map, FlatMap, Ensure, Try |
| **Error handling** | Optional — can be missed | Mandatory — compiler forces Match |
| **Async** | try-catch in every method | EitherAsync composes like sync |

---

## Core Types

### `Either<L, R>`

A discriminated union that can be in one of two states:

- **`Left<L>`** — error, holds a value of type `L`
- **`Right<R>`** — success, holds a value of type `R`

```csharp
public abstract record Either<L, R>
{
    public sealed record Left(L Value) : Either<L, R>;
    public sealed record Right(R Value) : Either<L, R>;
}
```

The naming comes from the mathematical tradition: the **Right** value is the correct one, the **Left** value is wrong.

### `EitherAsync<L, R>`

A lazy wrapper around `Func<Task<Either<L, R>>>`. The wrapped function is NOT executed until `.Run()` is called, making it safe to compose pipelines without triggering side effects early.

```csharp
public class EitherAsync<L, R>
{
    // Lazy: nothing executes until you await .Run()
    public Task<Either<L, R>> Run();
}
```

### `Unit`

A void type for command operations (create, update, delete) that have no return value.

```csharp
public record Unit;
```

---

## Either — Sync API

### Construction

```csharp
using EitherWay;

// Success (L defaults to string)
Either<string, int> ok = Either.Ok(42);

// Failure
Either<string, int> fail = Either.Fail<int>("something went wrong");

// Explicit types
Either<ErrorCode, int> result = Either<ErrorCode, int>.ToRight(42);
Either<ErrorCode, int> error = Either<ErrorCode, int>.ToLeft(ErrorCode.NotFound);
```

### `.Match(onLeft, onRight)` — Pattern matching

The ONLY way to extract the value. The compiler forces you to handle both cases.

```csharp
var message = result.Match(
    error => $"Error: {error}",
    value => $"Value: {value}");

// In a controller — maps to different HTTP responses
return result.Match<IActionResult>(
    left => BadRequest(new { error = left }),
    right => Ok(right));
```

### `.Map(fn)` — Transform success

```csharp
Either.Ok(21).Map(x => x * 2);                // Right(42)
Either.Fail<int>("err").Map(x => x * 2);       // Left("err") — unchanged
```

### `.FlatMap(fn)` — Chain computations

```csharp
var chained = Either.Ok(10).FlatMap(x =>
    x > 5
        ? Either<string, string>.ToRight($"big: {x}")
        : Either<string, string>.ToLeft("too small"));
// Right("big: 10")
```

### `.MapLeft(fn)` — Transform error

```csharp
Either<ErrorCode, int>.ToLeft(ErrorCode.NotFound)
    .MapLeft(code => $"HTTP {code}");
// Left("HTTP 404")
```

### `.BiMap(leftFn, rightFn)` — Map both sides

```csharp
Either<int, string>.ToRight("hello").BiMap(
    code => $"err:{code}",
    text => text.Length);
// Right(5)
```

### `.Ensure(predicate, error)` — Guard clause

Three overloads available:

```csharp
// 1. Direct error value (recommended)
Either.Ok(-5).Ensure(x => x > 0, "must be positive");
// Left("must be positive")

// 2. Lazy factory with the Right value
Either.Ok(-5).Ensure(x => x > 0, x => $"value {x} is invalid");
// Left("value -5 is invalid")

// 3. Lazy factory without parameters
Either.Ok(-5).Ensure(x => x > 0, () => "must be positive");
// Left("must be positive")
```

### `.Tap(action)` — Side effects

```csharp
Either.Ok(42).Tap(x => Console.WriteLine($"Processing {x}"));
// Still Right(42), side effect executed
```

---

## EitherAsync — Async API

EitherAsync mirrors the entire Either API but for async operations. Every method returns a new EitherAsync without executing — call `.Run()` at the end.

### Construction

```csharp
// Factory with string error default
EitherAsync.Right(42);                          // EitherAsync<string, int>
EitherAsync.Left<int>("not found");              // EitherAsync<string, int>

// Explicit types
EitherAsync<ErrorCode, int>.FromRight(42);
EitherAsync<ErrorCode, int>.FromLeft(ErrorCode.NotFound);
```

### `EitherAsync.Try()` — Static factories (start a pipeline)

#### `Try(action)` — no handler

Safely executes an async operation. The raw `Exception` becomes the Left value. Use `MapLeft` to project it.

```csharp
var result = await EitherAsync
    .Try(() => _repo.GetByIdAsync(id))
    .MapLeft(_ => "Database error")
    .Run();
```

#### `Try(action, error)` — direct error value

The exception is **discarded** and your error value is used directly as the Left. No handler needed.

```csharp
var result = await EitherAsync
    .Try(() => _repo.GetByIdAsync(id), new AppError("Database error"))
    .Ensure(user => user is not null, new AppError("User not found"))
    .Run();
```

#### `Try(action, handler)` — with error handler

You control how the exception is mapped to your error type.

```csharp
var op = EitherAsync<string, int>.Try(
    () => httpClient.GetAsync("https://api.example.com/data"),
    ex => $"Request failed: {ex.Message}");
```

### `.Map(fn)` — Transform success

```csharp
var result = await EitherAsync.Right(21)
    .Map(x => x * 2)
    .Run();
// Right(42)
```

### `.FlatMap(fn)` — Chain async operations

Accepts both sync and async functions:

```csharp
// Async
await asyncOp.FlatMap(x => Task.FromResult(Either<string, string>.ToRight($"value: {x}"))).Run();

// Sync
await asyncOp.FlatMap(x => Either<string, string>.ToRight($"value: {x}")).Run();
```

### `.FlatMap(action, onError)` — Extension (continue a pipeline)

Safely executes an async operation as part of a pipeline. The action receives the Right value from the previous step. **Must** include a handler because the error type `L` is already defined.

```csharp
// Receives the previous value
var result = await EitherAsync.Right(companyId)
    .FlatMap(async id => await _repo.GetById(id), ex => ex.Message)
    .Run();
```

### 🚨 Static `Try` vs Extension `FlatMap` — cómo distinguirlos

Son **dos métodos distintos** con propósitos diferentes:

| Característica | `EitherAsync.Try()` (estático) | `.FlatMap()` (extensión) |
|---|---|---|
| **Rol** | Arranca un pipeline desde cero | Continúa un pipeline existente |
| **Recibe valor previo?** | ❌ No | ✅ Sí |
| **Handler?** | ❌ Opcional (según overload) | ✅ Obligatorio |
| **Tipo de error** | Se define acá | Ya está definido |

#### Ejemplo real — CreateUser

```csharp
return await EitherAsync
    // ⬇️ Estático: arranca desde cero, puede NO llevar handler
    .Try(() => unitOfWork.Users.GetByUsernameAsync(request.Username, ct))
    .MapLeft(ex => new AppError(ex.Message))
    .Ensure(user => user is not null, new AppError("username already exists"))
    .Map(user => request.Project())
    // ⬇️ Extensión FlatMap: recibe el user, DEBE llevar handler
    .FlatMap(async user =>
    {
        user.PasswordHash = passwordHasher.Hash(request.Password);
        await unitOfWork.Users.AddUserAsync(user, ct);
        await unitOfWork.CommitAsync(ct);
        return user;
    }, exception => new AppError($"Failed to create user: {exception.Message}"))
    .Map(user => user.MapTo<User, UserDto>())
    .Run();
```

### `.Ensure(predicate, error)` — Async guard clause

Same three overloads as the sync version:

```csharp
// Direct error value
await EitherAsync.Right(-5)
    .Ensure(x => x > 0, "must be positive")
    .Run();

// Factory with value
await EitherAsync.Right(-5)
    .Ensure(x => x > 0, x => $"value {x} is invalid")
    .Run();

// Factory without params
await EitherAsync.Right(-5)
    .Ensure(x => x > 0, () => "must be positive")
    .Run();
```

### `.MapLeft(fn)` / `.BiMap(leftFn, rightFn)`

```csharp
var result = await EitherAsync.Left<int>(404)
    .MapLeft(code => $"HTTP {code}")
    .Run();
// Left("HTTP 404")

var result = await EitherAsync.Right(21)
    .BiMap(error => $"err: {error}", value => value * 2)
    .Run();
// Right(42)
```

### `.Tap(action)` — Side effects

```csharp
await EitherAsync.Right(42)
    .Tap(x => Console.WriteLine($"Processing {x}"))
    .Run();
// Still Right(42), side effect executed
```

### `.MatchAsync(onLeft, onRight)` — Execute and match

```csharp
// Sync handlers
var message = await asyncOp.MatchAsync(
    error => $"Error: {error}",
    value => $"Value: {value}");

// Async handlers
var httpResult = await asyncOp.MatchAsync(
    async error => {
        await _logger.LogErrorAsync(error);
        return StatusCode(500, new { error });
    },
    value => Ok(value));
```

### `.Run()` — Execute pipeline

```csharp
var either = await asyncOp.Run();
// either is Either<L, R> — call Match on it
```

---

## Pattern Comparison

### Option 1: `Try(action)` + `MapLeft` (recommended for new code)

```csharp
var result = await EitherAsync
    .Try(() => _repo.GetByIdAsync(id))
    .MapLeft(_ => "Database error")
    .Ensure(c => c != null, "Company not found")
    .Run();
```

**Pros:** Single `MapLeft` projects the exception once. Clean pipeline.

**Cons:** Requires chaining `MapLeft` even for simple cases.

### Option 2: `FlatMap(action, handler)` — inline error mapping

```csharp
public EitherAsync<string, Company> GetCompanyV2(int id)
    => EitherAsync.Right(id)
        .FlatMap(_ => _repo.GetById(id), ex => ex.Message)
        .Ensure(c => c != null, "Company not found");
```

**Pros:** Error is resolved immediately. Good when you need the exception message.

**Cons:** Lambda for the handler can feel verbose.

### Option 3: `Try(action, error)` — direct value (cleanest)

```csharp
var result = await EitherAsync
    .Try(() => _repo.GetByIdAsync(id), new AppError("Database error"))
    .Ensure(c => c != null, new AppError("Company not found"))
    .Run();
```

**Pros:** Most concise. No handler, no MapLeft.

**Cons:** The error is fixed — you can't include exception details.

### Summary

| Pattern | Exception included? | Boilerplate |
|---------|-------------------|-------------|
| `Try` + `MapLeft` | ✅ Yes (via exception reference) | Medium |
| `Try(action, handler)` | ✅ Yes (via handler parameter) | Medium |
| `Try(action, error)` | ❌ No (exception discarded) | Low |

---

## Controller Extensions

EitherWay includes built-in ASP.NET Core controller extensions — no extra package needed.

### `HandleResult<T>()`

```csharp
[HttpGet("{id}")]
public ActionResult<Company> Get(int id)
    => _service.GetCompany(id).HandleResult();
```

### `HandleResult()` (for Unit)

```csharp
[HttpDelete("{id}")]
public IActionResult Delete(int id)
    => _service.DeleteCompany(id).HandleResult();  // 204 No Content
```

### `HandleCreated(routeName, idSelector)`

```csharp
[HttpPost]
public ActionResult<Company> Create(Company company)
    => _service.CreateCompany(company).HandleCreated(
        routeName: "GetCompany",
        idSelector: c => c.Id);
```

### `HandleResultAsync<T>()` and `HandleCreatedAsync()`

Same as above but async — call `.Run()` internally.

### Error mapping logic

| Error contains | HTTP Status |
|---|---|
| `"not found"` or `"not exist"` | 404 NotFound |
| Everything else | 400 BadRequest |

---

## LINQ Support

Enable C# query comprehension syntax on both `Either` and `EitherAsync`:

```csharp
// Synchronous
var result = from a in Either<string, int>.ToRight(3)
             from b in Either<string, int>.ToRight(4)
             select a * b;
// → Right(12)

// Asynchronous
var result = await (from a in EitherAsync.Right(3)
                    from b in EitherAsync.Right(4)
                    select a * b).Run();
// → Right(12)
```

---

## Migration Guide

### From try-catch to Either

**Before:**

```csharp
public async Task<Company> GetCompany(int id)
{
    try
    {
        var company = await _repo.GetById(id);
        if (company == null)
            throw new NotFoundException("Company not found");
        return company;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to get company");
        throw;
    }
}
```

**After:**

```csharp
public async Task<Either<string, Company>> GetCompany(int id)
    => await EitherAsync
        .Try(() => _repo.GetByIdAsync(id))
        .MapLeft(_ => "Database error")
        .Ensure(c => c != null, "Company not found")
        .Run();
```

### From null checks to Ensure

**Before:**

```csharp
var user = await repo.GetById(id);
if (user == null) return null;
if (!user.Active) return null;
return user;
```

**After:**

```csharp
return await EitherAsync
    .Try(() => repo.GetByIdAsync(id))
    .MapLeft(_ => "Database error")
    .Ensure(u => u != null, "User not found")
    .Ensure(u => u.Active, "Inactive user")
    .Run();
```

### From exception-driven to error-driven controllers

**Before:**

```csharp
[HttpGet("{id}")]
public async Task<IActionResult> Get(int id)
{
    try
    {
        var company = await _service.GetCompany(id);
        return Ok(company);
    }
    catch (NotFoundException ex)
    {
        return NotFound(new { message = ex.Message });
    }
    catch (Exception ex)
    {
        return StatusCode(500, new { message = "Internal error" });
    }
}
```

**After:**

```csharp
[HttpGet("{id}")]
public async Task<ActionResult<Company>> Get(int id)
    => await _service.GetCompanyAsync(id).HandleResultAsync();
```

---

## FAQ

### When should I use `Either` vs `EitherAsync`?

- **`Either`**: when you have the value right now (sync computation, validation, transformation).
- **`EitherAsync`**: when the value comes from an async source (database, HTTP request, file read).

If you have an `EitherAsync` but need to call a sync function, use `.Map()`. If you have an `Either` but need an `EitherAsync`, create one with `EitherAsync<L,R>.FromRight(value)` or use the static `Try` with a pre-resolved value.

### What's the difference between the three `Ensure` overloads?

```csharp
// 1. Direct value — error is known upfront
Ensure(x => x > 0, "must be positive")

// 2. Factory with value — error depends on the Right value
Ensure(x => x > 0, x => $"value {x} is invalid")

// 3. Factory without params — lazy but doesn't depend on value
Ensure(x => x > 0, () => "must be positive")
```

Use #1 when possible — it's the most concise. Use #2 when the error message includes the value. Use #3 when the error is expensive to create.

### What's the difference between `EitherAsync.Try()` (static) and `.FlatMap()` (extension)?

The **static** `Try` starts a pipeline from scratch. The **extension** `FlatMap` continues an existing pipeline and receives the previous value. See the [comparison section](#-static-try-vs-extension-flatmap--cómo-distinguirlos).

### Can I mix error types in a pipeline?

**No.** C# does not support union types. Once `L` is set (e.g. `string`), all subsequent `Ensure`, `MapLeft`, and `FlatMap` calls must use the **same type**. If you need a different error type, convert it with `.MapLeft()` earlier in the chain.

```csharp
// ✅ Correct: MapLeft projects the error type before Ensure
var result = await EitherAsync
    .Try(() => repo.GetByIdAsync(id), ex => ex.Message)  // L = string
    .MapLeft(msg => new AppError(msg))                    // L = AppError
    .Ensure(u => u != null, new AppError("not found"))    // L = AppError ✓
    .Run();

// ❌ Wrong: Ensure uses AppError while L is still string
var result = await EitherAsync
    .Try(() => repo.GetByIdAsync(id), ex => ex.Message)  // L = string
    .Ensure(u => u != null, new AppError("not found"))    // ❌ compile error
    .Run();
```

### Does the library have any dependencies?

Zero runtime dependencies. It includes a `FrameworkReference` to `Microsoft.AspNetCore.App` (which comes with the SDK) for the controller extensions — no extra NuGet packages needed.

---

## API Reference

### Factories

| Method | Returns | Description |
|--------|---------|-------------|
| `Either.Ok(value)` | `Either<string, T>` | Success (L = string) |
| `Either.Fail<T>(error)` | `Either<string, T>` | Error (L = string) |
| `Either<L, R>.ToRight(value)` | `Either<L, R>` | Success with explicit types |
| `Either<L, R>.ToLeft(error)` | `Either<L, R>` | Error with explicit types |
| `EitherAsync.Right(value)` | `EitherAsync<string, T>` | Async success |
| `EitherAsync.Left<T>(error)` | `EitherAsync<string, T>` | Async error |
| `EitherAsync<L,R>.FromRight(value)` | `EitherAsync<L, R>` | Async success explicit |
| `EitherAsync<L,R>.FromLeft(error)` | `EitherAsync<L, R>` | Async error explicit |

### Static `Try` (start pipeline)

| Method | Returns | Description |
|--------|---------|-------------|
| `Try(action)` | `EitherAsync<Exception, R>` | No handler, raw Exception |
| `Try(action, error)` | `EitherAsync<L, R>` | Direct error value |
| `Try(action, handler)` | `EitherAsync<L, R>` | With error handler |

### Either extensions (sync)

| Method | Returns | Description |
|--------|---------|-------------|
| `Map(fn)` | `Either<L, T>` | Transform Right |
| `FlatMap(fn)` | `Either<L, T>` | Chain Either-returning fn |
| `MapLeft(fn)` | `Either<T, R>` | Transform Left |
| `BiMap(left, right)` | `Either<T, U>` | Map both sides |
| `Ensure(predicate, error)` | `Either<L, R>` | Guard clause |
| `Tap(action)` | `Either<L, R>` | Side effect on Right |
| `Match(left, right)` | `T` | Pattern match |

### EitherAsync extensions (async)

| Method | Returns | Description |
|--------|---------|-------------|
| `Map(fn)` | `EitherAsync<L, T>` | Transform Right |
| `FlatMap(fn)` | `EitherAsync<L, T>` | Chain Either-returning fn |
| `FlatMap(action, handler)` | `EitherAsync<L, T>` | Continue with exception safety |
| `MapLeft(fn)` | `EitherAsync<T, R>` | Transform Left |
| `BiMap(left, right)` | `EitherAsync<T, U>` | Map both sides |
| `Ensure(predicate, error)` | `EitherAsync<L, R>` | Guard clause |
| `Tap(action)` | `EitherAsync<L, R>` | Side effect |
| `MatchAsync(left, right)` | `Task<T>` | Execute and match |
| `Run()` | `Task<Either<L, R>>` | Execute pipeline |

---

## License

MIT
