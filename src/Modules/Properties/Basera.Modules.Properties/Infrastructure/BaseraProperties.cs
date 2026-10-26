using Basera.Modules.Properties.Application;
using Basera.Modules.Properties.Contracts;
using Basera.Shared.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Basera.Modules.Properties.Infrastructure;

public static class BaseraProperties
{
    /// <summary>
    /// The Properties module's services: its DbContext on the shared database (schema <c>properties</c>), the
    /// application services the screens use, and the contracts other modules call.
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
        return services;
    }
}
