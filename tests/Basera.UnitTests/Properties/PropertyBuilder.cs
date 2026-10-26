using Basera.Modules.Properties.Contracts;
using Basera.Modules.Properties.Domain;

namespace Basera.UnitTests.Properties;

/// <summary>A property with floors 1 and 2 and whatever rooms a test adds.</summary>
internal static class PropertyBuilder
{
    public static readonly DateOnly Today = new(2026, 10, 26);

    public static Property Create(string name = "Sunrise Boys Hostel")
    {
        var property = Property.Create(new PropertyDetails(name, "Kirtipur-3, Kathmandu", 3, "9800000000", "Gate closes at 9 pm.")).Value;
        property.AddFloor(1, "First").IsSuccess.Should().BeTrue();
        property.AddFloor(2, null).IsSuccess.Should().BeTrue();
        return property;
    }

    public static Room AddRoom(this Property property, string number, RoomType type = RoomType.Double, int floor = 1, int? dormBeds = null)
    {
        var room = property.AddRoom(floor, number, type, dormBeds, RoomAttributes.None, null);
        room.IsSuccess.Should().BeTrue(room.Error?.Message);
        return room.Value;
    }

    public static Bed Bed(this Room room, int slot) => room.Beds.Single(b => b.Slot == slot);
}
