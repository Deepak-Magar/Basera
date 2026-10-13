using Basera.Shared.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Basera.Modules.Mess;

public sealed class MessModule : IModule
{
    public string Name => "Mess";

    public void AddModule(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void MapModule(IEndpointRouteBuilder endpoints)
    {
    }
}
