using Basera.Modules.Identity.Domain;
using Basera.Modules.Identity.Infrastructure;
using Basera.Modules.Residents.Contracts;
using Basera.Shared.Domain;
using Basera.Shared.Tenancy;
using Basera.Shared.Time;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Basera.Modules.Identity.Application;

public enum OtpSendOutcome
{
    Sent,
    InvalidPhone,

    /// <summary>No user and no resident or guardian with this phone: refused before any SMS is paid for.</summary>
    NotRegistered,

    /// <summary>The phone belongs to staff, who sign in with a password.</summary>
    UsePassword,

    /// <summary>The user's membership was removed.</summary>
    NoAccess,
    TooManyCodes,
}

public enum OtpSignInOutcome
{
    Succeeded,
    InvalidPhone,
    WrongCode,
    TooManyAttempts,

    /// <summary>Expired, already used, replaced by a newer code, or never sent.</summary>
    CodeNotValid,
    NotRegistered,
    NoAccess,
}

public sealed record OtpSignInResult(OtpSignInOutcome Outcome, Role? Role = null);

/// <summary>
/// Phone + SMS code sign-in for residents and parents (ARCHITECTURE §7, ADR-004). A phone that already has a
/// user signs in as that user. Otherwise Residents is asked who the phone belongs to
/// (<see cref="IResidentPhoneLookup"/>), and on the first correct code the user and its Resident or Parent
/// membership are created in that tenant. Limits are <see cref="OtpService"/>'s.
/// </summary>
public sealed class OtpSignInService(
    IdentityDbContext db,
    TenantContext tenantContext,
    SignInLookup lookup,
    IResidentPhoneLookup residents,
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn,
    IUserStore<ApplicationUser> userStore,
    OtpService otp,
    IClock clock)
{
    public async Task<OtpSendOutcome> SendCodeAsync(string? phoneInput, CancellationToken cancellationToken = default)
    {
        var phone = PhoneNumber.Create(phoneInput);
        if (phone.IsFailure)
        {
            return OtpSendOutcome.InvalidPhone;
        }

        var candidate = await lookup.FindAsync(phone.Value.E164, cancellationToken);
        if (candidate is not null)
        {
            tenantContext.Set(candidate.TenantId);
            var role = await RoleOfAsync(candidate.UserId, cancellationToken);
            if (role is null)
            {
                return OtpSendOutcome.NoAccess;
            }

            if (role is not (Role.Resident or Role.Parent))
            {
                return OtpSendOutcome.UsePassword;
            }
        }
        else if (await residents.FindAsync(phone.Value, cancellationToken) is null)
        {
            return OtpSendOutcome.NotRegistered;
        }

        return await otp.SendAsync(phone.Value, OtpPurpose.Login, cancellationToken) == OtpSendResult.Sent
            ? OtpSendOutcome.Sent
            : OtpSendOutcome.TooManyCodes;
    }

    /// <summary>Checks the code, then signs in, creating the user on the first success. The cookie is persistent.</summary>
    public async Task<OtpSignInResult> SignInAsync(string? phoneInput, string? code, CancellationToken cancellationToken = default)
    {
        var phone = PhoneNumber.Create(phoneInput);
        if (phone.IsFailure)
        {
            return new OtpSignInResult(OtpSignInOutcome.InvalidPhone);
        }

        var check = await otp.VerifyAsync(phone.Value, OtpPurpose.Login, code, cancellationToken);
        if (check != OtpCheck.Verified)
        {
            return new OtpSignInResult(check switch
            {
                OtpCheck.WrongCode => OtpSignInOutcome.WrongCode,
                OtpCheck.TooManyAttempts => OtpSignInOutcome.TooManyAttempts,
                _ => OtpSignInOutcome.CodeNotValid,
            });
        }

        var candidate = await lookup.FindAsync(phone.Value.E164, cancellationToken);
        return candidate is null
            ? await SignInNewUserAsync(phone.Value, cancellationToken)
            : await SignInExistingUserAsync(candidate, cancellationToken);
    }

    private async Task<OtpSignInResult> SignInExistingUserAsync(SignInCandidate candidate, CancellationToken cancellationToken)
    {
        tenantContext.Set(candidate.TenantId);
        var user = await users.FindByIdAsync(candidate.UserId.ToString());
        var role = await RoleOfAsync(candidate.UserId, cancellationToken);
        if (user is null || role is not (Role.Resident or Role.Parent))
        {
            return new OtpSignInResult(OtpSignInOutcome.NoAccess);
        }

        if (user.PhoneVerifiedAt is null)
        {
            user.PhoneVerifiedAt = clock.UtcNow;
            await users.UpdateAsync(user);
        }

        await signIn.SignInAsync(user, isPersistent: true);
        return new OtpSignInResult(OtpSignInOutcome.Succeeded, role);
    }

    // First sign-in: the user, its phone proof and its membership are saved together.
    private async Task<OtpSignInResult> SignInNewUserAsync(PhoneNumber phone, CancellationToken cancellationToken)
    {
        var holder = await residents.FindAsync(phone, cancellationToken);
        if (holder is null)
        {
            return new OtpSignInResult(OtpSignInOutcome.NotRegistered);
        }

        tenantContext.Set(holder.TenantId);
        var user = new ApplicationUser
        {
            UserName = phone.E164,
            PhoneNumber = phone.E164,
            PhoneNumberConfirmed = true,
            PhoneVerifiedAt = clock.UtcNow,
            DisplayName = holder.DisplayName,
        };
        var role = holder.Kind == PhoneHolderKind.Parent ? Role.Parent : Role.Resident;

        userStore.HoldChanges();
        var created = await users.CreateAsync(user);
        if (!created.Succeeded)
        {
            throw new InvalidOperationException(
                $"Creating the user for an OTP sign-in failed: {string.Join(" ", created.Errors.Select(e => e.Description))}");
        }

        db.Memberships.Add(new TenantMembership(user.Id, role));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // The same person verified twice at once and the other request created the user first. The code
            // is spent either way; asking for a new one signs in as the existing user.
            return new OtpSignInResult(OtpSignInOutcome.CodeNotValid);
        }

        await signIn.SignInAsync(user, isPersistent: true);
        return new OtpSignInResult(OtpSignInOutcome.Succeeded, role);
    }

    private Task<Role?> RoleOfAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Memberships.Where(m => m.UserId == userId).Select(m => (Role?)m.Role).SingleOrDefaultAsync(cancellationToken);
}
