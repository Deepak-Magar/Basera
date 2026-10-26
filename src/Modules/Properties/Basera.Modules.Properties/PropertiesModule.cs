using Basera.Modules.Properties.Infrastructure;
using Basera.Shared.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Basera.Modules.Properties;

public sealed class PropertiesModule : IModule
{
    public string Name => "Properties";

    public void AddModule(IServiceCollection services, IConfiguration configuration)
    {
        services.AddBaseraProperties(configuration);
    }

    public void MapModule(IEndpointRouteBuilder endpoints)
    {
    }
}
