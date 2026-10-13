namespace Basera.UnitTests.Architecture;

/// <summary>Runs each rule against the fixtures to show it passes good code and catches bad code.</summary>
public sealed class RuleSelfTests
{
    private const string Fixtures = "Basera.UnitTests.Architecture.Fixtures";
    private static readonly System.Reflection.Assembly ThisAssembly = typeof(RuleSelfTests).Assembly;
    private static readonly string[] FakeModuleInternals =
        ArchitectureRules.InternalTypeNames(ThisAssembly, $"{Fixtures}.FakeModule");

    [Fact]
    public void Contracts_types_are_not_internal()
    {
        FakeModuleInternals.Should().Contain($"{Fixtures}.FakeModule.Domain.InternalEntity")
            .And.NotContain($"{Fixtures}.FakeModule.Contracts.PublicLookup");
    }

    [Fact]
    public void Boundary_rule_allows_using_contracts()
    {
        ArchitectureRules.TypesDependingOn(ThisAssembly, FakeModuleInternals, $"{Fixtures}.Consumers.Good")
            .Should().BeEmpty();
    }

    [Fact]
    public void Boundary_rule_catches_using_internals()
    {
        ArchitectureRules.TypesDependingOn(ThisAssembly, FakeModuleInternals, $"{Fixtures}.Consumers.Bad")
            .Should().Equal($"{Fixtures}.Consumers.Bad.UsesInternals");
    }

    [Fact]
    public void Domain_rule_catches_framework_dependency()
    {
        ArchitectureRules.DomainTypesUsingFrameworks(ThisAssembly)
            .Should().Equal($"{Fixtures}.FakeModule.Domain.LeakyEntity");
    }
}
