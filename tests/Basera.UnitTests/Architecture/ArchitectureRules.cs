using System.Reflection;
using NetArchTest.Rules;

namespace Basera.UnitTests.Architecture;

/// <summary>
/// The rules behind the architecture tests, kept separate so the fixture tests can prove each rule
/// really fails on bad code (a rule that can never fail protects nothing).
/// </summary>
internal static class ArchitectureRules
{
    /// <summary>Layers that must stay free of persistence and web frameworks.</summary>
    public static readonly string[] FrameworkNamespaces = ["Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore"];

    /// <summary>Matches a namespace segment named "Domain", e.g. Basera.Modules.Billing.Domain.Invoices.</summary>
    public const string DomainNamespacePattern = @"\.Domain(\.|$)";

    /// <summary>
    /// Full names of the types in a module that other modules must not use:
    /// everything except the module's <c>&lt;root&gt;.Contracts</c> namespace.
    /// </summary>
    public static string[] InternalTypeNames(Assembly module, string rootNamespace)
    {
        var contracts = rootNamespace + ".Contracts";
        return module.GetTypes()
            .Where(t => t.FullName is not null && t.Namespace is not null && t.Namespace.StartsWith(rootNamespace, StringComparison.Ordinal))
            .Where(t => t.Namespace != contracts && !t.Namespace!.StartsWith(contracts + ".", StringComparison.Ordinal))
            .Select(t => t.FullName!)
            .ToArray();
    }

    /// <summary>Types in <paramref name="source"/> (optionally limited to one namespace) that use any of <paramref name="forbiddenTypeNames"/>.</summary>
    public static IReadOnlyList<string> TypesDependingOn(Assembly source, string[] forbiddenTypeNames, string? inNamespace = null)
    {
        if (forbiddenTypeNames.Length == 0)
        {
            return [];
        }

        var types = Types.InAssembly(source);
        var selection = inNamespace is null
            ? types.ShouldNot()
            : types.That().ResideInNamespace(inNamespace).ShouldNot();

        return selection.HaveDependencyOnAny(forbiddenTypeNames).GetResult().FailingTypeNames ?? [];
    }

    /// <summary>Types in a Domain namespace of <paramref name="source"/> that use EF Core or ASP.NET Core.</summary>
    public static IReadOnlyList<string> DomainTypesUsingFrameworks(Assembly source) =>
        Types.InAssembly(source)
            .That().ResideInNamespaceMatching(DomainNamespacePattern)
            .ShouldNot().HaveDependencyOnAny(FrameworkNamespaces)
            .GetResult().FailingTypeNames ?? [];
}
