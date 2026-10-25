using Basera.Shared.Domain;

namespace Basera.Modules.Identity.Domain;

/// <summary>
/// A user's role in a tenant; the <c>role</c> claim comes from here, not from ASP.NET Identity roles.
/// One membership per user and tenant.
/// </summary>
public sealed class TenantMembership : Entity, ITenantOwned, IAuditable
{
    public TenantMembership(Guid userId, Role role)
    {
        UserId = userId;
        Role = role;
    }

    public Guid TenantId { get; private set; }

    public Guid UserId { get; private set; }

    public Role Role { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }
}
