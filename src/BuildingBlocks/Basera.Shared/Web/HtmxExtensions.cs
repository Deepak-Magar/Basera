using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace Basera.Shared.Web;

/// <summary>Header names HTMX sends and understands (htmx.org/reference).</summary>
public static class HtmxHeaders
{
    public const string Request = "HX-Request";
    public const string Boosted = "HX-Boosted";
    public const string Target = "HX-Target";
    public const string Trigger = "HX-Trigger";
    public const string Redirect = "HX-Redirect";
    public const string Reswap = "HX-Reswap";
    public const string Retarget = "HX-Retarget";
}

/// <summary>The HTMX conventions of ARCHITECTURE §9: fragment for HTMX requests, full page otherwise.</summary>
public static class HtmxExtensions
{
    public static bool IsHtmx(this HttpRequest request) => request.Headers[HtmxHeaders.Request] == "true";

    /// <summary>hx-boost swaps the whole body, so a boosted request wants the full page, not a fragment.</summary>
    public static bool IsHtmxBoosted(this HttpRequest request) => request.Headers[HtmxHeaders.Boosted] == "true";

    /// <summary>Raises a client-side event after the swap, e.g. to show a toast or refresh a count.</summary>
    public static void HxTrigger(this HttpResponse response, string eventName, object? detail = null) =>
        response.Headers[HtmxHeaders.Trigger] = JsonSerializer.Serialize(
            new Dictionary<string, object?> { [eventName] = detail ?? true },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

    /// <summary>A full navigation from an HTMX request (a plain 302 would be followed inside the fragment).</summary>
    public static void HxRedirect(this HttpResponse response, string url) => response.Headers[HtmxHeaders.Redirect] = url;

    /// <summary>
    /// The controller rule of §9: <c>PartialView</c> for an HTMX fragment request, <c>View</c> otherwise.
    /// Adds <c>Vary: HX-Request</c> so a cache never serves one in place of the other.
    /// </summary>
    public static IActionResult PageOrPartial(this Controller controller, string? viewName = null, object? model = null)
    {
        controller.Response.Headers.Append("Vary", HtmxHeaders.Request);
        return controller.Request.IsHtmx() && !controller.Request.IsHtmxBoosted()
            ? controller.PartialView(viewName, model)
            : controller.View(viewName, model);
    }

    /// <summary>
    /// <c>hx-headers</c> value carrying the anti-forgery token (§12), put on each layout's <c>body</c> so
    /// every HTMX request inherits it: <c>&lt;body hx-headers='@Html.HtmxAntiforgeryHeaders()'&gt;</c>.
    /// </summary>
    public static IHtmlContent HtmxAntiforgeryHeaders(this IHtmlHelper html)
    {
        var http = html.ViewContext.HttpContext;
        var tokens = http.RequestServices.GetRequiredService<IAntiforgery>().GetAndStoreTokens(http);
        var json = JsonSerializer.Serialize(new Dictionary<string, string?> { [tokens.HeaderName!] = tokens.RequestToken });
        return new HtmlString(HtmlEncoder.Default.Encode(json));
    }
}
