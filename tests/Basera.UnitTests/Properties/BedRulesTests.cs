using Basera.Modules.Properties.Domain;
using Basera.Shared.Results;
using static Basera.UnitTests.Properties.PropertyBuilder;

namespace Basera.UnitTests.Properties;

/// <summary>Reservations and their expiry, one active stay per bed (PRODUCT_SPEC §6 rule 6), maintenance.</summary>
public sealed class BedRulesTests
{
    private readonly Property property = Create();
    private readonly Bed bed;

    public BedRulesTests()
    {
        bed = property.AddRoom("101", RoomType.Single).Bed(1);
    }

    [Fact]
    public void A_reservation_blocks_the_bed_for_seven_days_by_default()
    {
        property.Reserve(bed.Id, Today).IsSuccess.Should().BeTrue();

        bed.ReservedUntil.Should().Be(Today.AddDays(7));
        bed.StatusOn(Today.AddDays(6)).Should().Be(BedStatus.Reserved);
        bed.StatusOn(Today.AddDays(7)).Should().Be(BedStatus.Free, "the reservation has run out");
    }

    [Fact]
    public void The_reservation_length_follows_the_property()
    {
        property.SetReservationDays(2);

        property.Reserve(bed.Id, Today);

        bed.StatusOn(Today.AddDays(1)).Should().Be(BedStatus.Reserved);
        bed.StatusOn(Today.AddDays(2)).Should().Be(BedStatus.Free);
    }

    [Fact]
    public void A_reserved_bed_cannot_be_reserved_again_until_it_runs_out()
    {
        property.Reserve(bed.Id, Today);

        property.Reserve(bed.Id, Today.AddDays(6)).Error!.Code.Should().Be(ErrorCode.RuleViolation);
        property.Reserve(bed.Id, Today.AddDays(7), Guid.NewGuid()).IsSuccess.Should().BeTrue();
        bed.ReservedUntil.Should().Be(Today.AddDays(14));
    }

    [Fact]
    public void Expired_reservations_are_released()
    {
        var other = property.AddRoom("102", RoomType.Single).Bed(1);
        property.Reserve(bed.Id, Today);
        property.Reserve(other.Id, Today.AddDays(3));

        property.ReleaseExpiredReservations(Today.AddDays(7)).Should().Be(1);

        bed.Status.Should().Be(BedStatus.Free);
        bed.ReservedUntil.Should().BeNull();
        other.Status.Should().Be(BedStatus.Reserved);
    }

    [Fact]
    public void A_reservation_can_be_released_by_hand()
    {
        property.Reserve(bed.Id, Today);

        property.Release(bed.Id).IsSuccess.Should().BeTrue();

        bed.Status.Should().Be(BedStatus.Free);
        property.Release(bed.Id).Error!.Code.Should().Be(ErrorCode.RuleViolation, "it is not reserved any more");
    }

    [Fact]
    public void An_inactive_property_takes_no_reservations_or_residents()
    {
        property.Deactivate();

        property.Reserve(bed.Id, Today).Error!.Code.Should().Be(ErrorCode.RuleViolation);
        property.Occupy(bed.Id, Guid.NewGuid(), Today, Today).Error!.Code.Should().Be(ErrorCode.RuleViolation);
    }

    [Fact]
    public void A_stay_moves_into_a_free_bed_and_opens_an_occupancy()
    {
        var stay = Guid.NewGuid();

        var occupancy = property.Occupy(bed.Id, stay, Today, Today).Value;

        bed.Status.Should().Be(BedStatus.Occupied);
        bed.CurrentOccupancy.Should().BeSameAs(occupancy);
        occupancy.StayId.Should().Be(stay);
        occupancy.From.Should().Be(Today);
        occupancy.To.Should().BeNull();
    }

    [Fact]
    public void A_bed_holds_one_active_stay()
    {
        property.Occupy(bed.Id, Guid.NewGuid(), Today, Today);

        var second = property.Occupy(bed.Id, Guid.NewGuid(), Today, Today);

        second.Error!.Code.Should().Be(ErrorCode.RuleViolation);
        bed.Occupancies.Should().ContainSingle();
    }

