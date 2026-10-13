using System.Reflection;
using Basera.Shared.Modules;

namespace Basera.UnitTests.Architecture;

public sealed class ModuleBoundaryTests
{
    // Loaded the same way the host loads them, so a new module is picked up automatically.
    private static readonly IReadOnlyList<Assembly> Modules =
        ModuleLoader.LoadModuleAssemblies(AppContext.BaseDirectory);

    public static TheoryData<string> ModuleNames() => new(Modules.Select(m => m.GetName().Name!));

    [Fact]
    public void All_eleven_modules_are_inspected()
    {
        // Guards against a module silently missing from the test project's references.
        Modules.Should().HaveCount(11);
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Module_uses_other_modules_only_through_their_contracts(string moduleName)
    {
        var module = Modules.Single(m => m.GetName().Name == moduleName);
        var forbidden = Modules
            .Where(other => other != module)
            .SelectMany(other => ArchitectureRules.InternalTypeNames(other, other.GetName().Name!))
            .ToArray();

        var violations = ArchitectureRules.TypesDependingOn(module, forbidden);

        violations.Should().BeEmpty("{0} may only use other modules' Contracts namespace", moduleName);
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Module_domain_has_no_framework_dependencies(string moduleName)
    {
        var module = Modules.Single(m => m.GetName().Name == moduleName);

        ArchitectureRules.DomainTypesUsingFrameworks(module).Should().BeEmpty();
    }

    [Fact]
    public void Shared_domain_has_no_framework_dependencies()
    {
        ArchitectureRules.DomainTypesUsingFrameworks(typeof(IModule).Assembly).Should().BeEmpty();
    }
}
