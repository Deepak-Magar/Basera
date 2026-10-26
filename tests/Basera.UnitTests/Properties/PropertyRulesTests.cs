using Basera.Modules.Properties.Domain;
using Basera.Shared.Results;
using static Basera.UnitTests.Properties.PropertyBuilder;

namespace Basera.UnitTests.Properties;

/// <summary>Property details, floors, rooms, bed counts per room type and unique bed codes.</summary>
public sealed class PropertyRulesTests
{
    [Fact]
    public void A_property_needs_a_name_and_an_address()
    {
        Property.Create(new PropertyDetails(" ", "Kirtipur", null, null, null)).Error!.Target.Should().Be("name");
        Property.Create(new PropertyDetails("Sunrise", "", null, null, null)).Error!.Target.Should().Be("address");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(41)]
    public void A_ward_is_1_to_40(int ward) =>
        Property.Create(new PropertyDetails("Sunrise", "Kirtipur", ward, null, null)).Error!.Target.Should().Be("ward");

    [Fact]
    public void The_contact_phone_is_optional_but_must_be_a_mobile_when_given()
    {
        Property.Create(new PropertyDetails("Sunrise", "Kirtipur", 3, null, null)).Value.ContactPhone.Should().BeNull();
        Property.Create(new PropertyDetails("Sunrise", "Kirtipur", 3, "9841234567", null)).Value.ContactPhone!.E164.Should().Be("+9779841234567");
        Property.Create(new PropertyDetails("Sunrise", "Kirtipur", 3, "01-4330000", null)).Error!.Target.Should().Be("contactPhone");
    }

    [Fact]
    public void A_new_property_is_active_and_holds_reservations_for_seven_days()
    {
        var property = Create();

        property.IsActive.Should().BeTrue();
        property.ReservationDays.Should().Be(7);
        property.SetReservationDays(0).Error!.Target.Should().Be("reservationDays");
        property.SetReservationDays(3).IsSuccess.Should().BeTrue();
        property.ReservationDays.Should().Be(3);
    }

    [Fact]
    public void Floor_numbers_are_unique_and_0_to_99()
    {
        var property = Create();

        property.AddFloor(1, null).Error!.Code.Should().Be(ErrorCode.Conflict);
        property.AddFloor(-1, null).Error!.Target.Should().Be("number");
        property.AddFloor(100, null).Error!.Target.Should().Be("number");
        property.AddFloor(0, "Ground").Value.Label.Should().Be("Ground");
        property.Floors.Select(f => f.Number).Should().Equal(0, 1, 2);
    }

    [Fact]
    public void A_floor_with_rooms_cannot_be_removed()
    {
        var property = Create();
        property.AddRoom("101");
        var first = property.Floors.Single(f => f.Number == 1);
        var second = property.Floors.Single(f => f.Number == 2);

        property.RemoveFloor(first.Id).Error!.Code.Should().Be(ErrorCode.RuleViolation);
        property.RemoveFloor(second.Id).IsSuccess.Should().BeTrue();
        property.Floors.Should().ContainSingle();
    }

    [Fact]
    public void A_room_goes_on_an_existing_floor()
    {
        var property = Create();

        property.AddRoom(5, "501", RoomType.Single, null, RoomAttributes.None, null).Error!.Target.Should().Be("floorNumber");
    }

    [Theory]
    [InlineData(RoomType.Single, 1)]
    [InlineData(RoomType.Double, 2)]
    [InlineData(RoomType.Triple, 3)]
    [InlineData(RoomType.Four, 4)]
    public void A_room_has_as_many_beds_as_its_type_says(RoomType type, int beds)
    {
        var room = Create().AddRoom("101", type);

        room.Beds.Select(b => b.Slot).Should().Equal(Enumerable.Range(1, beds));
        room.Beds.Should().OnlyContain(b => b.Status == BedStatus.Free);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(4)]
    [InlineData(21)]
    public void A_dorm_has_5_to_20_beds(int? beds) =>
        Create().AddRoom(1, "D1", RoomType.Dorm, beds, RoomAttributes.None, null).Error!.Target.Should().Be("dormBeds");

    [Fact]
    public void A_dorm_gets_the_number_of_beds_asked_for()
    {
        Create().AddRoom("D1", RoomType.Dorm, dormBeds: 8).Beds.Should().HaveCount(8);
    }

    [Fact]
    public void A_bed_count_given_for_a_non_dorm_room_is_ignored()
    {
        Create().AddRoom(1, "101", RoomType.Single, 12, RoomAttributes.None, null).Value.Beds.Should().ContainSingle();
    }

    [Fact]
    public void Bed_codes_are_floor_room_and_slot()
    {
        var room = Create().AddRoom("201", RoomType.Triple, floor: 2);

        room.Beds.Select(b => b.Code).Should().Equal("B2-201-1", "B2-201-2", "B2-201-3");
    }

    [Fact]
    public void Room_numbers_are_upper_cased_letters_and_digits()
    {
        var property = Create();

        property.AddRoom("12a").Number.Should().Be("12A");
        property.AddRoom(1, "1-2", RoomType.Single, null, RoomAttributes.None, null).Error!.Target.Should().Be("number");
        property.AddRoom(1, "", RoomType.Single, null, RoomAttributes.None, null).Error!.Target.Should().Be("number");
        property.AddRoom(1, "12345678901", RoomType.Single, null, RoomAttributes.None, null).Error!.Target.Should().Be("number");
    }

