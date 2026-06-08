# EitherWay

**Errors as values. Not exceptions.**

EitherWay is a functional error-handling library for C# that eliminates try-catch sprawl and makes failure paths explicit in your method signatures. Built on the Either monad pattern, it brings Railway-Oriented Programming to .NET with minimal ceremony.

```
dotnet add package EitherWay
```

Includes ASP.NET Core controller extensions (`HandleResult`, `HandleCreated`, async overloads) — no extra package needed.

---

## Philosophy

Exceptions are invisible control flow. They jump across layers, bubble up unexpectedly, and hide in places you don't expect. EitherWay flips that:

- **Errors are values.** A method that can fail says so in its return type.
- **No try-catch noise.** Business logic stays clean; error handling stays explicit.
- **Short-circuit by design.** Once something fails, the pipeline stops. You don't check `if (error)` at every step.
- **The compiler is your safety net.** If you don't handle the error case, it won't compile.

```csharp
// Before: exception-driven
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

// After: error-driven

// Option 1: static Try with MapLeft (recommended for new code)
public async Task<Either<string, Company>> GetCompany(int id)
    => await EitherAsync
        .Try(() => _repo.GetByIdAsync(id))
        .MapLeft(_ => "Database error")
        .Ensure(c => c != null, "Company not found")
        .Run();

// Option 2: extension Try with handler (no MapLeft needed)
public EitherAsync<string, Company> GetCompanyV2(int id)
    => EitherAsync.Right(id)
        .Try(_ => _repo.GetById(id), ex => ex.Message)
        .Ensure(c => c != null, "Company not found");

// Option 3: static Try with direct error value (cleanest when you don't need the exception)
public async Task<Either<string, Company>> GetCompanyV3(int id)
    => await EitherAsync
        .Try(() => _repo.GetByIdAsync(id), "Database error")
        .Ensure(c => c != null, "Company not found")
        .Run();
```

---

## Structure

```
EitherWay/              ← Core library (no dependencies)
├── Either<L, R>       ← Discriminated union: Left (error) / Right (success)
├── EitherAsync<L, R>  ← Lazy async: composes without executing until awaited
├── Unit                ← Void result for command operations
├── Fluent extensions   ← Map, FlatMap, Ensure, Try, Tap, MapLeft, BiMap
├── LINQ support        ← Select / SelectMany (from...select syntax)
└── Factories           ← Either.Ok, Either.Fail, EitherAsync.Right, EitherAsync.Left

ControllerExtensions  ← HandleResult, HandleCreated (sync + async) — built-in
```

---

## Quickstart

### Basic Either

```csharp
using EitherWay;

// Success
Either<string, int> ok = Either.Ok(42);

// Failure
Either<string, int> fail = Either.Fail<int>("something went wrong");

// Pattern match
var message = ok.Match(
    error => $"Error: {error}",
    value => $"Value: {value}");
```

### Async pipeline

```csharp
public EitherAsync<string, Company> CreateCompany(Company company)
{
    return EitherAsync.Right(company)
        .Ensure(c => !string.IsNullOrEmpty(c.Name), "Name is required")
        .Ensure(c => !string.IsNullOrEmpty(c.Address), "Address is required")
        .Try(async _ =>
        {
            var db = await _repo.CreateRecord(company);
            await _repo.Save();
            return db;
        }, ex => ex.Message);
}
```

### Controller

```csharp
[ApiController]
public class CompanyController : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<Company>> Create(Company company)
        => await _service.CreateCompany(company).HandleResultAsync();
}
```

### Void operations

```csharp
public EitherAsync<string, Unit> DeleteCompany(int id)
    => EitherAsync.Right(id)
        .Ensure(id => id > 0, "Invalid ID")
        .Try(_ => _repo.Delete(id), ex => ex.Message);

// In controller:
[HttpDelete("{id}")]
public async Task<IActionResult> Delete(int id)
    => await _service.DeleteCompany(id).HandleResultAsync();  // 204 No Content
```

### LINQ query syntax

```csharp
var result = from a in EitherAsync.Right(3)
             from b in EitherAsync.Right(4)
             select a * b;
// → Right(12)
```

---

## API Reference

### Core types

| Type | Description |
|---|---|
| `Either<L, R>` | Discriminated union. Left = error, Right = success. |
| `EitherAsync<L, R>` | Lazy async wrapper. Composes fluently, runs on await. |
| `Unit` | Void type for command operations (create, update, delete). |

---

### Factories

#### `Either.Ok(value)`

