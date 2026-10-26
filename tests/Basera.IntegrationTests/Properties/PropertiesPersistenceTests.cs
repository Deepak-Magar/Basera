using Basera.IntegrationTests.Infrastructure;
using Basera.Modules.Properties.Domain;
using Basera.Modules.Properties.Infrastructure;
using Basera.Shared.Domain;
using Basera.Shared.Domain.Calendar;
using Basera.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Basera.IntegrationTests.Properties;

/// <summary>
/// The Properties schema against PostgreSQL: the aggregate round trip, <c>xmin</c> concurrency, soft delete of
/// rooms and beds, and the database's own "one open occupancy per bed" index.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class PropertiesPersistenceTests(TestDatabase database) : IAsyncLifetime
{
    private static readonly DateOnly Today = new(2026, 10, 26);

    private readonly Guid tenant = GuidV7.New();
    private BaseraWebFactory factory = null!;

    public Task InitializeAsync()
    {
        factory = new BaseraWebFactory(database.ConnectionString);
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => factory.DisposeAsync().AsTask();

    [Fact]
    public async Task A_property_with_floors_rooms_beds_and_a_plan_round_trips()
    {
        var id = await SaveNewPropertyAsync(property =>
        {
            property.AddRoom(1, "101", RoomType.Double, null, RoomAttributes.AttachedBath | RoomAttributes.Window, "Corner");
            property.AddRoom(2, "D1", RoomType.Dorm, 6, RoomAttributes.None, null);
            property.AddRatePlan("2083", new NepaliDate(2083, 1, 1), new Dictionary<RoomType, Money>
            {
                [RoomType.Double] = Money.FromRupees(9_000),
                [RoomType.Dorm] = Money.FromRupees(6_000),
            });
        });

        var loaded = await LoadAsync(id);

        loaded.ContactPhone!.E164.Should().Be("+9779800000000");
        loaded.Floors.Select(f => f.Number).Should().Equal(1, 2);
        loaded.Rooms.Should().HaveCount(2);
        var room = loaded.Rooms.Single(r => r.Number == "101");
        room.Attributes.Should().Be(RoomAttributes.AttachedBath | RoomAttributes.Window);
        room.Beds.Select(b => b.Code).Should().Equal("B1-101-1", "B1-101-2");
        loaded.Beds.Should().HaveCount(8).And.OnlyContain(b => b.TenantId == tenant && b.PropertyId == id);
        loaded.RatePlans.Single().Rates[RoomType.Dorm].Should().Be(Money.FromRupees(6_000));
        loaded.MonthlyRate(room.Beds[0].Id, Today).Value.Should().Be(Money.FromRupees(9_000));
        room.Version.Should().NotBe(0u, "xmin is read back");
    }

    [Fact]
    public async Task Two_people_changing_one_bed_at_once_cannot_both_win()
    {
        var id = await SaveNewPropertyAsync(property => property.AddRoom(1, "101", RoomType.Single, null, RoomAttributes.None, null));
        await using var first = Scope();
        await using var second = Scope();
        var mine = await first.Db.Properties.WithAggregate().SingleAsync(p => p.Id == id);
        var theirs = await second.Db.Properties.WithAggregate().SingleAsync(p => p.Id == id);
        var bedId = mine.Beds.Single().Id;

        mine.Reserve(bedId, Today).IsSuccess.Should().BeTrue();
        await first.Db.SaveChangesAsync();
        theirs.MarkMaintenance(bedId, Today).IsSuccess.Should().BeTrue("their copy is stale and still says free");

        await second.Db.Invoking(db => db.SaveChangesAsync()).Should().ThrowAsync<DbUpdateConcurrencyException>();
        (await LoadAsync(id)).Beds.Single().Status.Should().Be(BedStatus.Reserved);
    }

    [Fact]
    public async Task A_stale_edit_of_a_property_is_refused()
    {
        var id = await SaveNewPropertyAsync(_ => { });
        await using var first = Scope();
        await using var second = Scope();
        var mine = await first.Db.Properties.SingleAsync(p => p.Id == id);
        var theirs = await second.Db.Properties.SingleAsync(p => p.Id == id);

        mine.SetReservationDays(3);
        await first.Db.SaveChangesAsync();
        theirs.Deactivate();

        await second.Db.Invoking(db => db.SaveChangesAsync()).Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task A_removed_room_and_its_beds_are_soft_deleted_and_their_history_stays()
    {
        Guid roomId = default, bedId = default;
        var stay = GuidV7.New();
        var id = await SaveNewPropertyAsync(property =>
        {
            var room = property.AddRoom(1, "101", RoomType.Double, null, RoomAttributes.None, null).Value;
            roomId = room.Id;
            bedId = room.Beds[0].Id;
            property.Occupy(bedId, stay, Today.AddDays(-30), Today);
            property.Vacate(bedId, stay, Today);
        });

        await using (var scope = Scope())
        {
            var property = await scope.Db.Properties.WithAggregate().SingleAsync(p => p.Id == id);
            property.RemoveRoom(roomId, Today).IsSuccess.Should().BeTrue();
            await scope.Db.SaveChangesAsync();
        }

        await using var check = Scope();
        (await LoadAsync(id)).Rooms.Should().BeEmpty();
        (await check.Db.Beds.CountAsync(b => b.RoomId == roomId)).Should().Be(0);
        var hidden = await check.Db.Beds.IgnoreQueryFilters().Where(b => b.RoomId == roomId).ToListAsync();
        hidden.Should().HaveCount(2).And.OnlyContain(b => b.DeletedAt != null);
        (await check.Db.Rooms.IgnoreQueryFilters().SingleAsync(r => r.Id == roomId)).DeletedAt.Should().NotBeNull();
        (await check.Db.Occupancies.SingleAsync(o => o.BedId == bedId)).To.Should().Be(Today);
    }

    [Fact]
    public async Task A_removed_room_number_can_be_used_again()
    {
        Guid roomId = default;
        var id = await SaveNewPropertyAsync(property => roomId = property.AddRoom(1, "101", RoomType.Single, null, RoomAttributes.None, null).Value.Id);
        await ChangeAsync(id, property => property.RemoveRoom(roomId, Today));

        await ChangeAsync(id, property => property.AddRoom(1, "101", RoomType.Single, null, RoomAttributes.None, null));

        (await LoadAsync(id)).Beds.Single().Code.Should().Be("B1-101-1");
    }

    [Fact]
    public async Task A_smaller_room_soft_deletes_its_last_beds()
    {
        Guid roomId = default;
        var id = await SaveNewPropertyAsync(property => roomId = property.AddRoom(1, "101", RoomType.Four, null, RoomAttributes.None, null).Value.Id);

        await ChangeAsync(id, property => property.UpdateRoom(roomId, "101", RoomType.Double, null, RoomAttributes.None, null, Today));

        (await LoadAsync(id)).Beds.Select(b => b.Slot).Should().Equal(1, 2);
        await using var check = Scope();
        (await check.Db.Beds.IgnoreQueryFilters().CountAsync(b => b.RoomId == roomId && b.DeletedAt != null)).Should().Be(2);
    }

    [Fact]
    public async Task The_database_allows_one_open_occupancy_per_bed()
    {
        var id = await SaveNewPropertyAsync(property => property.AddRoom(1, "101", RoomType.Single, null, RoomAttributes.None, null));
        var bedId = (await LoadAsync(id)).Beds.Single().Id;

        var insert = () => Sql(
            "INSERT INTO properties.bed_occupancy (id, tenant_id, bed_id, stay_id, from_date, created_at) " +
            $"VALUES ('{GuidV7.New()}', '{tenant}', '{bedId}', '{GuidV7.New()}', DATE '2026-10-26', now())");

        await insert();
        var second = await insert.Should().ThrowAsync<PostgresException>();
        second.Which.ConstraintName.Should().Be("ix_bed_occupancy_one_open_per_bed");
    }

    [Fact]
    public async Task Another_tenant_sees_none_of_it()
    {
        var id = await SaveNewPropertyAsync(property => property.AddRoom(1, "101", RoomType.Single, null, RoomAttributes.None, null));

        await using var other = Scope(GuidV7.New());

        (await other.Db.Properties.AnyAsync(p => p.Id == id)).Should().BeFalse();
        (await other.Db.Beds.AnyAsync(b => b.PropertyId == id)).Should().BeFalse();
        (await other.Db.Floors.AnyAsync(f => f.PropertyId == id)).Should().BeFalse();
    }

    private async Task<Guid> SaveNewPropertyAsync(Action<Property> build)
    {
        var property = Property.Create(new PropertyDetails("Sunrise Boys Hostel", "Kirtipur-3", 3, "9800000000", null)).Value;
        property.AddFloor(1, null);
        property.AddFloor(2, null);
        build(property);
        await using var scope = Scope();
        scope.Db.Properties.Add(property);
        await scope.Db.SaveChangesAsync();
        return property.Id;
    }

    private async Task ChangeAsync(Guid id, Func<Property, Basera.Shared.Results.Result> change)
    {
        await using var scope = Scope();
        var property = await scope.Db.Properties.WithAggregate().SingleAsync(p => p.Id == id);
        var result = change(property);
        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        await scope.Db.SaveChangesAsync();
    }

    private async Task<Property> LoadAsync(Guid id)
    {
        await using var scope = Scope();
        return await scope.Db.Properties.AsNoTracking().WithAggregate().SingleAsync(p => p.Id == id);
    }

    private async Task Sql(string sql)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private TenantScope Scope(Guid? tenantId = null)
    {
        var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenantId ?? tenant);
        return new TenantScope(scope, scope.ServiceProvider.GetRequiredService<PropertiesDbContext>());
    }

    private sealed record TenantScope(AsyncServiceScope Scope, PropertiesDbContext Db) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Scope.DisposeAsync();
    }
}
