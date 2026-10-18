namespace Basera.Shared.Domain;

/// <summary>
/// A row that is hidden rather than deleted. Removing it through a <c>TenantDbContext</c> sets
/// <see cref="DeletedAt"/> instead, and queries skip it unless they call <c>IgnoreQueryFilters()</c>.
/// Only Resident, Property, Room, Bed and Notice may implement this (ARCHITECTURE §6); financial rows never do.
/// </summary>
public interface ISoftDeletable
{
    DateTimeOffset? DeletedAt { get; }

    Guid? DeletedBy { get; }
}
