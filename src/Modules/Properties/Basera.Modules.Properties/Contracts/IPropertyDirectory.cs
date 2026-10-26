namespace Basera.Modules.Properties.Contracts;

/// <summary>A property as other modules see it: enough to list it, label a document or pick it in a form.</summary>
public sealed record PropertySummary(Guid Id, string Name, string Address, bool IsActive);

/// <summary>The current tenant's properties (branches), by name. Removed properties are not listed.</summary>
public interface IPropertyDirectory
{
    Task<IReadOnlyList<PropertySummary>> ListAsync(CancellationToken cancellationToken = default);

    Task<PropertySummary?> FindAsync(Guid propertyId, CancellationToken cancellationToken = default);
}
