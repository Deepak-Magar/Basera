using Basera.Modules.Residents.Application;
using Basera.Modules.Residents.Contracts;
using Basera.Shared.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Basera.Modules.Residents;

public sealed class ResidentsModule : IModule
{
    public string Name => "Residents";

    public void AddModule(IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddScoped<IResidentPhoneLookup, NoResidentPhoneLookup>();
    }

    public void MapModule(IEndpointRouteBuilder endpoints)
    {
    }
}
