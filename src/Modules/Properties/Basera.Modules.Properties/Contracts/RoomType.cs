namespace Basera.Modules.Properties.Contracts;

/// <summary>
/// Room types of PRODUCT_SPEC §4.2. In Contracts because other modules speak of them too (a resident wants a
/// double room, a waiting-list entry prefers a dorm); the Properties domain uses this same type. How many beds
/// each type has is a domain rule (<c>RoomTypes.BedCount</c>).
/// </summary>
public enum RoomType
{
    Single,
    Double,
    Triple,
    Four,
    Dorm,
}
