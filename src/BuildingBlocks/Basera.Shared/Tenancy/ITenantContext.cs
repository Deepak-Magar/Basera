namespace Basera.Shared.Tenancy;

/// <summary>
/// The tenant the current request or job acts for. Scoped: one per request, or one per scope a
/// background job creates. In a request it comes only from the signed-in user's <c>tenant_id</c> claim
/// (ADR-003), never from the URL or a form field.
/// </summary>
public interface ITenantContext
{
    Guid? TenantId { get; }

    bool IsResolved { get; }

    /// <summary>The tenant id, or <see cref="TenantNotResolvedException"/> when there is none.</summary>
    Guid RequiredTenantId { get; }
}
