using Basera.Modules.Identity.Application;
using Basera.Shared.Localization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace Basera.Web.Controllers;

/// <summary>
/// The language switch: for a signed-in user the choice is saved on the user (ARCHITECTURE §9) and the cookie
/// re-issued with the new <c>lang</c> claim; for everyone it also goes in the culture cookie, which is what
/// counts before sign-in. Then back to the page.
/// </summary>
[Route("language")]
public sealed class LanguageController(ProfileService profiles) : Controller
{
    [HttpPost("")]
    public async Task<IActionResult> Set([FromForm] string culture, [FromForm] string? returnUrl)
    {
        if (!BaseraCultures.Supported.Contains(culture))
        {
            return BadRequest();
        }

        if (User.Identity?.IsAuthenticated == true)
        {
            await profiles.SetLanguageAsync(culture);
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
