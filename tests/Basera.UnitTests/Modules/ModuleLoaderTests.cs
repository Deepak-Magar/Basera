using Basera.Shared.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Basera.UnitTests.Modules;

public sealed class ModuleLoaderTests
{
    [Fact]
    public void Discover_creates_one_instance_of_each_module_ordered_by_name()
    {
        var modules = ModuleLoader.Discover([typeof(ModuleLoaderTests).Assembly]);

        modules.Select(m => m.Name).Should().Equal("Alpha", "Zulu");
    }

    private sealed class ZuluModule : FakeModule
    {
        public override string Name => "Zulu";
    }

    private sealed class AlphaModule : FakeModule
    {
        public override string Name => "Alpha";
    }

    // Abstract, so Discover must skip it.
    private abstract class FakeModule : IModule
    {
        public abstract string Name { get; }

        public void AddModule(IServiceCollection services, IConfiguration configuration)
        {
        }

        public void MapModule(IEndpointRouteBuilder endpoints)
        {
        }
    }
}
