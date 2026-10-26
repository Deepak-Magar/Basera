using Basera.Shared.Domain;

namespace Basera.Modules.Properties.Domain;

/// <summary>
/// A Manager runs this property (PRODUCT_SPEC §2, §6 rule 8). <see cref="MembershipId"/> is Identity's tenant
/// membership; Properties keeps the link because it owns properties, and Identity's staff page edits it through
/// <c>IStaffPropertyAssignments</c>. Owners need no assignment: they see every property.
/// </summary>
public sealed class ManagerPropertyAssignment : Entity, ITenantOwned, IAuditable
{
    public ManagerPropertyAssignment(Guid membershipId, Guid propertyId)
    {
        MembershipId = membershipId;
        PropertyId = propertyId;
    }

    public Guid TenantId { get; private set; }

    public Guid MembershipId { get; private set; }

    public Guid PropertyId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }
}
