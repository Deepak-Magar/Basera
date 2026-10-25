using Basera.Shared.Domain;
using Microsoft.AspNetCore.Identity;

namespace Basera.Modules.Identity.Infrastructure;

/// <summary>
/// The ASP.NET Core Identity user (ARCHITECTURE §7). It lives in Infrastructure, not Domain, because it
/// derives from <see cref="IdentityUser{TKey}"/>, and Domain stays free of ASP.NET Core (architecture test).
/// <see cref="IdentityUser{TKey}.UserName"/> is the phone number in E.164 form: every user has one, and
/// it is unique across all tenants, so a login identifies exactly one user and therefore one tenant.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>, ITenantOwned, IAuditable
{
    public const int DisplayNameMaxLength = 100;

    public ApplicationUser()
    {
        Id = GuidV7.New();
    }

    /// <summary>The tenant this user belongs to; set from the tenant context on insert.</summary>
    public Guid TenantId { get; private set; }

    public string DisplayName { get; set; } = "";

    /// <summary>"ne" or "en"; null means the area's default language (§1).</summary>
    public string? PreferredLanguage { get; set; }

    /// <summary>When the phone number was proved by an OTP; null until then.</summary>
    public DateTimeOffset? PhoneVerifiedAt { get; set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }
}
