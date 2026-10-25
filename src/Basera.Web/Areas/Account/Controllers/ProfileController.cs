using Basera.Modules.Identity.Application;
using Basera.Modules.Identity.Infrastructure;
using Basera.Shared.Authorization;
using Basera.Shared.Localization;
using Basera.Shared.Users;
using Basera.Shared.Web;
using Basera.Web.Areas.Account.Models;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Basera.Web.Areas.Account.Controllers;

public sealed class ProfileInput
{
    public string? DisplayName { get; set; }

    /// <summary>"en", "ne", or empty for the area's default.</summary>
    public string? PreferredLanguage { get; set; }
}

public sealed class ChangePasswordInput
{
    public string? CurrentPassword { get; set; }

    public string? NewPassword { get; set; }
}

public sealed class ProfileInputValidator : AbstractValidator<ProfileInput>
{
    public ProfileInputValidator(IStringLocalizer<SharedResource> t)
    {
        RuleFor(x => x.DisplayName).NotEmpty().WithMessage(t["Validation.Required"])
            .MaximumLength(ApplicationUser.DisplayNameMaxLength).WithMessage(t["Validation.TooLong", ApplicationUser.DisplayNameMaxLength]);
    }
}

public sealed class ChangePasswordInputValidator : AbstractValidator<ChangePasswordInput>
{
    public ChangePasswordInputValidator(IStringLocalizer<SharedResource> t)
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage(t["Validation.Required"]);
        RuleFor(x => x.NewPassword).NotEmpty().WithMessage(t["Validation.Required"]);
    }
}

/// <summary>Page model for the profile: the saved profile plus whichever form is being redisplayed.</summary>
public sealed class ProfilePageViewModel
{
    public required Profile Profile { get; init; }

    public required ProfileInput Details { get; init; }

    public ChangePasswordInput Password { get; init; } = new();

    public string HomePath { get; init; } = "/";
}

/// <summary>Every signed-in user's own profile (PRODUCT_SPEC §4.1): name, language, password.</summary>
[Area("Account")]
[Route("account/profile")]
[Authorize]
public sealed class ProfileController(ProfileService profiles, IStringLocalizer<SharedResource> t) : Controller
{
    private const string NoticeKey = "Account.Profile.Notice";

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var profile = await profiles.GetAsync();
        ViewData[NoticeKey] = TempData[NoticeKey];
        return View(Page(profile, new ProfileInput { DisplayName = profile.DisplayName, PreferredLanguage = profile.PreferredLanguage }));
    }

    [HttpPost("")]
    public async Task<IActionResult> Index([Bind(Prefix = "Details")] ProfileInput details)
    {
        if (ModelState.IsValid)
        {
            var language = string.IsNullOrEmpty(details.PreferredLanguage) ? null : details.PreferredLanguage;
            var result = await profiles.UpdateAsync(details.DisplayName, language);
            if (result.IsSuccess)
            {
                return Saved(t["Account.Profile.Saved"]);
            }

            ModelState.AddDomainError(result.Error!, prefix: "Details");
        }

        return View(Page(await profiles.GetAsync(), details));
    }

    [HttpPost("password")]
    public async Task<IActionResult> Password([Bind(Prefix = "Password")] ChangePasswordInput password)
    {
        if (ModelState.IsValid)
        {
            var result = await profiles.ChangePasswordAsync(password.CurrentPassword, password.NewPassword);
            if (result.IsSuccess)
            {
                return Saved(t["Account.Profile.PasswordChanged"]);
            }

            ModelState.AddDomainError(result.Error!, prefix: "Password");
        }

        var profile = await profiles.GetAsync();
        return View(nameof(Index), Page(profile, new ProfileInput { DisplayName = profile.DisplayName, PreferredLanguage = profile.PreferredLanguage }, new ChangePasswordInput()));
    }

    private ProfilePageViewModel Page(Profile profile, ProfileInput details, ChangePasswordInput? password = null) => new()
    {
        Profile = profile,
        Details = details,
        Password = password ?? new ChangePasswordInput(),
        HomePath = BaseraRoles.HomePath(User.FindFirst(BaseraClaimTypes.Role)?.Value),
    };

    private RedirectToActionResult Saved(string notice)
    {
        TempData[NoticeKey] = notice;
        return RedirectToAction(nameof(Index));
    }
}
