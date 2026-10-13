using Basera.Shared.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Basera.Modules.Payments;

public sealed class PaymentsModule : IModule
{
    public string Name => "Payments";

    public void AddModule(IServiceCollection services, IConfiguration configuration)
    {
    }

    public void MapModule(IEndpointRouteBuilder endpoints)
    {
    }
}