Creates a successful `Either<string, T>`. The error type defaults to `string`.

```csharp
Either<string, Company> result = Either.Ok(company);
```

#### `Either.Fail<T>(error)`

Creates a failed `Either<string, T>` with an error message.

```csharp
Either<string, int> result = Either.Fail<int>("invalid operation");
```

#### `EitherAsync.Right(value)`

Creates a lazy async `EitherAsync<string, T>` in the success track.

```csharp
EitherAsync<string, Company> op = EitherAsync.Right(company);
```

#### `EitherAsync.Left<T>(error)`

Creates a lazy async `EitherAsync<string, T>` in the error track.

```csharp
EitherAsync<string, int> op = EitherAsync.Left<int>("not found");
```

#### `Either<L, R>.ToRight(value)`

Creates a successful `Either<L, R>` with explicit types. Use when your error type is not `string`.

```csharp
Either<ErrorCode, int> result = Either<ErrorCode, int>.ToRight(42);
```

#### `Either<L, R>.ToLeft(error)`

Creates a failed `Either<L, R>` with explicit types.

```csharp
Either<ErrorCode, int> result = Either<ErrorCode, int>.ToLeft(ErrorCode.NotFound);
```

#### `EitherAsync<L, R>.FromRight(value)`

Creates a lazy async `EitherAsync<L, R>` in the success track with explicit types.

```csharp
EitherAsync<ErrorCode, int> op = EitherAsync<ErrorCode, int>.FromRight(42);
```

#### `EitherAsync<L, R>.FromLeft(error)`

Creates a lazy async `EitherAsync<L, R>` in the error track with explicit types.

```csharp
EitherAsync<ErrorCode, int> op = EitherAsync<ErrorCode, int>.FromLeft(ErrorCode.NotFound);
```

#### `EitherAsync.Try(action)` (static, no handler)

Safely executes an async operation without providing an error handler. The raw `Exception` becomes the Left value. Use `MapLeft` to project it to your error type.

```csharp
var result = await EitherAsync
    .Try(() => _repo.GetByIdAsync(id))
    .MapLeft(_ => "Database error")
    .Run();
```

#### `EitherAsync<L, R>.Try(action, onError)`

Safely executes an async operation. If it throws, the exception is caught and transformed into a Left value via `onError`. This is a static factory — use it to start a pipeline from a risky operation.

```csharp
var op = EitherAsync<string, int>.Try(
    () => httpClient.GetAsync("https://api.example.com/data"),
    ex => $"Request failed: {ex.Message}"
);
```

#### `EitherAsync.Try(action, error)` (static, direct error value)

Safely executes an async operation. If it throws, the exception is **discarded** and your error value is used directly as the Left. You don't need a handler — the error is provided upfront.

```csharp
var result = await EitherAsync
    .Try(() => _repo.GetByIdAsync(id), new AppError("Database error"))
    .Ensure(user => user is not null, new AppError("User not found"))
    .Run();
```

Use this when you don't care about the exception details — only that the operation failed. The `error` value is captured once at construction time.

---

### 🚨 Static `Try` vs Extension `Try` — clave para entender

Hay **dos familias** de métodos `Try` y es IMPORTANTE distinguirlas:

#### `EitherAsync.Try(...)` → estático, arranca un pipeline

Son métodos **estáticos** en la clase `EitherAsync`. **No reciben** un valor previo — inician el pipeline desde cero.

| Overload | Firma | Handler? |
|----------|-------|----------|
| Sin handler | `Try<R>(action)` | ❌ No — la Exception cruda es el Left |
| Handler | `Try<L,R>(action, handler)` | ✅ `Func<Exception, L>` |
| Error directo | `Try<L,R>(action, error)` | ❌ No — usás un valor fijo |

```csharp
// Arranca desde cero — no hay valor previo
EitherAsync.Try(() => _repo.GetByIdAsync(id))
```

#### `EitherAsync<L,R>.Try(...)` → extensión, CONTINÚA un pipeline

Son métodos de **extensión** sobre `EitherAsync<L,R>`. **Reciben** el valor Right del paso anterior y **necesitan handler** porque el tipo de error `L` ya está definido.

| Overload | Firma | Handler? |
|----------|-------|----------|
| Recibe valor | `Try<L,R,T>(this ..., Func<R,Task<T>>, Func<Exception,L>)` | ✅ Obligatorio |
| Ignora valor | `Try<L,R,T>(this ..., Func<Task<T>>, Func<Exception,L>)` | ✅ Obligatorio |

