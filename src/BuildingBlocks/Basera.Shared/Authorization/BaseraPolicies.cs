using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Basera.Shared.Authorization;

/// <summary>Authorization policy names. Identity adds the tenant role policies of ARCHITECTURE §7.</summary>
public static class BaseraPolicies
{
    /// <summary>
    /// Basera staff only: the Hangfire dashboard and, later, the /admin area. Until Identity defines the
    /// platform admin role, it allows every request in Development and no request anywhere else.
    /// </summary>
    public const string PlatformAdmin = "PlatformAdmin";

    public static IServiceCollection AddBaseraPolicies(this IServiceCollection services)
    {
        services.AddAuthorization(options => options.AddPolicy(PlatformAdmin, policy => policy.RequireAssertion(context =>
            context.Resource is HttpContext http && http.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment())));
        return services;
    }
}
