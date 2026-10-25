using Basera.Modules.Identity.Application;
using Basera.Modules.Identity.Domain;
using Basera.Shared.Authorization;
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
public sealed class AccountController(
    AccountService accounts, OtpSignInService otp, InvitationService invitations, IStringLocalizer<SharedResource> t) : Controller
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

        return LocalRedirect(BaseraRoles.HomePath(BaseraRoles.Owner));
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
                return LocalRedirect(AfterSignIn(input.ReturnUrl, result.Role));
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

    [HttpGet("otp")]
    public IActionResult Otp() => SignedIn ? LocalRedirect("/") : View(new OtpRequestInput());

    [HttpPost("otp")]
    public async Task<IActionResult> Otp(OtpRequestInput input, CancellationToken cancellationToken)
    {
        if (SignedIn)
        {
            return LocalRedirect("/");
        }

        if (!ModelState.IsValid)
        {
            return View(input);
        }

        var outcome = await otp.SendCodeAsync(input.Phone, cancellationToken);
        if (outcome == OtpSendOutcome.Sent)
        {
            return RedirectToAction(nameof(OtpVerify), new { phone = input.Phone });
        }

        ModelState.AddModelError(nameof(input.Phone), outcome switch
        {
            OtpSendOutcome.InvalidPhone => t["Account.Otp.InvalidPhone"],
            OtpSendOutcome.NotRegistered => t["Account.Otp.NotRegistered"],
            OtpSendOutcome.UsePassword => t["Account.Otp.UsePassword"],
            OtpSendOutcome.NoAccess => t["Account.Login.NoAccess"],
            _ => t["Account.Otp.TooManyCodes"],
        });
        return View(input);
    }

    [HttpGet("otp/verify")]
    public IActionResult OtpVerify(string? phone) => SignedIn ? LocalRedirect("/") : View(new OtpVerifyInput { Phone = phone });

    [HttpPost("otp/verify")]
    public async Task<IActionResult> OtpVerify(OtpVerifyInput input, CancellationToken cancellationToken)
    {
        if (SignedIn)
        {
            return LocalRedirect("/");
        }

        if (!ModelState.IsValid)
        {
            return View(input);
        }

        var result = await otp.SignInAsync(input.Phone, input.Code, cancellationToken);
        if (result.Outcome == OtpSignInOutcome.Succeeded)
        {
            return LocalRedirect(AfterSignIn(null, result.Role));
        }

        var (field, message) = result.Outcome switch
        {
            OtpSignInOutcome.InvalidPhone => (nameof(input.Phone), t["Account.Otp.InvalidPhone"]),
            OtpSignInOutcome.WrongCode => (nameof(input.Code), t["Account.Otp.WrongCode"]),
            OtpSignInOutcome.TooManyAttempts => (nameof(input.Code), t["Account.Otp.TooManyAttempts"]),
            OtpSignInOutcome.NotRegistered => ("", t["Account.Otp.NotRegistered"]),
            OtpSignInOutcome.NoAccess => ("", t["Account.Login.NoAccess"]),
            _ => (nameof(input.Code), t["Account.Otp.CodeNotValid"]),
        };
        ModelState.AddModelError(field, message);
        return View(input);
    }

    [HttpGet("invite/{token}")]
    public async Task<IActionResult> Invite(string token, CancellationToken cancellationToken)
    {
        if (SignedIn)
        {
            return View("InviteSignedIn");
        }

        var preview = await invitations.PreviewAsync(token, cancellationToken);
        if (preview is null)
        {
            return View("InviteInvalid");
        }

        ViewData["Invitation"] = preview;
        return View(new AcceptInvitationInput());
    }

    [HttpPost("invite/{token}")]
    public async Task<IActionResult> Invite(string token, AcceptInvitationInput input, CancellationToken cancellationToken)
    {
        if (SignedIn)
        {
            return View("InviteSignedIn");
        }

        var preview = await invitations.PreviewAsync(token, cancellationToken);
        if (preview is null)
        {
            return View("InviteInvalid");
        }

        ViewData["Invitation"] = preview;
        if (!ModelState.IsValid)
        {
            return View(input);
        }

        var result = await invitations.AcceptAsync(token, input.DisplayName, input.Password, cancellationToken);
        if (result.IsFailure)
        {
            ModelState.AddDomainError(result.Error!);
            return View(input);
        }

        return LocalRedirect(AfterSignIn(null, result.Value));
    }

    [HttpGet("denied")]
    public IActionResult Denied() => View();

    private bool SignedIn => User.Identity?.IsAuthenticated == true;

    // Only back to one of our own pages: a returnUrl pointing elsewhere would make the login page an open redirect.
    private string SafeReturnUrl(string? returnUrl) => Url.IsLocalUrl(returnUrl) ? returnUrl! : "/";

    // Where the user was going, if that was one of our pages; otherwise their role's area.
    private string AfterSignIn(string? returnUrl, Role? role) =>
        Url.IsLocalUrl(returnUrl) ? returnUrl! : BaseraRoles.HomePath(role?.ToString());
}
