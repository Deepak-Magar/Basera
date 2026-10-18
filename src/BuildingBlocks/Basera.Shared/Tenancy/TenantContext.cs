namespace Basera.Shared.Tenancy;

/// <summary>
/// The scoped <see cref="ITenantContext"/>. <see cref="TenantResolutionMiddleware"/> sets it from the
/// claim; a background job working through tenants creates one scope per tenant and calls <see cref="Set"/>.
/// </summary>
public sealed class TenantContext : ITenantContext
{
    public Guid? TenantId { get; private set; }

    public bool IsResolved => TenantId is not null;

    public Guid RequiredTenantId => TenantId ?? throw new TenantNotResolvedException();

    /// <summary>
    /// Sets the tenant for this scope. Switching to a different tenant within the same scope is refused:
    /// entities already loaded belong to the first one, so continuing would mix tenants.
    /// </summary>
    public void Set(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("A tenant id cannot be empty.", nameof(tenantId));
        }

        if (TenantId is { } current && current != tenantId)
        {
            throw new InvalidOperationException(
                $"This scope already acts for tenant {current}; create a new scope for tenant {tenantId}.");
        }

        TenantId = tenantId;
    }
}
