using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Basera.Shared.Web;

/// <summary>
/// Gives every request a correlation id (ARCHITECTURE §10): the caller's <c>X-Correlation-Id</c> if it is a
/// sane value, otherwise the trace id. It becomes <see cref="HttpContext.TraceIdentifier"/>, is echoed in the
/// response header, is added to every log line of the request, and is shown on error pages and in envelopes,
/// so a user's screenshot leads straight to the logs.
/// </summary>
public sealed partial class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var supplied = context.Request.Headers[HeaderName].ToString();
        var correlationId = SafeId().IsMatch(supplied)
            ? supplied
            : System.Diagnostics.Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");

        context.TraceIdentifier = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await next(context);
        }
    }

    // Untrusted input goes into logs and headers, so only short ids made of letters, digits and dashes.
    [GeneratedRegex("^[A-Za-z0-9-]{8,64}$")]
    private static partial Regex SafeId();
}
