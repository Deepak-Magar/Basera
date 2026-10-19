using Basera.Shared.Web;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Basera.Web.Controllers;

/// <summary>
/// Where unhandled exceptions (<c>/error</c>) and empty error statuses (<c>/error/404</c>) are re-executed to.
/// Web requests get a page; <c>/api</c> requests get the envelope. Accepts any method, because a failed POST
/// is re-executed as a POST.
/// </summary>
[Route("error")]
[IgnoreAntiforgeryToken]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class ErrorController(IHostEnvironment environment) : Controller
{
    [Route("")]
    public IActionResult Error()
    {
        var exception = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
        Response.StatusCode = StatusCodes.Status500InternalServerError;
        return View("Error", new ErrorViewModel(
            HttpContext.TraceIdentifier,
            environment.IsDevelopment() ? exception?.Error.ToString() : null));
    }

    [Route("{status:int}")]
    public IActionResult Status(int status)
    {
        var original = HttpContext.Features.Get<IStatusCodeReExecuteFeature>();
        Response.StatusCode = status;
        if (original?.OriginalPath.StartsWith("/api", StringComparison.OrdinalIgnoreCase) == true)
        {
            var (code, message) = ApiResults.ForStatus(status);
            return Json(new ApiEnvelope<object>(false, null, new ApiError(code, message), HttpContext.TraceIdentifier));
        }

        return View("Status", new StatusViewModel(status, HttpContext.TraceIdentifier));
    }
}

public sealed record ErrorViewModel(string CorrelationId, string? Details);

public sealed record StatusViewModel(int Status, string CorrelationId);
