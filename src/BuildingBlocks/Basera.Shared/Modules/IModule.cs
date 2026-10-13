using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Basera.Shared.Modules;

/// <summary>
/// The entry point of a module. The host finds every implementation by assembly scanning,
/// calls <see cref="AddModule"/> while building the service container and
/// <see cref="MapModule"/> after routing is configured.
/// </summary>
public interface IModule
{
    /// <summary>Short name, also used as the database schema and job id prefix (e.g. "Billing").</summary>
    string Name { get; }

    void AddModule(IServiceCollection services, IConfiguration configuration);

    void MapModule(IEndpointRouteBuilder endpoints);
}
