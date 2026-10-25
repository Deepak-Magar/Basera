using Basera.Modules.Identity.Application;
using Basera.Shared.Authorization;
using Basera.Shared.Persistence;
using Basera.Shared.Tenancy;
using Basera.Shared.Users;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Basera.Modules.Identity.Infrastructure;

public static class BaseraIdentity
{
    public const string CookieName = ".Basera.Auth";

    /// <summary>
    /// ASP.NET Core Identity with cookie authentication (ADR-004) on <see cref="IdentityDbContext"/>, and the
    /// role policies of ARCHITECTURE §7. <c>AddIdentityCore</c> plus the cookie schemes is what <c>AddIdentity</c>
    /// does, minus Identity roles, which Basera does not use.
    /// </summary>
    public static IServiceCollection AddBaseraIdentity(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<IdentityDbContext>(configuration);
        services.AddScoped<SignInLookup>();
        services.AddScoped<OtpService>();
        services.AddScoped<AccountService>();
        services.AddScoped<OtpSignInService>();
        services.Configure<OtpOptions>(configuration.GetSection("Identity:Otp"));

        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                // The claim names of ARCHITECTURE §7 rather than the long XML-schema URIs.
                options.ClaimsIdentity.UserIdClaimType = BaseraClaimTypes.Subject;
                options.ClaimsIdentity.UserNameClaimType = BaseraClaimTypes.Name;
                options.ClaimsIdentity.RoleClaimType = BaseraClaimTypes.Role;
                // Enforced across tenants by unique indexes; Identity's own check would only see one tenant.
                options.User.RequireUniqueEmail = false;
                // PRODUCT_SPEC §4.1 / feature brief: five wrong passwords lock the account for 15 minutes.
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddEntityFrameworkStores<IdentityDbContext>()
            .AddSignInManager()
            .AddClaimsPrincipalFactory<BaseraClaimsPrincipalFactory>()
            .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = CookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            // Same reasoning as the anti-forgery cookie (§12): always Secure behind the HTTPS redirect and HSTS.
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.LoginPath = "/account/login";
            options.LogoutPath = "/account/logout";
            options.AccessDeniedPath = "/account/denied";
            options.ExpireTimeSpan = TimeSpan.FromDays(14);
            options.SlidingExpiration = true;
            options.Events.OnValidatePrincipal = ValidatePrincipalAsync;
        });

        services.AddAuthorization(AddRolePolicies);
        return services;
    }

    /// <summary>
    /// The security stamp check loads the user, and users are tenant-filtered. This runs inside
    /// <c>UseAuthentication</c>, before tenant resolution, so the tenant comes from the cookie's own claim
    /// first. TenantResolutionMiddleware later sets the same id again, which is allowed.
    /// </summary>
    private static Task ValidatePrincipalAsync(CookieValidatePrincipalContext context)
    {
        var claim = context.Principal?.FindFirst(BaseraClaimTypes.TenantId)?.Value;
        if (Guid.TryParse(claim, out var tenantId) && tenantId != Guid.Empty)
        {
            context.HttpContext.RequestServices.GetRequiredService<TenantContext>().Set(tenantId);
        }

        return SecurityStampValidator.ValidatePrincipalAsync(context);
    }

    private static void AddRolePolicies(AuthorizationOptions options)
    {
        Add(BaseraPolicies.RequireOwner, BaseraRoles.Owner);
        Add(BaseraPolicies.RequireManagement, BaseraRoles.Owner, BaseraRoles.Manager);
        Add(BaseraPolicies.RequireStaff, BaseraRoles.Owner, BaseraRoles.Manager, BaseraRoles.Warden, BaseraRoles.Cook);
        Add(BaseraPolicies.RequireResident, BaseraRoles.Resident);
        Add(BaseraPolicies.RequireParent, BaseraRoles.Parent);

        void Add(string name, params string[] roles) => options.AddPolicy(name, policy => policy
            .RequireAuthenticatedUser()
            .RequireClaim(BaseraClaimTypes.Role, roles));
    }
}