```csharp
// CONTINÚA — recibe el valor del paso anterior
EitherAsync.Right(companyId)
    .Try(async id => await _repo.GetById(id), ex => ex.Message)
```

#### Ejemplo real — CreateUser

```csharp
return await EitherAsync
    // ⬇️ Estático: arranca desde cero, NO recibe valor previo
    //     Puede NO llevar handler porque la Exception cruda es el Left
    .Try(() => unitOfWork.Users.GetByUsernameAsync(request.Username, ct))
    .MapLeft(ex => new AppError(ex.Message))
    .Ensure(user => user is not null, new AppError("username already exists"))
    .Map(user => request.Project())
    // ⬇️ Extensión: SÍ recibe el user del Map anterior
    //     DEBE llevar handler porque el tipo AppError ya está definido
    .Try(async user =>
    {
        user.PasswordHash = passwordHasher.Hash(request.Password);
        await unitOfWork.Users.AddUserAsync(user, ct);
        await unitOfWork.CommitAsync(ct);
        return user;
    }, exception => new AppError($"Failed to create user: {exception.Message}"))
    .Map(user => user.MapTo<User, UserDto>())
    .Run();
```

**Regla de oro:**
- Si **arrancás** un pipeline → `EitherAsync.Try(...)` estático (puede o no llevar handler)
- Si **seguís** un pipeline y la operación puede fallar → `.Try(...)` extensión (SIEMPRE lleva handler)

---

### Extensions on `Either`

#### `.Map(fn)`

Transforms the Right value with a synchronous function. If the state is Left, the function is skipped and the Left passes through unchanged.

```csharp
Either<string, int> ok = Either.Ok(21);
var doubled = ok.Map(x => x * 2);
// → Right(42)

Either<string, int> fail = Either.Fail<int>("error");
var doubled = fail.Map(x => x * 2);
// → Left("error") — unchanged
```

#### `.FlatMap(fn)`

Chains a function that returns a new `Either`. Use this when the next operation can also fail. If the current state is Left, the function is skipped.

```csharp
Either<string, int> ok = Either.Ok(10);
var chained = ok.FlatMap(x =>
    x > 5
        ? Either<string, string>.ToRight($"big: {x}")
        : Either<string, string>.ToLeft("too small")
);
// → Right("big: 10")
```

#### `.Ensure(predicate, error)`

Guard clause. If the Right value satisfies the predicate, it passes through. If not, the pipeline switches to Left with the given error. If the state is already Left, the predicate is skipped.

```csharp
Either<string, int> ok = Either.Ok(-5);
var result = ok.Ensure(x => x > 0, "must be positive");
// → Left("must be positive")
```

#### `.Ensure(predicate, errorFactory)`

Same as Ensure but the error is lazily created from the Right value. Use this when the error message depends on the value.

```csharp
Either<string, int> ok = Either.Ok(-5);
var result = ok.Ensure(x => x > 0, x => $"value {x} is invalid");
// → Left("value -5 is invalid")
```

#### `.Ensure(predicate, errorFactory)` — with `Func<L>`

Same as Ensure but the error factory doesn't take any parameters. Cleaner when the error doesn't depend on the Right value.

```csharp
Either<string, int> ok = Either.Ok(-5);
var result = ok.Ensure(x => x > 0, () => "must be positive");
// → Left("must be positive")
```

#### `.MapLeft(fn)`

Transforms the Left value while leaving the Right unchanged. Useful when you need to convert error types at layer boundaries.

```csharp
Either<int, string> either = Either<int, string>.ToLeft(404);
var result = either.MapLeft(code => $"HTTP {code}");
// → Left("HTTP 404")
```

#### `.BiMap(leftFn, rightFn)`

Transforms both Left and Right values simultaneously. You provide one function for each side.

```csharp
Either<int, string> right = Either<int, string>.ToRight("hello");
var r1 = right.BiMap(
    code => $"err:{code}",
    text => text.Length
);
// → Right(5)

Either<int, string> left = Either<int, string>.ToLeft(42);
var l1 = left.BiMap(
    code => $"err:{code}",
    text => text.Length
);
// → Left("err:42")
```

#### `.Tap(action)`

Executes a side-effect action if the state is Right. The Either value is returned unchanged. Use this for logging, metrics, or caching without breaking the chain.

```csharp
Either<string, int> ok = Either.Ok(42);
ok.Tap(x => Console.WriteLine($"Processing {x}"));
// → Still Right(42), side effect executed
```

