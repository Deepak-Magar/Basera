using Basera.Shared.Results;
using Microsoft.AspNetCore.Http;

namespace Basera.Shared.Web;

/// <summary>
/// The JSON shape of every <c>/api</c> response, success or failure:
/// <c>{ "success": true, "data": {...}, "error": null, "correlationId": "..." }</c>. Clients check
/// <c>success</c>, then read <c>data</c> or <c>error.code</c> (one of the <see cref="ErrorCode"/> wire codes).
/// </summary>
public sealed record ApiEnvelope<T>(bool Success, T? Data, ApiError? Error, string? CorrelationId);

/// <summary><paramref name="Target"/> names the input a validation or rule error is about (e.g. "phone").</summary>
public sealed record ApiError(string Code, string Message, string? Target = null);

public static class ApiResults
{
    public static IResult Ok<T>(T data, HttpContext? http = null) =>
        TypedResults.Ok(new ApiEnvelope<T>(true, data, null, http?.TraceIdentifier));

    public static IResult Fail(DomainError error, HttpContext? http = null) =>
        TypedResults.Json(
            new ApiEnvelope<object>(false, null, new ApiError(error.Code.ToWireCode(), error.Message, error.Target), http?.TraceIdentifier),
            statusCode: error.Code.ToHttpStatus());

    /// <summary>What module endpoints return: 200 with the value, or the error's status with the envelope.</summary>
    public static IResult ToApiResult<T>(this Result<T> result, HttpContext? http = null) =>
        result.IsSuccess ? Ok(result.Value, http) : Fail(result.Error!, http);

    public static IResult ToApiResult(this Result result, HttpContext? http = null) =>
        result.IsSuccess ? Ok<object?>(null, http) : Fail(result.Error!, http);

    /// <summary>The single mapping from <see cref="ErrorCode"/> to HTTP status.</summary>
    public static int ToHttpStatus(this ErrorCode code) => code switch
    {
        ErrorCode.Validation => StatusCodes.Status400BadRequest,
        ErrorCode.Forbidden => StatusCodes.Status403Forbidden,
        ErrorCode.NotFound => StatusCodes.Status404NotFound,
        ErrorCode.Conflict => StatusCodes.Status409Conflict,
        ErrorCode.RuleViolation => StatusCodes.Status422UnprocessableEntity,
        ErrorCode.Internal => StatusCodes.Status500InternalServerError,
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, null),
    };

    /// <summary>The reverse, for status codes produced outside a service (404 route, 429 rate limit).</summary>
    public static (string Code, string Message) ForStatus(int status) => status switch
    {
        StatusCodes.Status400BadRequest => (ErrorCode.Validation.ToWireCode(), "The request is not valid."),
        StatusCodes.Status401Unauthorized => ("UNAUTHORIZED", "Sign in to continue."),
        StatusCodes.Status403Forbidden => (ErrorCode.Forbidden.ToWireCode(), "You may not do this."),
        StatusCodes.Status404NotFound => (ErrorCode.NotFound.ToWireCode(), "Not found."),
        StatusCodes.Status405MethodNotAllowed => ("METHOD_NOT_ALLOWED", "This method is not allowed here."),
        StatusCodes.Status409Conflict => (ErrorCode.Conflict.ToWireCode(), "The request conflicts with the current state."),
        StatusCodes.Status429TooManyRequests => ("RATE_LIMITED", "Too many requests. Wait a moment and try again."),
        _ => (ErrorCode.Internal.ToWireCode(), "Something went wrong."),
    };

    /// <summary>True for <c>/api</c> and everything below it.</summary>
    public static bool IsApiRequest(this HttpRequest request) => IsApiPath(request.Path);

    public static bool IsApiPath(PathString path) => path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase);
}
