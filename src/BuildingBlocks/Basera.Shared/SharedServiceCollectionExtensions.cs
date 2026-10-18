using Basera.Shared.Persistence;
using Basera.Shared.Tenancy;
using Basera.Shared.Time;
using Basera.Shared.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Basera.Shared;

public static class SharedServiceCollectionExtensions
{
    /// <summary>Registers the building blocks every module may use.</summary>
    public static IServiceCollection AddBaseraShared(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IClock, SystemClock>();

        services.AddHttpContextAccessor();
        services.TryAddScoped<TenantContext>();
        services.TryAddScoped<ITenantContext>(provider => provider.GetRequiredService<TenantContext>());
        services.TryAddScoped<ICurrentUser, HttpCurrentUser>();
        services.TryAddScoped<TenantInterceptor>();
        services.TryAddScoped<AuditInterceptor>();
        return services;
    }

    /// <summary>Resolves the tenant from the signed-in user. Place after authentication, before authorization.</summary>
    public static IApplicationBuilder UseBaseraTenancy(this IApplicationBuilder app) =>
        app.UseMiddleware<TenantResolutionMiddleware>();
}
