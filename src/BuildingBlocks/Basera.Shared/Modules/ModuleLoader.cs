using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Basera.Shared.Modules;

public static class ModuleLoader
{
    /// <summary>File name pattern of module assemblies next to the host executable.</summary>
    public const string ModuleAssemblyPattern = "Basera.Modules.*.dll";

    /// <summary>
    /// Where a module's views live: <c>Presentation/Views/&lt;Area&gt;/&lt;Controller&gt;/&lt;Action&gt;.cshtml</c> in the
    /// module project. All modules share this virtual path, so controller names must be unique per area.
    /// Layouts are found in the host's <c>Areas/&lt;Area&gt;/Views/Shared</c> as usual.
    /// </summary>
    public const string ModuleAreaViewLocation = "/Presentation/Views/{2}/{1}/{0}.cshtml";

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
        services.AddModuleMvcParts(modules.Select(module => module.GetType().Assembly).Distinct());
        return services;
    }

    /// <summary>
    /// Makes each module's controllers and compiled views part of MVC, without relying on MVC's own
    /// discovery of referenced assemblies. A part MVC already found is not added twice: duplicate
    /// controllers would make every route ambiguous.
    /// </summary>
    private static void AddModuleMvcParts(this IServiceCollection services, IEnumerable<Assembly> assemblies)
    {
        services.AddMvcCore().ConfigureApplicationPartManager(manager =>
        {
            foreach (var part in assemblies.SelectMany(a => ApplicationPartFactory.GetApplicationPartFactory(a).GetApplicationParts(a)))
            {
                if (!manager.ApplicationParts.Any(existing => existing.GetType() == part.GetType() && existing.Name == part.Name))
                {
                    manager.ApplicationParts.Add(part);
                }
            }
        });
        services.Configure<RazorViewEngineOptions>(options => options.AreaViewLocationFormats.Add(ModuleAreaViewLocation));
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
