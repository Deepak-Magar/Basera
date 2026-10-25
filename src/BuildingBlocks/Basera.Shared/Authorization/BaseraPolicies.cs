using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Basera.Shared.Authorization;

/// <summary>
/// Authorization policy names. The tenant role policies of ARCHITECTURE §7 are registered by the Identity
/// module; their names live here so any module's controller can say <c>[Authorize(Policy = ...)]</c>.
/// </summary>
public static class BaseraPolicies
{
    /// <summary>
    /// Basera staff only: the Hangfire dashboard and, later, the /admin area. Until Identity defines the
    /// platform admin role, it allows every request in Development and no request anywhere else.
    /// </summary>
    public const string PlatformAdmin = "PlatformAdmin";

    /// <summary>Owner.</summary>
    public const string RequireOwner = "RequireOwner";

    /// <summary>Owner or Manager.</summary>
    public const string RequireManagement = "RequireManagement";

    /// <summary>Owner, Manager, Warden or Cook.</summary>
    public const string RequireStaff = "RequireStaff";

    /// <summary>Resident.</summary>
    public const string RequireResident = "RequireResident";

    /// <summary>Parent.</summary>
    public const string RequireParent = "RequireParent";

    public static IServiceCollection AddBaseraPolicies(this IServiceCollection services)
    {
        services.AddAuthorization(options => options.AddPolicy(PlatformAdmin, policy => policy.RequireAssertion(context =>
            context.Resource is HttpContext http && http.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment())));
        return services;
    }
}
