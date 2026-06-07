using Microsoft.AspNetCore.Mvc;

namespace EitherWay;

/// <summary>
/// Maps <see cref="Either{String,R}"/> results to standard ASP.NET Core HTTP responses.
/// Eliminates boilerplate try-catch and if-else in controller actions.
/// </summary>
public static class ControllerExtensions
{
    // ── Sync: Either<string, T> ───────────────────────────────

    /// <summary>
    /// Maps an Either result to an <see cref="ActionResult{T}"/>.
    /// Success → 200 OK; failure → error status (404/400) based on the message content.
    /// </summary>
    public static ActionResult<T> HandleResult<T>(this Either<string, T> result)
    {
        return result.Match<ActionResult<T>>(
            onLeft: error => (ActionResult<T>)MapToErrorResult(error),
            onRight: data => new OkObjectResult(data)
        );
    }

    /// <summary>
    /// Maps an Either result with a <see cref="Unit"/> (void) payload.
    /// Success → 204 No Content; failure → error status.
    /// </summary>
    public static IActionResult HandleResult(this Either<string, Unit> result)
    {
        return result.Match<IActionResult>(
            onLeft: MapToErrorResult,
            onRight: _ => new NoContentResult()
        );
    }

    /// <summary>
    /// Maps a successful creation to 201 Created with route values.
    /// </summary>
    /// <param name="result">The Either from the service layer.</param>
    /// <param name="routeName">The named route to retrieve the resource (e.g. "GetCompanyById").</param>
    /// <param name="idSelector">
    /// Selects route values from the created entity. Can return a primitive (maps to <c>id</c>)
    /// or an anonymous object for multi-parameter routes.
    /// </param>
    public static ActionResult<T> HandleCreated<T>(
        this Either<string, T> result,
        string routeName,
        Func<T, object> idSelector)
    {
        return result.Match<ActionResult<T>>(
            onLeft: error => (ActionResult<T>)MapToErrorResult(error),
            onRight: data =>
            {
                var selectorResult = idSelector(data);
                var routeValues = selectorResult.GetType().Name.Contains("AnonymousType")
                    ? selectorResult
                    : new { id = selectorResult };

                return new CreatedAtRouteResult(routeName, routeValues, data);
            }
        );
    }

    // ── Async: EitherAsync<string, T> ─────────────────────────

    /// <summary>
    /// Resolves the async Either and maps it to an <see cref="ActionResult{T}"/>.
    /// </summary>
    public static async Task<ActionResult<T>> HandleResultAsync<T>(this EitherAsync<string, T> asyncEither)
    {
        var result = await asyncEither.Run();
        return result.HandleResult();
    }

    /// <summary>
    /// Resolves the async Either with Unit and maps to <see cref="IActionResult"/>.
    /// Success → 204 No Content.
    /// </summary>
    public static async Task<IActionResult> HandleResultAsync(this EitherAsync<string, Unit> asyncEither)
    {
        var result = await asyncEither.Run();
        return result.HandleResult();
    }

    /// <summary>
    /// Resolves the async Either and maps a successful creation to 201 Created.
    /// </summary>
    public static async Task<ActionResult<T>> HandleCreatedAsync<T>(
        this EitherAsync<string, T> asyncEither,
        string routeName,
        Func<T, object> idSelector)
    {
        var result = await asyncEither.Run();
        return result.HandleCreated(routeName, idSelector);
    }

    // ── Utilities ─────────────────────────────────────────────

    /// <summary>
    /// Maps error messages to HTTP status codes.
    /// Messages containing "not found" or "not exist" → 404 NotFound.
    /// Everything else → 400 BadRequest.
    /// </summary>
    private static ObjectResult MapToErrorResult(string error)
    {
        return error switch
        {
            var msg when msg.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                          msg.Contains("not exist", StringComparison.OrdinalIgnoreCase)
                => new NotFoundObjectResult(new { Message = msg }),

            _ => new BadRequestObjectResult(new { Message = error })
        };
    }
}
