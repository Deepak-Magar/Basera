using Basera.Shared.Results;

namespace Basera.Modules.Identity.Contracts;

/// <summary>One of the tenant's properties, and whether the staff member is assigned to it.</summary>
public sealed record AssignableProperty(Guid PropertyId, string Name, bool IsAssigned);

/// <summary>
/// Which properties a Manager runs (PRODUCT_SPEC §2: "runs one property for an owner with several"). The
/// owner's staff page asks; the Properties module, which owns properties and the assignments, answers. Identity
/// owns this contract because Identity is the one asking (learning doc 13); Properties implements it and
/// registers it with <c>services.Replace</c>, replacing Identity's stub that knows no properties.
/// </summary>
public interface IStaffPropertyAssignments
{
    /// <summary>Every property of the tenant, by name, marked with the membership's assignments.</summary>
    Task<IReadOnlyList<AssignableProperty>> ListAsync(Guid membershipId, CancellationToken cancellationToken = default);

    /// <summary>Replaces the membership's assignments with <paramref name="propertyIds"/> (empty clears them).</summary>
    Task<Result> SetAsync(Guid membershipId, IReadOnlyCollection<Guid> propertyIds, CancellationToken cancellationToken = default);
}
