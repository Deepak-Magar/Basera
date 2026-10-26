using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Basera.IntegrationTests.Infrastructure;
using Basera.Modules.Identity.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Basera.IntegrationTests.Identity;

/// <summary>
/// A browser-like client for the account pages: keeps cookies, does not follow redirects, and posts each
/// form with the anti-forgery token from the page it came from.
/// </summary>
public sealed partial class AccountTestClient(BaseraWebFactory factory)
{
    public HttpClient Http { get; } = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    /// <summary>The auth cookie value set by the last response that set one, or null.</summary>
    public string? AuthCookie { get; private set; }

    /// <summary>
    /// GETs <paramref name="page"/>, then POSTs <paramref name="fields"/> to <paramref name="action"/> (default: the same
    /// URL). Pairs rather than a dictionary, so a field can repeat (checkboxes).
    /// </summary>
    public async Task<HttpResponseMessage> SubmitAsync(string page, IEnumerable<KeyValuePair<string, string>> fields, string? action = null)
    {
        var html = await (await Http.GetAsync(page)).Content.ReadAsStringAsync();
        var token = FormToken().Match(html) is { Success: true } hidden ? hidden.Groups[1].Value : HtmxToken().Match(html).Groups[1].Value;
        var form = fields.Append(new KeyValuePair<string, string>("__RequestVerificationToken", token));
        var response = await Http.PostAsync(action ?? page, new FormUrlEncodedContent(form));
        if (response.Headers.TryGetValues("Set-Cookie", out var cookies)
            && cookies.FirstOrDefault(c => c.StartsWith(BaseraIdentity.CookieName + "=", StringComparison.Ordinal)) is { } auth)
        {
            var value = auth.Split(';')[0][(BaseraIdentity.CookieName.Length + 1)..];
            AuthCookie = value.Length == 0 ? null : value;
        }

        return response;
    }

    public Task<HttpResponseMessage> RegisterAsync(Owner owner) => SubmitAsync("/account/register", new Dictionary<string, string>
    {
        ["HostelName"] = owner.HostelName,
        ["OwnerName"] = owner.Name,
        ["Phone"] = owner.Phone,
        ["Email"] = owner.Email,
        ["Password"] = owner.Password,
    });

    public Task<HttpResponseMessage> LoginAsync(string login, string password) => SubmitAsync("/account/login", new Dictionary<string, string>
    {
        ["Login"] = login,
        ["Password"] = password,
    });

    /// <summary>Decrypts the auth cookie with the host's own ticket format, as the cookie handler does.</summary>
    public ClaimsPrincipal? Principal()
    {
        if (AuthCookie is null)
        {
            return null;
        }

        var options = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(IdentityConstants.ApplicationScheme);
        return options.TicketDataFormat.Unprotect(WebUtility.UrlDecode(AuthCookie))?.Principal;
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex FormToken();

    // Pages without a form still carry the token for HTMX on the layout's body.
    [GeneratedRegex("RequestVerificationToken&quot;:&quot;([^&]+)&quot;")]
    private static partial Regex HtmxToken();
}

/// <summary>Registration details unique to one test.</summary>
public sealed record Owner(string HostelName, string Name, string Phone, string Email, string Password = "Passw0rd!")
{
    public static Owner Random() => new(
        $"Hostel {Guid.NewGuid():N}"[..20],
        "Sita Sharma",
        $"98{System.Random.Shared.Next(0, 100_000_000):D8}",
        $"{Guid.NewGuid():N}@basera.test");
}
