using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace Basera.Shared.Web;

/// <summary>
/// The one exception handler (ARCHITECTURE §10). The exception middleware has already logged the error, with
/// the correlation id in scope. This decides how the user sees it:
/// <list type="bullet">
/// <item><c>/api</c>: a 500 envelope with code INTERNAL.</item>
/// <item>HTMX fragment request: a 500 with <c>HX-Reswap: none</c>, so the page stays as it is, and an
/// <c>HX-Trigger</c> event that site.js shows as a message.</item>
/// <item>Anything else: not handled here, so the middleware re-executes <c>/error</c> for the full error page.</item>
/// </list>
/// Exception details are shown only in Development.
/// </summary>
public sealed class BaseraExceptionHandler(IHostEnvironment environment) : IExceptionHandler
{
    public const string HtmxErrorEvent = "basera:error";

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var message = environment.IsDevelopment()
            ? $"{exception.GetType().Name}: {exception.Message}"
            : "Something went wrong. Please try again.";

        // The middleware has already rewritten Request.Path to "/error"; decide on the path that failed.
        var originalPath = httpContext.Features.Get<IExceptionHandlerPathFeature>()?.Path ?? httpContext.Request.Path;
        if (ApiResults.IsApiPath(originalPath))
        {
            httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await httpContext.Response.WriteAsJsonAsync(
                new ApiEnvelope<object>(false, null, new ApiError("INTERNAL", message), httpContext.TraceIdentifier),
                cancellationToken);
            return true;
        }

        if (httpContext.Request.IsHtmx() && !httpContext.Request.IsHtmxBoosted())
        {
            httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
            httpContext.Response.Headers[HtmxHeaders.Reswap] = "none";
            httpContext.Response.HxTrigger(HtmxErrorEvent, new { message, correlationId = httpContext.TraceIdentifier });
            return true;
        }

        return false;
    }
}
