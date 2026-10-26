using Basera.IntegrationTests.Identity;
using Basera.IntegrationTests.Infrastructure;
using Basera.Modules.Properties.Application;
using Basera.Modules.Properties.Contracts;
using Basera.Modules.Properties.Domain;
using Basera.Shared.Domain;
using Basera.Shared.Domain.Calendar;
using Basera.Shared.Results;
using Basera.Shared.Tenancy;
using Basera.Shared.Time;
using Microsoft.Extensions.DependencyInjection;

namespace Basera.IntegrationTests.Properties;

/// <summary>
/// The Properties services and contracts against PostgreSQL, as other modules and the screens will call them:
/// rooms and rates, the bed lifecycle with occupancy history, races, and tenant isolation.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class PropertyServicesTests(TestDatabase database) : IAsyncLifetime
{
    // 2026-10-26 09:00 in Nepal.
    private readonly TestClock clock = new(new DateTimeOffset(2026, 10, 26, 3, 15, 0, TimeSpan.Zero));
    private readonly Guid tenant = GuidV7.New();
    private BaseraWebFactory factory = null!;

    private DateOnly Today => clock.Today;

    public Task InitializeAsync()
    {
        factory = new BaseraWebFactory(database.ConnectionString, services => services.AddSingleton<IClock>(clock));
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => factory.DisposeAsync().AsTask();

    [Fact]
    public async Task An_owner_sets_up_a_property_and_sees_its_beds_counted()
    {
        var id = await CreatePropertyAsync("Sunrise Boys Hostel");
        await AddRoomAsync(id, 1, "101", RoomType.Double);
        await AddRoomAsync(id, 1, "102", RoomType.Triple);
        var bed = await FirstBedAsync(id, "101");
        await As(tenant, (BedService beds) => beds.OccupyAsync(bed, GuidV7.New(), Today));

        var list = await As(tenant, (PropertyService properties) => properties.ListAsync());

        list.Should().ContainSingle().Which.Should().Be(new PropertyListItem(id, "Sunrise Boys Hostel", "Kirtipur-3", true, 5, 4, 1));
    }

    [Fact]
    public async Task Invalid_input_comes_back_as_a_result_naming_the_field()
    {
        var result = await As(tenant, (PropertyService properties) =>
            properties.CreateAsync(new PropertySettings(new PropertyDetails("", "Kirtipur", null, null, null), 7, true)));
        var id = await CreatePropertyAsync("Sunrise");
        var duplicate = await As(tenant, (RoomService rooms) => AddRoom(rooms, id, 1, "101", RoomType.Single));
        var again = await As(tenant, (RoomService rooms) => AddRoom(rooms, id, 1, "101", RoomType.Single));

        result.Error!.Target.Should().Be("name");
        duplicate.IsSuccess.Should().BeTrue();
        again.Error!.Code.Should().Be(ErrorCode.Conflict);
    }

    [Fact]
    public async Task Rooms_grow_shrink_move_and_go()
    {
        var id = await CreatePropertyAsync("Sunrise");
        var room = await AddRoomAsync(id, 1, "101", RoomType.Double);

        (await As(tenant, (RoomService rooms) => rooms.UpdateRoomAsync(id, room,
            new RoomDetails(2, "201", RoomType.Four, null, RoomAttributes.Balcony, "Big")))).IsSuccess.Should().BeTrue();
        var moved = (await LoadAsync(id)).FindRoom(room)!;
        moved.Beds.Select(b => b.Code).Should().Equal("B2-201-1", "B2-201-2", "B2-201-3", "B2-201-4");
        moved.Has(RoomAttributes.Balcony).Should().BeTrue();

        var firstFloor = (await LoadAsync(id)).Floors.Single(f => f.Number == 1).Id;
        (await As(tenant, (RoomService rooms) => rooms.RemoveFloorAsync(id, firstFloor))).IsSuccess.Should().BeTrue("room 201 moved off it");
        (await As(tenant, (RoomService rooms) => rooms.RemoveRoomAsync(id, room))).IsSuccess.Should().BeTrue();
        (await LoadAsync(id)).Rooms.Should().BeEmpty();
    }

    [Fact]
    public async Task The_rate_resolver_uses_the_bed_override_else_the_plan_in_effect()
    {
        var id = await CreatePropertyAsync("Sunrise");
        await AddRoomAsync(id, 1, "101", RoomType.Double);
        var bed = await FirstBedAsync(id, "101");
        (await As(tenant, (IRateResolver rates) => rates.MonthlyRateAsync(bed, Today))).Error!.Code.Should().Be(ErrorCode.RuleViolation);

        var plan = await As(tenant, (RatePlanService plans) => plans.AddAsync(id, new RatePlanDetails("2083",
            new NepaliDate(2083, 1, 1), new Dictionary<RoomType, Money> { [RoomType.Double] = Money.FromRupees(9_000) })));
        plan.IsSuccess.Should().BeTrue(plan.Error?.Message);
        (await As(tenant, (IRateResolver rates) => rates.MonthlyRateAsync(bed, Today))).Value.Should().Be(Money.FromRupees(9_000));

        await As(tenant, (BedService beds) => beds.SetRateAsync(bed, Money.FromRupees(9_800)));
        (await As(tenant, (IRateResolver rates) => rates.MonthlyRateAsync(bed, Today))).Value.Should().Be(Money.FromRupees(9_800));
        (await As(tenant, (IBedAvailability beds) => beds.FreeBedsAsync(id))).Select(b => b.MonthlyRate)
            .Should().Equal(Money.FromRupees(9_800), Money.FromRupees(9_000));
    }

    [Fact]
    public async Task A_stay_reserves_moves_in_and_out_and_the_bed_keeps_its_history()
    {
        var id = await CreatePropertyAsync("Sunrise");
        await AddRoomAsync(id, 1, "101", RoomType.Single);
        await AddRoomAsync(id, 1, "D1", RoomType.Dorm, dormBeds: 5);
        var free = await As(tenant, (IBedAvailability beds) => beds.FreeBedsAsync(id, RoomType.Single));
        var bed = free.Should().ContainSingle().Which.BedId;
        var stay = GuidV7.New();

        (await As(tenant, (IBedAvailability beds) => beds.ReserveAsync(bed, stay))).IsSuccess.Should().BeTrue();
        (await As(tenant, (IBedAvailability beds) => beds.FreeBedsAsync(id, RoomType.Single))).Should().BeEmpty();
        (await As(tenant, (IBedAvailability beds) => beds.OccupyAsync(bed, GuidV7.New(), Today))).Error!.Code.Should().Be(ErrorCode.RuleViolation);
        (await As(tenant, (IBedAvailability beds) => beds.OccupyAsync(bed, stay, Today))).IsSuccess.Should().BeTrue();

        clock.Advance(TimeSpan.FromDays(40));
        (await As(tenant, (IBedAvailability beds) => beds.VacateAsync(bed, stay, Today))).IsSuccess.Should().BeTrue();
        var next = GuidV7.New();
        (await As(tenant, (IBedAvailability beds) => beds.OccupyAsync(bed, next, Today))).IsSuccess.Should().BeTrue();

        var details = (await As(tenant, (BedService beds) => beds.GetAsync(bed)))!;
        details.Status.Should().Be(BedStatus.Occupied);
        details.History.Should().Equal(
            new BedHistoryEntry(next, Today, null),
            new BedHistoryEntry(stay, Today.AddDays(-40), Today));
    }

    [Fact]
    public async Task A_reservation_runs_out_after_the_propertys_reservation_days()
    {
        var id = await CreatePropertyAsync("Sunrise");
        await AddRoomAsync(id, 1, "101", RoomType.Single);
        var bed = await FirstBedAsync(id, "101");
        await As(tenant, (IBedAvailability beds) => beds.ReserveAsync(bed, GuidV7.New()));

        clock.Advance(TimeSpan.FromDays(6));
        (await As(tenant, (IBedAvailability beds) => beds.FreeBedsAsync(id))).Should().BeEmpty();
        clock.Advance(TimeSpan.FromDays(1));
        (await As(tenant, (IBedAvailability beds) => beds.FreeBedsAsync(id))).Should().ContainSingle();
        (await As(tenant, (IBedAvailability beds) => beds.OccupyAsync(bed, GuidV7.New(), Today))).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Two_check_ins_racing_for_one_bed_give_one_conflict()
    {
        var id = await CreatePropertyAsync("Sunrise");
        await AddRoomAsync(id, 1, "101", RoomType.Single);
        var bed = await FirstBedAsync(id, "101");

        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
            As(tenant, (IBedAvailability beds) => beds.OccupyAsync(bed, GuidV7.New(), Today))));

        results.Count(r => r.IsSuccess).Should().Be(1);
        results.Single(r => r.IsFailure).Error!.Code.Should().BeOneOf(ErrorCode.Conflict, ErrorCode.RuleViolation);
        (await As(tenant, (BedService beds) => beds.GetAsync(bed)))!.History.Should().ContainSingle();
    }

    [Fact]
    public async Task A_property_with_a_resident_cannot_be_removed_until_they_leave()
    {
        var id = await CreatePropertyAsync("Sunrise");
        await AddRoomAsync(id, 1, "101", RoomType.Single);
        var bed = await FirstBedAsync(id, "101");
        var stay = GuidV7.New();
        await As(tenant, (IBedAvailability beds) => beds.OccupyAsync(bed, stay, Today));

        (await As(tenant, (PropertyService properties) => properties.RemoveAsync(id))).Error!.Code.Should().Be(ErrorCode.RuleViolation);
        await As(tenant, (IBedAvailability beds) => beds.VacateAsync(bed, stay, Today));
        (await As(tenant, (PropertyService properties) => properties.RemoveAsync(id))).IsSuccess.Should().BeTrue();

        (await As(tenant, (IPropertyDirectory directory) => directory.FindAsync(id))).Should().BeNull();
        (await As(tenant, (BedService beds) => beds.GetAsync(bed))).Should().BeNull("its beds went with it");
    }

    [Fact]
    public async Task An_inactive_property_offers_no_free_beds()
    {
        var id = await CreatePropertyAsync("Sunrise");
        await AddRoomAsync(id, 1, "101", RoomType.Single);

        await As(tenant, (PropertyService properties) => properties.UpdateAsync(id,
            new PropertySettings(new PropertyDetails("Sunrise", "Kirtipur-3", null, null, null), 7, IsActive: false)));

        (await As(tenant, (IBedAvailability beds) => beds.FreeBedsAsync(id))).Should().BeEmpty();
        (await As(tenant, (IPropertyDirectory directory) => directory.FindAsync(id)))!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Another_tenant_can_neither_see_nor_change_any_of_it()
    {
        var id = await CreatePropertyAsync("Sunrise");
        await AddRoomAsync(id, 1, "101", RoomType.Single);
        var bed = await FirstBedAsync(id, "101");
        var other = GuidV7.New();

        (await As(other, (IPropertyDirectory directory) => directory.ListAsync())).Should().BeEmpty();
        (await As(other, (IPropertyDirectory directory) => directory.FindAsync(id))).Should().BeNull();
        (await As(other, (PropertyService properties) => properties.ListAsync())).Should().BeEmpty();
        (await As(other, (IBedAvailability beds) => beds.FreeBedsAsync(id))).Should().BeEmpty();
        (await As(other, (IBedAvailability beds) => beds.ReserveAsync(bed, null))).Error!.Code.Should().Be(ErrorCode.NotFound);
        (await As(other, (IBedAvailability beds) => beds.OccupyAsync(bed, GuidV7.New(), Today))).Error!.Code.Should().Be(ErrorCode.NotFound);
        (await As(other, (IRateResolver rates) => rates.MonthlyRateAsync(bed, Today))).Error!.Code.Should().Be(ErrorCode.NotFound);
        (await As(other, (RoomService rooms) => AddRoom(rooms, id, 1, "999", RoomType.Single))).Error!.Code.Should().Be(ErrorCode.NotFound);
        (await As(other, (PropertyService properties) => properties.RemoveAsync(id))).Error!.Code.Should().Be(ErrorCode.NotFound);
        (await As(other, (BedService beds) => beds.GetAsync(bed))).Should().BeNull();

        (await As(tenant, (BedService beds) => beds.GetAsync(bed)))!.Status.Should().Be(BedStatus.Free, "nothing changed for the owner");
    }

    private async Task<Guid> CreatePropertyAsync(string name)
    {
        var created = await As(tenant, (PropertyService properties) =>
            properties.CreateAsync(new PropertySettings(new PropertyDetails(name, "Kirtipur-3", 3, "9800000000", null), 7, true)));
        created.IsSuccess.Should().BeTrue(created.Error?.Message);
        await As(tenant, (RoomService rooms) => rooms.AddFloorAsync(created.Value, 1, null));
        await As(tenant, (RoomService rooms) => rooms.AddFloorAsync(created.Value, 2, null));
        return created.Value;
    }

    private async Task<Guid> AddRoomAsync(Guid propertyId, int floor, string number, RoomType type, int? dormBeds = null)
    {
        var room = await As(tenant, (RoomService rooms) => AddRoom(rooms, propertyId, floor, number, type, dormBeds));
        room.IsSuccess.Should().BeTrue(room.Error?.Message);
        return room.Value;
    }

    private static Task<Result<Guid>> AddRoom(RoomService rooms, Guid propertyId, int floor, string number, RoomType type, int? dormBeds = null) =>
        rooms.AddRoomAsync(propertyId, new RoomDetails(floor, number, type, dormBeds, RoomAttributes.None, null));

    private async Task<Guid> FirstBedAsync(Guid propertyId, string room) =>
        (await LoadAsync(propertyId)).Rooms.Single(r => r.Number == room).Beds[0].Id;

    private async Task<Property> LoadAsync(Guid propertyId) =>
        (await As(tenant, (PropertyService properties) => properties.FindAsync(propertyId)))!;

    /// <summary>Runs <paramref name="call"/> in a fresh DI scope acting for <paramref name="tenantId"/>, like one request.</summary>
    private async Task<TResult> As<TService, TResult>(Guid tenantId, Func<TService, Task<TResult>> call)
        where TService : notnull
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenantId);
        return await call(scope.ServiceProvider.GetRequiredService<TService>());
    }
}
