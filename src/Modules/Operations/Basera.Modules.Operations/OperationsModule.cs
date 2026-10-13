using Basera.Shared.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Basera.Modules.Operations;

public sealed class OperationsModule : IModule
{
    public string Name => "Operations";

    public void AddModule(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void MapModule(IEndpointRouteBuilder endpoints)
    {
    }
}