    [Fact]
    public void Room_numbers_are_unique_in_the_property_so_bed_codes_are_too()
    {
        var property = Create();
        property.AddRoom("101");

        var duplicate = property.AddRoom(2, "101", RoomType.Single, null, RoomAttributes.None, null);

        duplicate.Error!.Code.Should().Be(ErrorCode.Conflict);
        property.Rooms.Should().ContainSingle();
        property.Beds.Select(b => b.Code).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Renaming_a_room_to_a_taken_number_is_refused()
    {
        var property = Create();
        property.AddRoom("101");
        var other = property.AddRoom("102");

        property.UpdateRoom(other.Id, "101", RoomType.Double, null, RoomAttributes.None, null, Today).Error!.Code.Should().Be(ErrorCode.Conflict);
        other.Number.Should().Be("102");
    }

    [Fact]
    public void Renumbering_a_floor_moves_its_rooms_and_recodes_their_beds()
    {
        var property = Create();
        var room = property.AddRoom("101");
        var floor = property.Floors.Single(f => f.Number == 1);

        property.UpdateFloor(floor.Id, 3, "Third").IsSuccess.Should().BeTrue();

        room.FloorNumber.Should().Be(3);
        room.Beds.Select(b => b.Code).Should().Equal("B3-101-1", "B3-101-2");
        property.UpdateFloor(floor.Id, 2, null).Error!.Code.Should().Be(ErrorCode.Conflict, "floor 2 exists");
    }

    [Fact]
    public void Moving_a_room_recodes_its_beds()
    {
        var property = Create();
        var room = property.AddRoom("101", RoomType.Single);

        property.MoveRoom(room.Id, 2).IsSuccess.Should().BeTrue();

        room.Bed(1).Code.Should().Be("B2-101-1");
        property.MoveRoom(room.Id, 9).Error!.Target.Should().Be("floorNumber");
    }

    [Fact]
    public void Attributes_are_kept_together()
    {
        var room = Create().AddRoom(1, "101", RoomType.Single, null, RoomAttributes.AttachedBath | RoomAttributes.Window, " quiet ").Value;

        room.Has(RoomAttributes.AttachedBath).Should().BeTrue();
        room.Has(RoomAttributes.Window).Should().BeTrue();
        room.Has(RoomAttributes.Balcony).Should().BeFalse();
        room.Notes.Should().Be("quiet");
    }

    [Fact]
    public void A_bigger_type_adds_beds_and_a_smaller_one_removes_the_last_free_ones()
    {
        var property = Create();
        var room = property.AddRoom("101", RoomType.Double);
        var first = room.Bed(1);

        property.UpdateRoom(room.Id, "101", RoomType.Four, null, RoomAttributes.None, null, Today).IsSuccess.Should().BeTrue();
        room.Beds.Select(b => b.Code).Should().Equal("B1-101-1", "B1-101-2", "B1-101-3", "B1-101-4");

        property.UpdateRoom(room.Id, "101", RoomType.Single, null, RoomAttributes.None, null, Today).IsSuccess.Should().BeTrue();
        room.Beds.Should().ContainSingle().Which.Should().BeSameAs(first, "the first bed stays");
    }

    [Fact]
    public void A_room_cannot_shrink_below_a_bed_in_use()
    {
        var property = Create();
        var room = property.AddRoom("101", RoomType.Triple);
        property.Occupy(room.Bed(3).Id, Guid.NewGuid(), Today, Today).IsSuccess.Should().BeTrue();

        var shrink = property.UpdateRoom(room.Id, "101", RoomType.Double, null, RoomAttributes.None, null, Today);

        shrink.Error!.Code.Should().Be(ErrorCode.RuleViolation);
        room.Type.Should().Be(RoomType.Triple);
        room.Beds.Should().HaveCount(3);
    }

    [Fact]
    public void Renaming_a_room_recodes_its_beds()
    {
        var property = Create();
        var room = property.AddRoom("101", RoomType.Single);

        property.UpdateRoom(room.Id, "105", RoomType.Single, null, RoomAttributes.None, null, Today).IsSuccess.Should().BeTrue();

        room.Bed(1).Code.Should().Be("B1-105-1");
    }

    [Fact]
    public void A_room_with_a_bed_in_use_cannot_be_removed()
    {
        var property = Create();
        var busy = property.AddRoom("101");
        var free = property.AddRoom("102");
        property.Reserve(busy.Bed(2).Id, Today).IsSuccess.Should().BeTrue();

        property.RemoveRoom(busy.Id, Today).Error!.Code.Should().Be(ErrorCode.RuleViolation);
        property.RemoveRoom(free.Id, Today).IsSuccess.Should().BeTrue();
        property.Rooms.Should().ContainSingle().Which.Should().BeSameAs(busy);
    }

    [Fact]
    public void A_property_with_a_bed_in_use_cannot_be_removed()
    {
        var property = Create();
        var room = property.AddRoom("101");
        property.CanBeRemoved(Today).IsSuccess.Should().BeTrue();

        property.Occupy(room.Bed(1).Id, Guid.NewGuid(), Today, Today);

        property.CanBeRemoved(Today).Error!.Code.Should().Be(ErrorCode.RuleViolation);
    }
}
