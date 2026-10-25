using Basera.Modules.Identity.Application;
using Basera.Shared.Localization;
using Basera.Shared.Web;
using Basera.Web.Areas.Account.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Basera.Web.Areas.Account.Controllers;

/// <summary>
/// Registration, password sign-in, sign-out and password reset (PRODUCT_SPEC §4.1). The pages live in the
/// host because they belong to no area's shell (ARCHITECTURE §3.6); the work is in Identity's
/// <see cref="AccountService"/>. Every POST here is rate-limited by the "auth" policy.
/// </summary>
[Area("Account")]
[Route("account")]
[AllowAnonymous]
public sealed class AccountController(AccountService accounts, IStringLocalizer<SharedResource> t) : Controller
{
    [HttpGet("register")]
    public IActionResult Register() => SignedIn ? LocalRedirect("/") : View(new RegisterInput());

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterInput input, CancellationToken cancellationToken)
    {
        if (SignedIn)
        {
            return LocalRedirect("/");
        }

        if (!ModelState.IsValid)
        {
            return View(input);
        }

        var result = await accounts.RegisterOwnerAsync(
            new RegisterOwnerCommand(input.HostelName, input.OwnerName, input.Phone, input.Email, input.Password), cancellationToken);
        if (result.IsFailure)
        {
            ModelState.AddDomainError(result.Error!);
            return View(input);
        }

        return LocalRedirect("/owner");
    }

    [HttpGet("login")]
    public IActionResult Login(string? returnUrl) =>
        SignedIn ? LocalRedirect(SafeReturnUrl(returnUrl)) : View(new LoginInput { ReturnUrl = returnUrl });

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginInput input, CancellationToken cancellationToken)
    {
        if (SignedIn)
        {
            return LocalRedirect(SafeReturnUrl(input.ReturnUrl));
        }

        if (!ModelState.IsValid)
        {
            return View(input);
        }

        var result = await accounts.PasswordSignInAsync(input.Login, input.Password, input.RememberMe, cancellationToken);
        switch (result.Outcome)
        {
            case PasswordSignInOutcome.Succeeded:
                return LocalRedirect(SafeReturnUrl(input.ReturnUrl));
            case PasswordSignInOutcome.LockedOut:
                ModelState.AddModelError("", t["Account.Login.LockedOut"]);
                break;
            case PasswordSignInOutcome.NoAccess:
                ModelState.AddModelError("", t["Account.Login.NoAccess"]);
                break;
            default:
                ModelState.AddModelError("", t["Account.Login.Invalid"]);
                break;
        }

        input.Password = null;
        return View(input);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await accounts.SignOutAsync();
        return LocalRedirect("/");
    }

    [HttpGet("forgot")]
    public IActionResult Forgot() => View(new ForgotPasswordInput());

    [HttpPost("forgot")]
    public async Task<IActionResult> Forgot(ForgotPasswordInput input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(input);
        }

        var result = await accounts.SendPasswordResetCodeAsync(input.Phone, cancellationToken);
        if (result.IsFailure)
        {
            ModelState.AddDomainError(result.Error!);
            return View(input);
        }

        // Same page whether or not the number has an account.
        return RedirectToAction(nameof(Reset), new { phone = input.Phone });
    }

    [HttpGet("reset")]
    public IActionResult Reset(string? phone) => View(new ResetPasswordInput { Phone = phone });

    [HttpPost("reset")]
    public async Task<IActionResult> Reset(ResetPasswordInput input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(input);
        }

        var result = await accounts.ResetPasswordAsync(input.Phone, input.Code, input.NewPassword, cancellationToken);
        if (result.IsFailure)
        {
            ModelState.AddDomainError(result.Error!);
            input.NewPassword = null;
            return View(input);
        }

        TempData["Account.Notice"] = t["Account.Reset.Done"].Value;
        return RedirectToAction(nameof(Login));
    }

    [HttpGet("denied")]
    public IActionResult Denied() => View();

    private bool SignedIn => User.Identity?.IsAuthenticated == true;

    // Only back to one of our own pages: a returnUrl pointing elsewhere would make the login page an open redirect.
    private string SafeReturnUrl(string? returnUrl) => Url.IsLocalUrl(returnUrl) ? returnUrl! : "/";
}
