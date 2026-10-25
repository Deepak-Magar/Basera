using Basera.Modules.Identity.Domain;
using Basera.Modules.Identity.Infrastructure;
using Basera.Shared.Localization;
using FluentValidation;
using Microsoft.Extensions.Localization;

namespace Basera.Web.Areas.Account.Models;

// Form inputs for the account pages (ARCHITECTURE §14: inputs end in Input). The validators check shape only;
// whether a phone is free or a password is right is the service's job.

public sealed class RegisterInput
{
    public string? HostelName { get; set; }

    public string? OwnerName { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Password { get; set; }
}

public sealed class LoginInput
{
    /// <summary>Email address or mobile number.</summary>
    public string? Login { get; set; }

    public string? Password { get; set; }

    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

public sealed class ForgotPasswordInput
{
    public string? Phone { get; set; }
}

public sealed class ResetPasswordInput
{
    public string? Phone { get; set; }

    public string? Code { get; set; }

    public string? NewPassword { get; set; }
}

public sealed class OtpRequestInput
{
    public string? Phone { get; set; }
}

public sealed class OtpVerifyInput
{
    public string? Phone { get; set; }

    public string? Code { get; set; }
}

public sealed class OtpRequestInputValidator : AbstractValidator<OtpRequestInput>
{
    public OtpRequestInputValidator(IStringLocalizer<SharedResource> t)
    {
        RuleFor(x => x.Phone).NotEmpty().WithMessage(t["Validation.Required"]);
    }
}

public sealed class OtpVerifyInputValidator : AbstractValidator<OtpVerifyInput>
{
    public OtpVerifyInputValidator(IStringLocalizer<SharedResource> t)
    {
        RuleFor(x => x.Phone).NotEmpty().WithMessage(t["Validation.Required"]);
        RuleFor(x => x.Code).NotEmpty().WithMessage(t["Validation.Required"])
            .Matches(OtpCodePattern).WithMessage(t["Validation.OtpCode"]);
    }

    /// <summary>Six digits, ASCII or Devanagari (a Nepali keyboard types ०-९).</summary>
    public const string OtpCodePattern = @"^\s*[0-9०-९]{6}\s*$";
}

public sealed class RegisterInputValidator : AbstractValidator<RegisterInput>
{
    public RegisterInputValidator(IStringLocalizer<SharedResource> t)
    {
        RuleFor(x => x.HostelName).NotEmpty().WithMessage(t["Validation.Required"])
            .MaximumLength(Tenant.NameMaxLength).WithMessage(t["Validation.TooLong", Tenant.NameMaxLength]);
        RuleFor(x => x.OwnerName).NotEmpty().WithMessage(t["Validation.Required"])
            .MaximumLength(ApplicationUser.DisplayNameMaxLength).WithMessage(t["Validation.TooLong", ApplicationUser.DisplayNameMaxLength]);
        RuleFor(x => x.Phone).NotEmpty().WithMessage(t["Validation.Required"]);
        RuleFor(x => x.Email).NotEmpty().WithMessage(t["Validation.Required"])
            .EmailAddress().WithMessage(t["Validation.Email"])
            .MaximumLength(256).WithMessage(t["Validation.TooLong", 256]);
        RuleFor(x => x.Password).NotEmpty().WithMessage(t["Validation.Required"]);
    }
}

public sealed class LoginInputValidator : AbstractValidator<LoginInput>
{
    public LoginInputValidator(IStringLocalizer<SharedResource> t)
    {
        RuleFor(x => x.Login).NotEmpty().WithMessage(t["Validation.Required"]);
        RuleFor(x => x.Password).NotEmpty().WithMessage(t["Validation.Required"]);
    }
}

public sealed class ForgotPasswordInputValidator : AbstractValidator<ForgotPasswordInput>
{
    public ForgotPasswordInputValidator(IStringLocalizer<SharedResource> t)
    {
        RuleFor(x => x.Phone).NotEmpty().WithMessage(t["Validation.Required"]);
    }
}

public sealed class ResetPasswordInputValidator : AbstractValidator<ResetPasswordInput>
{
    public ResetPasswordInputValidator(IStringLocalizer<SharedResource> t)
    {
        RuleFor(x => x.Phone).NotEmpty().WithMessage(t["Validation.Required"]);
        RuleFor(x => x.Code).NotEmpty().WithMessage(t["Validation.Required"])
            .Matches(OtpVerifyInputValidator.OtpCodePattern).WithMessage(t["Validation.OtpCode"]);
        RuleFor(x => x.NewPassword).NotEmpty().WithMessage(t["Validation.Required"]);
    }
}
