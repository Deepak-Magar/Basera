namespace Basera.Shared.Modules;

/// <summary>The modules loaded at startup, registered as a singleton so pages can list them.</summary>
public sealed class ModuleCatalog(IReadOnlyList<IModule> modules)
{
    public IReadOnlyList<IModule> Modules { get; } = modules;
}
