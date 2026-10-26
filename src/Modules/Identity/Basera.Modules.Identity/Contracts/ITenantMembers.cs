namespace Basera.Modules.Identity.Contracts;

/// <summary>A user's membership in the current tenant: the id other modules key their own data by, and the role.</summary>
public sealed record MemberSummary(Guid MembershipId, Guid UserId, string DisplayName, string Role);

/// <summary>
/// Who a signed-in user is in the current tenant, for modules that keep data per staff member (the
/// Properties module's manager assignments). Tenant-filtered: a user of another tenant is not found.
/// </summary>
public interface ITenantMembers
{
    Task<MemberSummary?> FindByUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
