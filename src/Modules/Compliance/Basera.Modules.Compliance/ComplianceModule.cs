using Basera.Shared.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Basera.Modules.Compliance;

public sealed class ComplianceModule : IModule
{
    public string Name => "Compliance";

    public void AddModule(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void MapModule(IEndpointRouteBuilder endpoints)
    {
    }
}
