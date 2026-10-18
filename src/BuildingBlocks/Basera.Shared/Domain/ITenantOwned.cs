namespace Basera.Shared.Domain;

/// <summary>
/// A row that belongs to one tenant (hostel business). <c>TenantDbContext</c> filters every query by the
/// current tenant and sets <see cref="TenantId"/> on insert, so entities expose it with a private setter
/// and domain code never assigns it.
/// </summary>
public interface ITenantOwned
{
    Guid TenantId { get; }
}
