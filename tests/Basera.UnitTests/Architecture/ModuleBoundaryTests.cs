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
    public void Identity_references_no_other_module()
    {
        // Identity is the root of the module graph: others use its Contracts (and implement contracts it owns,
        // such as IResidentPhoneLookup), never the other way round, so no project reference cycle can appear.
        var identity = Modules.Single(m => m.GetName().Name == "Basera.Modules.Identity");

        identity.GetReferencedAssemblies().Select(a => a.Name)
            .Should().NotContain(name => name!.StartsWith("Basera.Modules.", StringComparison.Ordinal));
    }

    [Fact]
    public void Shared_domain_has_no_framework_dependencies()
    {
        ArchitectureRules.DomainTypesUsingFrameworks(typeof(IModule).Assembly).Should().BeEmpty();
    }
}
