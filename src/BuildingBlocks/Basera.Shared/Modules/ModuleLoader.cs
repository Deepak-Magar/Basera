using System.Reflection;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Basera.Shared.Modules;

public static class ModuleLoader
{
    /// <summary>File name pattern of module assemblies next to the host executable.</summary>
    public const string ModuleAssemblyPattern = "Basera.Modules.*.dll";

    /// <summary>
    /// Loads every module assembly found in <paramref name="directory"/>. Referenced assemblies are
    /// only loaded by .NET when first used, so we cannot rely on what is already in the AppDomain.
    /// </summary>
    public static IReadOnlyList<Assembly> LoadModuleAssemblies(string directory) =>
        Directory.GetFiles(directory, ModuleAssemblyPattern)
            .Select(path => Assembly.Load(AssemblyName.GetAssemblyName(path)))
            .ToList();

    /// <summary>Creates one instance of every concrete <see cref="IModule"/> in the given assemblies, ordered by name.</summary>
    public static IReadOnlyList<IModule> Discover(IEnumerable<Assembly> assemblies) =>
        assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IModule).IsAssignableFrom(t))
            .Select(t => (IModule)Activator.CreateInstance(t)!)
            .OrderBy(m => m.Name, StringComparer.Ordinal)
            .ToList();

    /// <summary>Discovers modules next to the host, calls <see cref="IModule.AddModule"/> on each and registers the <see cref="ModuleCatalog"/>.</summary>
    public static IServiceCollection AddModules(this IServiceCollection services, IConfiguration configuration)
    {
        var modules = Discover(LoadModuleAssemblies(AppContext.BaseDirectory));
        foreach (var module in modules)
        {
            module.AddModule(services, configuration);
        }

        services.AddSingleton(new ModuleCatalog(modules));
        return services;
    }

    /// <summary>Calls <see cref="IModule.MapModule"/> on every module registered by <see cref="AddModules"/>.</summary>
    public static IEndpointRouteBuilder MapModules(this IEndpointRouteBuilder endpoints)
    {
        foreach (var module in endpoints.ServiceProvider.GetRequiredService<ModuleCatalog>().Modules)
        {
            module.MapModule(endpoints);
        }

        return endpoints;
    }
}
