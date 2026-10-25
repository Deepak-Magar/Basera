using System.Security.Claims;
using Basera.Modules.Identity.Domain;
using Basera.Shared.Localization;
using Basera.Shared.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Basera.Modules.Identity.Infrastructure;

/// <summary>
/// The claims in the auth cookie (ARCHITECTURE §7): <c>sub</c>, <c>name</c> (display name, not the phone
/// login), <c>tenant_id</c>, <c>role</c> from the membership, <c>lang</c>, and Identity's security stamp so a
/// password reset signs out other sessions. Needs the tenant context set, because memberships are filtered;
/// sign-in sets it from <see cref="SignInLookup"/>, a cookie refresh from the cookie's own claim.
/// </summary>
public sealed class BaseraClaimsPrincipalFactory(
    UserManager<ApplicationUser> users, IdentityDbContext db, IOptions<IdentityOptions> options)
    : IUserClaimsPrincipalFactory<ApplicationUser>
{
    public async Task<ClaimsPrincipal> CreateAsync(ApplicationUser user)
    {
        var role = await db.Memberships
            .Where(m => m.UserId == user.Id)
            .Select(m => (Role?)m.Role)
            .SingleOrDefaultAsync();

        var identity = new ClaimsIdentity(IdentityConstants.ApplicationScheme, BaseraClaimTypes.Name, BaseraClaimTypes.Role);
        identity.AddClaim(new Claim(BaseraClaimTypes.Subject, user.Id.ToString()));
        identity.AddClaim(new Claim(BaseraClaimTypes.Name, user.DisplayName));
        identity.AddClaim(new Claim(BaseraClaimTypes.TenantId, user.TenantId.ToString()));
        if (role is not null)
        {
            identity.AddClaim(new Claim(BaseraClaimTypes.Role, role.Value.ToString()));
        }

        identity.AddClaim(new Claim(BaseraClaimTypes.Language, user.PreferredLanguage ?? DefaultLanguage(role)));
        if (users.SupportsUserSecurityStamp)
        {
            identity.AddClaim(new Claim(options.Value.ClaimsIdentity.SecurityStampClaimType, await users.GetSecurityStampAsync(user)));
        }

        return new ClaimsPrincipal(identity);
    }

    /// <summary>The same split as the area defaults (§1): management at a desk in English, everyone else Nepali.</summary>
    public static string DefaultLanguage(Role? role) =>
        role is Role.Owner or Role.Manager ? BaseraCultures.English : BaseraCultures.Nepali;
}
