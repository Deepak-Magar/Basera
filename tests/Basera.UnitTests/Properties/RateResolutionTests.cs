using Basera.Modules.Properties.Contracts;
using Basera.Modules.Properties.Domain;
using Basera.Shared.Domain;
using Basera.Shared.Domain.Calendar;
using Basera.Shared.Results;
using static Basera.UnitTests.Properties.PropertyBuilder;

namespace Basera.UnitTests.Properties;

/// <summary>A bed's monthly rate: its own override, else the plan in effect on the day (PRODUCT_SPEC §4.2).</summary>
public sealed class RateResolutionTests
{
    private static readonly NepaliDate Baisakh1 = new(2083, 1, 1);
    private static readonly NepaliDate Shrawan1 = new(2083, 4, 1);

    private readonly Property property = Create();
    private readonly Room doubleRoom;
    private readonly Room dorm;

    public RateResolutionTests()
    {
        doubleRoom = property.AddRoom("101", RoomType.Double);
        dorm = property.AddRoom("D1", RoomType.Dorm, dormBeds: 6);
    }

    [Fact]
    public void Without_a_plan_or_an_override_there_is_no_rate()
    {
        property.MonthlyRate(doubleRoom.Bed(1).Id, Today).Error!.Code.Should().Be(ErrorCode.RuleViolation);
    }

    [Fact]
    public void A_bed_takes_the_rate_for_its_room_type_from_the_plan()
    {
        AddPlan("2083", Baisakh1, (RoomType.Double, 9_000), (RoomType.Dorm, 6_000));

        property.MonthlyRate(doubleRoom.Bed(1).Id, Today).Value.Should().Be(Money.FromRupees(9_000));
        property.MonthlyRate(dorm.Bed(4).Id, Today).Value.Should().Be(Money.FromRupees(6_000));
    }

    [Fact]
    public void The_plan_in_effect_is_the_latest_one_started_on_or_before_the_day()
    {
        AddPlan("2083 spring", Baisakh1, (RoomType.Double, 9_000));
        AddPlan("2083 monsoon", Shrawan1, (RoomType.Double, 9_500));
        var bedId = doubleRoom.Bed(1).Id;

        property.MonthlyRate(bedId, Shrawan1.AddDays(-1).ToDateOnly()).Value.Should().Be(Money.FromRupees(9_000));
        property.MonthlyRate(bedId, Shrawan1.ToDateOnly()).Value.Should().Be(Money.FromRupees(9_500), "a plan starts on its own date");
        property.MonthlyRate(bedId, Baisakh1.AddDays(-1).ToDateOnly()).IsFailure.Should().BeTrue("no plan had started yet");
        property.PlanInEffect(Shrawan1.AddDays(10).ToDateOnly())!.Name.Should().Be("2083 monsoon");
    }

    [Fact]
    public void A_bed_override_beats_the_plan()
    {
        AddPlan("2083", Baisakh1, (RoomType.Double, 9_000));
        var corner = doubleRoom.Bed(2);

        property.SetBedRate(corner.Id, Money.FromRupees(10_500)).IsSuccess.Should().BeTrue();

        property.MonthlyRate(corner.Id, Today).Value.Should().Be(Money.FromRupees(10_500));
        property.MonthlyRate(doubleRoom.Bed(1).Id, Today).Value.Should().Be(Money.FromRupees(9_000));
        property.MonthlyRate(corner.Id, Baisakh1.AddDays(-30).ToDateOnly()).Value.Should().Be(Money.FromRupees(10_500), "an override needs no plan");
    }

    [Fact]
    public void Clearing_the_override_falls_back_to_the_plan()
    {
        AddPlan("2083", Baisakh1, (RoomType.Double, 9_000));
        var bedId = doubleRoom.Bed(1).Id;
        property.SetBedRate(bedId, Money.FromRupees(10_000));

        property.SetBedRate(bedId, null);

        property.MonthlyRate(bedId, Today).Value.Should().Be(Money.FromRupees(9_000));
    }

    [Fact]
    public void An_override_must_be_positive()
    {
        property.SetBedRate(doubleRoom.Bed(1).Id, Money.Zero).Error!.Target.Should().Be("rateOverride");
    }

    [Fact]
    public void A_plan_without_the_room_type_gives_no_rate()
    {
        AddPlan("2083", Baisakh1, (RoomType.Double, 9_000));

        property.MonthlyRate(dorm.Bed(1).Id, Today).Error!.Message.Should().Contain("Dorm");
    }

    [Fact]
    public void A_plan_needs_a_name_and_positive_rates_and_its_own_start_date()
    {
        Rates((RoomType.Single, 9_000)).Should().ContainSingle();
        property.AddRatePlan(" ", Baisakh1, Rates((RoomType.Single, 9_000))).Error!.Target.Should().Be("name");
        property.AddRatePlan("Empty", Baisakh1, Rates()).Error!.Target.Should().Be("rates");
        property.AddRatePlan("Free", Baisakh1, Rates((RoomType.Single, 0))).Error!.Target.Should().Be("rateSingle");

        AddPlan("2083", Baisakh1, (RoomType.Single, 9_000));
        property.AddRatePlan("Again", Baisakh1, Rates((RoomType.Single, 8_000))).Error!.Code.Should().Be(ErrorCode.Conflict);
    }

    [Fact]
    public void A_plan_can_be_changed()
    {
        var plan = AddPlan("2083", Baisakh1, (RoomType.Double, 9_000));

        property.UpdateRatePlan(plan.Id, "2083 revised", Shrawan1, Rates((RoomType.Double, 9_200), (RoomType.Dorm, 6_000))).IsSuccess.Should().BeTrue();

        plan.Name.Should().Be("2083 revised");
        plan.EffectiveFromBs.Should().Be(Shrawan1);
        plan.Rates.Should().HaveCount(2);
        property.MonthlyRate(doubleRoom.Bed(1).Id, Shrawan1.ToDateOnly()).Value.Should().Be(Money.FromRupees(9_200));
    }

    private RatePlan AddPlan(string name, NepaliDate from, params (RoomType Type, decimal Rupees)[] rates)
    {
        var plan = property.AddRatePlan(name, from, Rates(rates));
        plan.IsSuccess.Should().BeTrue(plan.Error?.Message);
        return plan.Value;
    }

    private static Dictionary<RoomType, Money> Rates(params (RoomType Type, decimal Rupees)[] rates) =>
        rates.ToDictionary(r => r.Type, r => Money.FromRupees(r.Rupees));
}
