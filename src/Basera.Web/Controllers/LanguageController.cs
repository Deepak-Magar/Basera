using Basera.Shared.Localization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace Basera.Web.Controllers;

/// <summary>
/// The language switch: stores the choice in the culture cookie and goes back. ARCHITECTURE §9 keeps the
/// preference on the user; until Identity exists the cookie is all there is (PROGRESS.md).
/// </summary>
[Route("language")]
public sealed class LanguageController : Controller
{
    [HttpPost("")]
    public IActionResult Set([FromForm] string culture, [FromForm] string? returnUrl)
    {
        if (!BaseraCultures.Supported.Contains(culture))
        {
            return BadRequest();
        }

        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = Request.IsHttps,
            });

        // Only back to a page of ours: an open redirect would let a link send users anywhere.
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/");
    }
}
