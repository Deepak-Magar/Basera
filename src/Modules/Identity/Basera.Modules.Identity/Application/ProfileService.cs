using Basera.Modules.Identity.Infrastructure;
using Basera.Shared.Domain;
using Basera.Shared.Localization;
using Basera.Shared.Results;
using Basera.Shared.Users;
using Microsoft.AspNetCore.Identity;

namespace Basera.Modules.Identity.Application;

public sealed record Profile(string DisplayName, PhoneNumber Phone, string? Email, string? PreferredLanguage, bool HasPassword);

/// <summary>
/// The signed-in user's own profile (PRODUCT_SPEC §4.1): display name, language, password. Every change
/// re-issues the cookie (<c>RefreshSignInAsync</c>) so the <c>name</c> and <c>lang</c> claims follow at once,
/// and a password change keeps this session while the new security stamp ends the others.
/// </summary>
public sealed class ProfileService(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn, ICurrentUser currentUser)
{
    public async Task<Profile> GetAsync()
    {
        var user = await CurrentAsync();
        return new Profile(user.DisplayName, PhoneNumber.Create(user.PhoneNumber).Value, user.Email, user.PreferredLanguage,
            await users.HasPasswordAsync(user));
    }

    public async Task<Result> UpdateAsync(string? displayName, string? language)
    {
        var name = displayName?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > ApplicationUser.DisplayNameMaxLength)
        {
            return DomainError.Validation($"Enter a name of up to {ApplicationUser.DisplayNameMaxLength} characters.", "displayName");
        }

        if (language is not null && !BaseraCultures.Supported.Contains(language))
        {
            return DomainError.Validation("Choose English or Nepali.", "preferredLanguage");
        }

        var user = await CurrentAsync();
        user.DisplayName = name;
        user.PreferredLanguage = language;
        return await SaveAndRefreshAsync(user);
    }

    /// <summary>The header's language switch, for a signed-in user: the choice is kept on the user (ARCHITECTURE §9).</summary>
    public async Task<Result> SetLanguageAsync(string language)
    {
        if (!BaseraCultures.Supported.Contains(language))
        {
            return DomainError.Validation("Choose English or Nepali.", "culture");
        }

        var user = await CurrentAsync();
        user.PreferredLanguage = language;
        return await SaveAndRefreshAsync(user);
    }

    public async Task<Result> ChangePasswordAsync(string? currentPassword, string? newPassword)
    {
        var user = await CurrentAsync();
        var changed = await users.ChangePasswordAsync(user, currentPassword ?? "", newPassword ?? "");
        if (!changed.Succeeded)
        {
            var wrongCurrent = changed.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.PasswordMismatch));
            return DomainError.Validation(
                wrongCurrent ? "The current password is not right." : string.Join(" ", changed.Errors.Select(e => e.Description)),
                wrongCurrent ? "currentPassword" : "newPassword");
        }

        await signIn.RefreshSignInAsync(user);
        return Result.Success();
    }

    private async Task<Result> SaveAndRefreshAsync(ApplicationUser user)
    {
        var updated = await users.UpdateAsync(user);
        if (!updated.Succeeded)
        {
            return DomainError.Conflict(string.Join(" ", updated.Errors.Select(e => e.Description)));
        }

        await signIn.RefreshSignInAsync(user);
        return Result.Success();
    }

    // Only reachable signed in ([Authorize]); the tenant is resolved from the cookie, so this lookup is filtered.
    private async Task<ApplicationUser> CurrentAsync() =>
        await users.FindByIdAsync((currentUser.UserId ?? throw new InvalidOperationException("No signed-in user.")).ToString())
        ?? throw new InvalidOperationException($"Signed-in user {currentUser.UserId} was not found.");
}
