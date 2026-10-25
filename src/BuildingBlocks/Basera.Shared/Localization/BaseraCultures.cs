using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;

namespace Basera.Shared.Localization;

/// <summary>
/// Nepali and English (ARCHITECTURE §1, §9). The language comes from the <c>.AspNetCore.Culture</c> cookie the
/// language switch sets; with no cookie, each area has its default: English for /owner, Nepali for the phone
/// areas. When Identity arrives the preference moves onto the user, and the cookie follows it.
/// </summary>
public static class BaseraCultures
{
    public const string English = "en";
    public const string Nepali = "ne";

    public static readonly IReadOnlyList<string> Supported = [English, Nepali];

    // The OTP sign-in pages are for residents and parents, so they start in Nepali like those areas.
    private static readonly string[] NepaliAreas = ["/staff", "/resident", "/parent", "/account/otp"];

    public static bool IsNepali(this CultureInfo culture) =>
        culture.TwoLetterISOLanguageName == Nepali;

    /// <summary>The language a page uses before anyone chooses one.</summary>
    public static string DefaultFor(PathString path) =>
        NepaliAreas.Any(area => path.StartsWithSegments(area, StringComparison.OrdinalIgnoreCase)) ? Nepali : English;

    // Error pages are re-executed at /error/...; they should speak the language of the page that failed.
    private static PathString OriginalPath(HttpContext http) =>
        http.Features.Get<IStatusCodeReExecuteFeature>()?.OriginalPath
        ?? http.Features.Get<IExceptionHandlerPathFeature>()?.Path
        ?? http.Request.Path;

    public static RequestLocalizationOptions Options()
    {
        var cultures = Supported.Select(name => new CultureInfo(name)).ToList();
        var options = new RequestLocalizationOptions
        {
            DefaultRequestCulture = new RequestCulture(English),
            SupportedCultures = cultures,
            SupportedUICultures = cultures,
        };

        // Cookie first, then the area default; no query string or Accept-Language, so a page's
        // language never changes behind the user's back.
        options.RequestCultureProviders =
        [
            new CookieRequestCultureProvider(),
            new CustomRequestCultureProvider(http =>
                Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult(DefaultFor(OriginalPath(http))))),
        ];
        return options;
    }
}
