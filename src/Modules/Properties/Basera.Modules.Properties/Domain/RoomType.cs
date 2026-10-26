using Basera.Modules.Properties.Contracts;
using Basera.Shared.Results;

namespace Basera.Modules.Properties.Domain;

/// <summary>What a room may have besides beds. Several at once.</summary>
[Flags]
public enum RoomAttributes
{
    None = 0,
    AttachedBath = 1,
    Balcony = 2,
    AirConditioning = 4,
    Window = 8,
}

public static class RoomTypes
{
    /// <summary>A dorm has more beds than a four-bed room, and no more than this.</summary>
    public const int DormMinBeds = 5;

    public const int DormMaxBeds = 20;

    public static readonly IReadOnlyList<RoomType> All = Enum.GetValues<RoomType>();

    public static readonly IReadOnlyList<RoomAttributes> AllAttributes =
        [RoomAttributes.AttachedBath, RoomAttributes.Balcony, RoomAttributes.AirConditioning, RoomAttributes.Window];

    /// <summary>
    /// The number of beds a room of <paramref name="type"/> has: 1 to 4 for single to four, and for a dorm the
    /// number the owner gives (<see cref="DormMinBeds"/> to <see cref="DormMaxBeds"/>). A count given for a
    /// non-dorm room is ignored.
    /// </summary>
    public static Result<int> BedCount(RoomType type, int? dormBeds) => type switch
    {
        RoomType.Single => 1,
        RoomType.Double => 2,
        RoomType.Triple => 3,
        RoomType.Four => 4,
        RoomType.Dorm when dormBeds is >= DormMinBeds and <= DormMaxBeds => dormBeds.Value,
        RoomType.Dorm => DomainError.Validation($"A dorm has {DormMinBeds} to {DormMaxBeds} beds.", "dormBeds"),
        _ => DomainError.Validation("Choose a room type.", "type"),
    };
}