#### `.Match(onLeft, onRight)`

Resolves the Either into a single value by providing handlers for both cases. This is the **only way** to extract the value. The compiler forces you to handle both paths.

```csharp
Either<string, int> either = Either.Ok(42);

// Returns a string regardless of Left or Right
var message = either.Match(
    left => $"Error: {left}",
    right => $"Value: {right}"
);
// → "Value: 42"

// In a controller — maps to different HTTP responses
return either.Match<IActionResult>(
    left => BadRequest(new { error = left }),
    right => Ok(right)
);
```

---

### Extensions on `EitherAsync`

#### `.Map(fn)`

Transforms the Right value with a synchronous function. If the pipeline is in the Left state, the function is skipped.

```csharp
var asyncOp = EitherAsync.Right(21);
var result = await asyncOp.Map(x => x * 2).Run();
// → Right(42)
```

#### `.FlatMap(fn)`

Chains an async operation that returns a new `Either`. Accepts both sync and async functions.

```csharp
// Async
var result = await asyncOp
    .FlatMap(x => Task.FromResult(Either<string, string>.ToRight($"value: {x}")))
    .Run();

// Sync (the library provides the overload)
var result = await asyncOp
    .FlatMap(x => Either<string, string>.ToRight($"value: {x}"))
    .Run();
```

#### `.Ensure(predicate, error)`

Guard clause for async pipelines. If the predicate fails, the pipeline switches to Left.

```csharp
var result = await EitherAsync.Right(-5)
    .Ensure(x => x > 0, "must be positive")
    .Run();
// → Left("must be positive")
```

#### `.Ensure(predicate, errorFactory)`

Guard clause with a lazy error factory that receives the Right value.

```csharp
var result = await EitherAsync.Right(-5)
    .Ensure(x => x > 0, x => $"value {x} is invalid")
    .Run();
// → Left("value -5 is invalid")
```

#### `.Ensure(predicate, errorFactory)` — with `Func<L>`

Same as above but the error factory doesn't take any parameters. Useful when the error doesn't depend on the Right value and you want a cleaner lambda.

```csharp
var result = await EitherAsync.Right(-5)
    .Ensure(x => x > 0, () => "must be positive")
    .Run();
// → Left("must be positive")
```

#### `.Try(action, onError)`

Safely executes an async operation as part of a pipeline. The action receives the Right value from the previous step. If it throws, the exception is caught and mapped to a Left. Use `_` as the parameter name when you don't need the incoming value.

```csharp
// Receives the previous value
var result = await EitherAsync.Right(companyId)
    .Try(async id => await _repo.GetById(id), ex => ex.Message)
    .Run();

// Ignores the previous value
var result = await EitherAsync.Right(company)
    .Try(async _ => {
        var db = await _repo.CreateRecord(company);
        await _repo.Save();
        return db;
    }, ex => ex.Message)
    .Run();
```

#### `.Try(action, onError)` (overload without input)

Same as above but the action doesn't receive the previous value.

```csharp
var result = await EitherAsync.Right("ignored")
    .Try(async () => await _repo.GetCount(), ex => ex.Message)
    .Run();
```

#### `EitherAsync.Try(action)` (static, no handler)

Safely executes an async operation without providing an error handler. The raw `Exception` becomes the Left value. Chain `.MapLeft` to project it to your error type.

```csharp
var result = await EitherAsync
    .Try(() => _repo.GetByIdAsync(id))
    .MapLeft(_ => "Database error")
    .Ensure(user => user is not null, () => "User not found")
    .Run();
```

#### `EitherAsync.Try(action, error)` (static, direct error value)

Safely executes an async operation. If it throws, the exception is **discarded** and your error value is used directly as the Left. This is the cleanest option when you don't need the exception details.

```csharp
var result = await EitherAsync
    .Try(() => _repo.GetByIdAsync(id), "Database error")
    .Ensure(user => user is not null, "User not found")
    .Run();
```

#### `.MapLeft(fn)`

Transforms the Left value in an async pipeline.

```csharp
var result = await EitherAsync.Left<int>(404)
    .MapLeft(code => $"HTTP {code}")
    .Run();
// → Left("HTTP 404")
```

#### `.BiMap(leftFn, rightFn)`

Transforms both Left and Right in an async pipeline.

```csharp
var result = await EitherAsync.Right(21)
    .BiMap(
        error => $"err: {error}",
        value => value * 2
    )
    .Run();
// → Right(42)
```

