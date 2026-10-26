using Basera.Shared.Domain;

namespace Basera.Modules.Properties.Domain;

/// <summary>
/// Who was in a bed and when (PRODUCT_SPEC §4.2): one row per stay segment, for the police register and
/// reports. <see cref="StayId"/> is the Residents module's stay. <see cref="To"/> is null while the stay is
/// in the bed; a bed has at most one such open row (§6 rule 6). Never deleted.
/// </summary>
public sealed class BedOccupancy : Entity, ITenantOwned, IAuditable
{
    internal BedOccupancy(Guid bedId, Guid stayId, DateOnly from)
    {
        BedId = bedId;
        StayId = stayId;
        From = from;
    }

    public Guid TenantId { get; private set; }

    public Guid BedId { get; private set; }

    public Guid StayId { get; private set; }

    /// <summary>The check-in day (counts as occupied).</summary>
    public DateOnly From { get; private set; }

    /// <summary>The check-out day (does not count, §6 rule 1), or null while occupied.</summary>
    public DateOnly? To { get; private set; }

    public bool IsOpen => To is null;

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    internal void Close(DateOnly on) => To = on;
}
