using Basera.Shared.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Basera.Modules.Residents;

public sealed class ResidentsModule : IModule
{
    public string Name => "Residents";

    public void AddModule(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void MapModule(IEndpointRouteBuilder endpoints)
    {
    }
}
