using Basera.Modules.Identity.Contracts;
using Basera.Modules.Properties.Application;
using Basera.Modules.Properties.Contracts;
using Basera.Shared.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Basera.Modules.Properties.Infrastructure;

public static class BaseraProperties
{
    /// <summary>
    /// The Properties module's services: its DbContext on the shared database (schema <c>properties</c>), the
    /// application services the screens use, the contracts other modules call, property access authorization,
    /// and the answer to Identity's staff property picker.
    /// </summary>
    public static IServiceCollection AddBaseraProperties(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<PropertiesDbContext>(configuration);
        services.AddScoped<PropertyStore>();
        services.AddScoped<PropertyService>();
        services.AddScoped<RoomService>();
        services.AddScoped<BedService>();
        services.AddScoped<RatePlanService>();
        services.AddScoped<IBedAvailability, BedAvailability>();
        services.AddScoped<IPropertyDirectory, PropertyDirectory>();
        services.AddScoped<IRateResolver, RateResolver>();
        services.AddScoped<PropertyAccess>();
        services.AddHttpContextAccessor();
        services.AddSingleton<IAuthorizationHandler, PropertyAccessHandler>();
        // Replace, not TryAdd: Identity is added first and has already registered its stub (learning doc 13).
        services.Replace(ServiceDescriptor.Scoped<IStaffPropertyAssignments, StaffPropertyAssignments>());
        return services;
    }
}