#### `.Tap(action)`

Executes a side-effect on the Right value without transforming it.

```csharp
await EitherAsync.Right(42)
    .Tap(x => Console.WriteLine($"Processing {x}"))
    .Run();
// Still Right(42), side effect executed
```

#### `.MatchAsync(onLeft, onRight)`

Resolves an `EitherAsync` by running the pipeline and matching the result. Available with sync or async handlers.

```csharp
// Sync handlers
var message = await asyncOp.MatchAsync(
    error => $"Error: {error}",
    value => $"Value: {value}"
);

// Async handlers
var httpResult = await asyncOp.MatchAsync(
    async error => {
        await _logger.LogErrorAsync(error);
        return StatusCode(500, new { error });
    },
    value => Ok(value)  // sync is fine too
);
```

#### `.Run()`

Executes the lazy async pipeline and returns the raw `Either<L, R>`. Use this when you want to pass the result to another function or use Match directly.

```csharp
var either = await asyncOp.Run();
// either is Either<string, Company> — call Match on it
```

---

### LINQ extensions

#### `Select` / `SelectMany`

Enables C# query comprehension syntax (`from...select`). Works on both `Either` and `EitherAsync`.

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

// Short-circuit on failure
var result = from a in Either<string, int>.ToRight(3)
             from b in Either<string, int>.ToLeft("fail")
             select a * b;
// → Left("fail") — second from fails, select is skipped
```

#### `Where`

Filters the Right value. If the predicate fails, the Either switches to Left with the provided error.

```csharp
var result = from x in Either<string, int>.ToRight(5)
             where x > 10  // needs explicit error
             select x;
```

Note: `Where` on Either/EitherAsync requires an explicit error value (unlike LINQ to Objects) because there's no default error to use when the predicate fails.

```csharp
var result = from x in Either<string, int>.ToRight(5)
             where x > 10 select "too small"
             select x;
```

---

### Controller extensions (built-in)

#### `HandleResult<T>()`

Maps an `Either<string, T>` to `ActionResult<T>`. Right → 200 OK with the value. Left → 400 BadRequest or 404 NotFound depending on the error message content.

```csharp
[HttpGet("{id}")]
public ActionResult<Company> Get(int id)
    => _service.GetCompany(id).HandleResult();
```

#### `HandleResult()` (for Unit)

Maps an `Either<string, Unit>` to `IActionResult`. Right → 204 No Content. Left → error response.

```csharp
[HttpDelete("{id}")]
public IActionResult Delete(int id)
    => _service.DeleteCompany(id).HandleResult();
```

#### `HandleCreated(routeName, idSelector)`

Maps a successful creation to 201 Created with a Location header. The `idSelector` extracts route values for the `CreatedAtRoute` result.

```csharp
[HttpPost]
public ActionResult<Company> Create(Company company)
{
    return _service.CreateCompany(company).HandleCreated(
        routeName: "GetCompany",          // the named route for retrieval
        idSelector: c => c.Id             // "{id}" in the route
    );
}

// For routes with multiple parameters:
return service.Create(order).HandleCreated(
    routeName: "GetOrderItem",
    idSelector: o => new { orderId = o.OrderId, itemId = o.ItemId }
);
```

#### `HandleResultAsync<T>()`

Async version of `HandleResult<T>()`. Calls `Run()` internally and maps the result.

```csharp
[HttpGet("{id}")]
public async Task<ActionResult<Company>> Get(int id)
    => await _service.GetCompanyAsync(id).HandleResultAsync();
```

#### `HandleResultAsync()` (for Unit)

Async version for Unit results.

```csharp
[HttpDelete("{id}")]
public async Task<IActionResult> Delete(int id)
    => await _service.DeleteCompanyAsync(id).HandleResultAsync();
```

#### `HandleCreatedAsync(routeName, idSelector)`

Async version of `HandleCreated`.

```csharp
[HttpPost]
public async Task<ActionResult<Company>> Create(Company company)
    => await _service.CreateCompanyAsync(company)
        .HandleCreatedAsync("GetCompany", c => c.Id);
```

#### Error mapping logic

The library automatically maps error messages to HTTP status codes:

| Error contains | HTTP Status |
|---|---|
| `"not found"` or `"not exist"` | 404 NotFound |
| Everything else | 400 BadRequest |

```json
// 400 BadRequest
{ "message": "Name is required" }

// 404 NotFound
{ "message": "Company not found" }
```

---

## Requirements

- .NET 10+
- C# 14+
## License

MIT
