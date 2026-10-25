using Basera.Modules.Identity.Domain;
using Basera.Modules.Identity.Infrastructure;
using Basera.Shared.Domain;
using Basera.Shared.Localization;
using Basera.Shared.Results;
using Basera.Shared.Tenancy;
using Basera.Shared.Time;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Basera.Modules.Identity.Application;

public sealed record RegisterOwnerCommand(string? HostelName, string? OwnerName, string? Phone, string? Email, string? Password);

public enum PasswordSignInOutcome
{
    Succeeded,

    /// <summary>Unknown login or wrong password; the page never says which.</summary>
    InvalidCredentials,

    /// <summary>Five wrong passwords in a row: locked for 15 minutes.</summary>
    LockedOut,

    /// <summary>Right password, but the user has no role in their tenant (e.g. revoked staff).</summary>
    NoAccess,
}

public sealed record PasswordSignInResult(PasswordSignInOutcome Outcome, Role? Role = null);

/// <summary>
/// Owner self-registration, password sign-in and sign-out, and password reset by SMS code (PRODUCT_SPEC §4.1).
/// Every flow starts with no tenant (nobody is signed in), finds the tenant through
/// <see cref="SignInLookup"/> or creates it, sets the tenant context, and continues tenant-filtered.
/// </summary>
public sealed class AccountService(
    IdentityDbContext db,
    TenantContext tenantContext,
    SignInLookup lookup,
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn,
    IUserStore<ApplicationUser> userStore,
    OtpService otp,
    IClock clock)
{
    /// <summary>
    /// Creates the tenant, the owner and the Owner membership in one <c>SaveChanges</c> (one transaction:
    /// all three or none), then signs the owner in.
    /// </summary>
    public async Task<Result> RegisterOwnerAsync(RegisterOwnerCommand command, CancellationToken cancellationToken = default)
    {
        if (tenantContext.IsResolved)
        {
            return DomainError.Conflict("You are already signed in. Sign out to register another hostel.");
        }

        var phone = PhoneNumber.Create(command.Phone);
        if (phone.IsFailure)
        {
            return phone.Error!;
        }

        var email = command.Email?.Trim() ?? "";
        if (await lookup.FindAsync(phone.Value.E164, cancellationToken) is not null)
        {
            return DomainError.Validation("This mobile number already has a Basera account. Sign in instead.", "phone");
        }

        if (await lookup.FindAsync(email, cancellationToken) is not null)
        {
            return DomainError.Validation("This email address already has a Basera account. Sign in instead.", "email");
        }

        var tenant = Tenant.Register(command.HostelName, phone.Value, clock.Today);
        if (tenant.IsFailure)
        {
            return tenant.Error!;
        }

        tenantContext.Set(tenant.Value.Id);
        db.Tenants.Add(tenant.Value);

        var owner = new ApplicationUser
        {
            UserName = phone.Value.E164,
            PhoneNumber = phone.Value.E164,
            Email = email,
            DisplayName = command.OwnerName?.Trim() ?? "",
            PreferredLanguage = BaseraCultures.English,
        };

        // UserManager would save the user on its own; hold it so the tenant, user and membership commit together.
        userStore.HoldChanges();
        var created = await users.CreateAsync(owner, command.Password ?? "");
        if (!created.Succeeded)
        {
            return DomainError.Validation(string.Join(" ", created.Errors.Select(e => e.Description)), "password");
        }

        db.Memberships.Add(new TenantMembership(owner.Id, Role.Owner));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // Someone registered the same phone or email between our check and this insert.
            return DomainError.Conflict("This mobile number or email already has a Basera account. Sign in instead.");
        }

        await signIn.SignInAsync(owner, isPersistent: false);
        return Result.Success();
    }

    /// <summary><paramref name="login"/> is an email address or a mobile number. Five failures lock the account for 15 minutes.</summary>
    public async Task<PasswordSignInResult> PasswordSignInAsync(string? login, string? password, bool remember, CancellationToken cancellationToken = default)
    {
        var user = await FindUserAsync(login, cancellationToken);
        if (user is null)
        {
            return new PasswordSignInResult(PasswordSignInOutcome.InvalidCredentials);
        }

        var result = await signIn.CheckPasswordSignInAsync(user, password ?? "", lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            return new PasswordSignInResult(PasswordSignInOutcome.LockedOut);
        }

        if (!result.Succeeded)
        {
            return new PasswordSignInResult(PasswordSignInOutcome.InvalidCredentials);
        }

        // Checked after the password, so the page can't be used to learn who is a member.
        var role = await RoleOfAsync(user, cancellationToken);
        if (role is null)
        {
            return new PasswordSignInResult(PasswordSignInOutcome.NoAccess);
        }

        await signIn.SignInAsync(user, remember);
        return new PasswordSignInResult(PasswordSignInOutcome.Succeeded, role);
    }

    public Task SignOutAsync() => signIn.SignOutAsync();

    /// <summary>
    /// Sends a reset code if the number belongs to a user. An unknown number gets the same answer and no SMS,
    /// so the page does not reveal who has an account, and nobody pays for SMS to random numbers.
    /// </summary>
    public async Task<Result> SendPasswordResetCodeAsync(string? phoneInput, CancellationToken cancellationToken = default)
    {
        var phone = PhoneNumber.Create(phoneInput);
        if (phone.IsFailure)
        {
            return phone.Error!;
        }

        if (await lookup.FindAsync(phone.Value.E164, cancellationToken) is null)
        {
            return Result.Success();
        }

        return await otp.SendAsync(phone.Value, OtpPurpose.PasswordReset, cancellationToken) == OtpSendResult.Sent
            ? Result.Success()
            : DomainError.RuleViolation("Too many codes sent to this number. Wait 10 minutes and try again.", "phone");
    }

    /// <summary>
    /// Sets a new password after the SMS code checks out. The password is validated before the code, so a
    /// weak password doesn't use up the code. A successful reset also proves the phone, ends any lockout,
    /// and changes the security stamp, which signs out other sessions.
    /// </summary>
    public async Task<Result> ResetPasswordAsync(string? phoneInput, string? code, string? newPassword, CancellationToken cancellationToken = default)
    {
        var phone = PhoneNumber.Create(phoneInput);
        if (phone.IsFailure)
        {
            return phone.Error!;
        }

        var user = await FindUserAsync(phone.Value.E164, cancellationToken);
        if (user is null)
        {
            return DomainError.Validation("This code is not valid any more. Request a new one.", "code");
        }

        foreach (var validator in users.PasswordValidators)
        {
            var valid = await validator.ValidateAsync(users, user, newPassword ?? "");
            if (!valid.Succeeded)
            {
                return DomainError.Validation(string.Join(" ", valid.Errors.Select(e => e.Description)), "newPassword");
            }
        }

        var check = await otp.VerifyAsync(phone.Value, OtpPurpose.PasswordReset, code, cancellationToken);
        if (check != OtpCheck.Verified)
        {
            return DomainError.Validation(
                check switch
                {
                    OtpCheck.WrongCode => "That code is not right. Check the SMS and try again.",
                    OtpCheck.TooManyAttempts => "Too many wrong tries. Request a new code.",
                    _ => "This code is not valid any more. Request a new one.",
                },
                "code");
        }

        var token = await users.GeneratePasswordResetTokenAsync(user);
        var reset = await users.ResetPasswordAsync(user, token, newPassword!);
        if (!reset.Succeeded)
        {
            return DomainError.Validation(string.Join(" ", reset.Errors.Select(e => e.Description)), "newPassword");
        }

        user.PhoneVerifiedAt ??= clock.UtcNow;
        await users.SetLockoutEndDateAsync(user, null);
        await users.ResetAccessFailedCountAsync(user);
        return Result.Success();
    }

    // The login path: find the tenant without a filter (SignInLookup), then act as that tenant.
    private async Task<ApplicationUser?> FindUserAsync(string? login, CancellationToken cancellationToken)
    {
        var candidate = await lookup.FindAsync(login, cancellationToken);
        if (candidate is null)
        {
            return null;
        }

        tenantContext.Set(candidate.TenantId);
        return await users.FindByIdAsync(candidate.UserId.ToString());
    }

    private Task<Role?> RoleOfAsync(ApplicationUser user, CancellationToken cancellationToken) =>
        db.Memberships.Where(m => m.UserId == user.Id).Select(m => (Role?)m.Role).SingleOrDefaultAsync(cancellationToken);
}
