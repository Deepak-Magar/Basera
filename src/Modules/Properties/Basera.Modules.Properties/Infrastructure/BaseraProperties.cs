using Basera.Shared.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Basera.Modules.Properties.Infrastructure;

public static class BaseraProperties
{
    /// <summary>The Properties module's services: its DbContext on the shared database (schema <c>properties</c>).</summary>
    public static IServiceCollection AddBaseraProperties(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<PropertiesDbContext>(configuration);
        return services;
    }
}