    [Fact]
    public void The_stay_it_was_reserved_for_can_move_in()
    {
        var stay = Guid.NewGuid();
        property.Reserve(bed.Id, Today, stay);

        property.Occupy(bed.Id, Guid.NewGuid(), Today, Today).Error!.Code.Should().Be(ErrorCode.RuleViolation, "it is held for someone else");
        property.Occupy(bed.Id, stay, Today.AddDays(1), Today.AddDays(1)).IsSuccess.Should().BeTrue();
        bed.ReservedUntil.Should().BeNull();
        bed.ReservedFor.Should().BeNull();
    }

    [Fact]
    public void A_bed_held_by_hand_can_be_filled_by_anyone()
    {
        property.Reserve(bed.Id, Today);

        property.Occupy(bed.Id, Guid.NewGuid(), Today, Today).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Once_a_reservation_runs_out_anyone_can_move_in()
    {
        property.Reserve(bed.Id, Today, Guid.NewGuid());

        property.Occupy(bed.Id, Guid.NewGuid(), Today.AddDays(7), Today.AddDays(7)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Vacating_closes_the_occupancy_and_frees_the_bed_for_the_next_stay()
    {
        var first = Guid.NewGuid();
        var occupancy = property.Occupy(bed.Id, first, Today, Today).Value;

        property.Vacate(bed.Id, first, Today.AddDays(30)).IsSuccess.Should().BeTrue();

        occupancy.To.Should().Be(Today.AddDays(30));
        bed.Status.Should().Be(BedStatus.Free);
        bed.CurrentOccupancy.Should().BeNull();
        property.Occupy(bed.Id, Guid.NewGuid(), Today.AddDays(30), Today.AddDays(30)).IsSuccess.Should().BeTrue();
        bed.Occupancies.Should().HaveCount(2);
    }

    [Fact]
    public void Only_the_stay_in_the_bed_can_leave_it_and_not_before_it_came()
    {
        var stay = Guid.NewGuid();
        property.Occupy(bed.Id, stay, Today, Today);

        property.Vacate(bed.Id, Guid.NewGuid(), Today.AddDays(1)).Error!.Code.Should().Be(ErrorCode.RuleViolation);
        property.Vacate(bed.Id, stay, Today.AddDays(-1)).Error!.Target.Should().Be("on");
        bed.Status.Should().Be(BedStatus.Occupied);
    }

    [Fact]
    public void Maintenance_takes_a_free_bed_out_and_back()
    {
        property.MarkMaintenance(bed.Id, Today).IsSuccess.Should().BeTrue();
        bed.Status.Should().Be(BedStatus.Maintenance);

        property.Reserve(bed.Id, Today).Error!.Code.Should().Be(ErrorCode.RuleViolation);
        property.Occupy(bed.Id, Guid.NewGuid(), Today, Today).Error!.Code.Should().Be(ErrorCode.RuleViolation);

        property.ReturnToService(bed.Id).IsSuccess.Should().BeTrue();
        bed.Status.Should().Be(BedStatus.Free);
        property.ReturnToService(bed.Id).Error!.Code.Should().Be(ErrorCode.RuleViolation);
    }

    [Fact]
    public void An_occupied_or_reserved_bed_cannot_go_to_maintenance()
    {
        property.Reserve(bed.Id, Today);
        property.MarkMaintenance(bed.Id, Today).Error!.Code.Should().Be(ErrorCode.RuleViolation);

        property.Occupy(bed.Id, Guid.NewGuid(), Today, Today);
        property.MarkMaintenance(bed.Id, Today).Error!.Code.Should().Be(ErrorCode.RuleViolation);
    }

    [Fact]
    public void Unknown_beds_are_not_found()
    {
        property.Reserve(Guid.NewGuid(), Today).Error!.Code.Should().Be(ErrorCode.NotFound);
        property.Occupy(Guid.NewGuid(), Guid.NewGuid(), Today, Today).Error!.Code.Should().Be(ErrorCode.NotFound);
    }
}
