# EitherWay

**Errors as values. Not exceptions.**

EitherWay is a functional error-handling library for C# that eliminates try-catch sprawl and makes failure paths explicit in your method signatures. Built on the Either monad pattern, it brings Railway-Oriented Programming to .NET with minimal ceremony.

```
dotnet add package EitherWay
dotnet add package EitherWay.Http    (for ASP.NET Core)
```

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
public EitherAsync<string, Company> GetCompany(int id)
    => EitherAsync.Right(id)
        .Try(_ => _repo.GetById(id), ex => ex.Message)
        .Ensure(c => c != null, "Company not found");
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

EitherWay.Http/         ← ASP.NET Core integration
└── ControllerExtensions ← HandleResult, HandleCreated (sync + async)
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

### Factories

```csharp
Either.Ok(value)                          // Either<string, T> — Right
Either.Fail<T>("error")                   // Either<string, T> — Left
EitherAsync.Right(value)                  // EitherAsync<string, T>
EitherAsync.Left<T>("error")              // EitherAsync<string, T>
Either<L,R>.ToRight(value)                // Explicit type
Either<L,R>.ToLeft(error)                 // Explicit type
EitherAsync<L,R>.FromRight(value)         // Explicit async
EitherAsync<L,R>.FromLeft(error)          // Explicit async
EitherAsync<L,R>.Try(action, onError)     // Static try-catch wrapper
```

### Extensions on Either

| Method | Returns | What it does |
|---|---|---|
| `.Map(fn)` | `Either<L, T>` | Transform the Right value |
| `.FlatMap(fn)` | `Either<L, T>` | Chain an operation that returns Either |
| `.Ensure(pred, error)` | `Either<L, R>` | Guard clause — Left if predicate fails |
| `.Ensure(pred, errorFn)` | `Either<L, R>` | Guard clause with lazy error factory |
| `.MapLeft(fn)` | `Either<L2, R>` | Transform the Left value |
| `.BiMap(lFn, rFn)` | `Either<L2, R2>` | Transform both sides |
| `.Tap(action)` | `Either<L, R>` | Side effect on Right, value unchanged |
| `.Match(onLeft, onRight)` | `T` | Resolve the Either into a single value |

### Extensions on EitherAsync

| Method | Returns | What it does |
|---|---|---|
| `.Map(fn)` | `EitherAsync<L, T>` | Transform Right |
| `.FlatMap(fn)` | `EitherAsync<L, T>` | Chain async operation |
| `.Ensure(pred, error)` | `EitherAsync<L, R>` | Guard clause |
| `.Try(action, onError)` | `EitherAsync<L, T>` | Catch exceptions, receives Right value |
| `.Tap(action)` | `EitherAsync<L, R>` | Side effect |
| `.MapLeft(fn)` | `EitherAsync<L2, R>` | Transform Left |
| `.MatchAsync(onLeft, onRight)` | `Task<T>` | Resolve async Either |

### Controller extensions (EitherWay.Http)

| Method | HTTP Result | Use case |
|---|---|---|
| `result.HandleResult<T>()` | `ActionResult<T>` | GET with data |
| `result.HandleResult()` | `IActionResult` | With Unit → 204 No Content |
| `result.HandleCreated(route, fn)` | `ActionResult<T>` | POST → 201 Created |
| `asyncOp.HandleResultAsync<T>()` | `Task<ActionResult<T>>` | Async GET |
| `asyncOp.HandleResultAsync()` | `Task<IActionResult>` | Async command → 204 |
| `asyncOp.HandleCreatedAsync(...)` | `Task<ActionResult<T>>` | Async POST → 201 |

---

## Requirements

- .NET 10+
- C# 14+
- EitherWay.Http requires `Microsoft.AspNetCore.App` framework reference

## License

MIT
