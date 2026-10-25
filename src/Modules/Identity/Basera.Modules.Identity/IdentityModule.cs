using Basera.Modules.Identity.Infrastructure;
using Basera.Shared.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Basera.Modules.Identity;

public sealed class IdentityModule : IModule
{
    public string Name => "Identity";

    public void AddModule(IServiceCollection services, IConfiguration configuration)
    {
        services.AddBaseraIdentity(configuration);
    }

    public void MapModule(IEndpointRouteBuilder endpoints)
    {
    }
}
