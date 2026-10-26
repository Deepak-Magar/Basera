using System.Net;
using System.Text.RegularExpressions;
using Basera.IntegrationTests.Identity;
using Basera.IntegrationTests.Infrastructure;
using Basera.Modules.Identity.Domain;
using Basera.Modules.Identity.Infrastructure;
using Basera.Modules.Properties.Application;
using Basera.Modules.Properties.Contracts;
using Basera.Modules.Properties.Domain;
using Basera.Modules.Properties.Infrastructure;
using Basera.Shared.Messaging;
using Basera.Shared.Tenancy;
using Basera.Shared.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Basera.IntegrationTests.Properties;

/// <summary>
/// PRODUCT_SPEC §6 rule 8: a Manager sees and changes only the properties the owner assigned on the staff page;
/// the Owner sees all. The picker lives on Identity's staff page and is answered by Properties.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed partial class ManagerScopingTests(TestDatabase database) : IAsyncLifetime
{
    private BaseraWebFactory factory = null!;

    public Task InitializeAsync()
    {
        factory = new BaseraWebFactory(
            database.ConnectionString,
            settings: new Dictionary<string, string> { ["RateLimits:Auth:PermitLimit"] = "10000" });
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => factory.DisposeAsync().AsTask();

    [Fact]
    public async Task A_manager_sees_only_assigned_properties_and_the_owner_sees_all()
    {
        var hostel = await HostelAsync();
        await AssignAsync(hostel, hostel.North);

        var managerList = await hostel.Manager.Http.GetStringAsync("/owner/properties");
        var ownerList = await hostel.Owner.Http.GetStringAsync("/owner/properties");

        managerList.Should().Contain("North Block").And.NotContain("South Block");
        ownerList.Should().Contain("North Block").And.Contain("South Block");
    }

    [Fact]
    public async Task A_manager_cannot_open_another_propertys_page_or_beds()
    {
        var hostel = await HostelAsync();
        await AssignAsync(hostel, hostel.North);

        (await hostel.Manager.Http.GetAsync($"/owner/properties/{hostel.North}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await hostel.Manager.Http.GetAsync($"/owner/beds/{hostel.NorthBed}")).StatusCode.Should().Be(HttpStatusCode.OK);

        Denied(await hostel.Manager.Http.GetAsync($"/owner/properties/{hostel.South}"));
        Denied(await hostel.Manager.Http.GetAsync($"/owner/beds/{hostel.SouthBed}"));
        (await hostel.Owner.Http.GetAsync($"/owner/beds/{hostel.SouthBed}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_manager_with_no_assignment_sees_nothing()
    {
        var hostel = await HostelAsync();

        (await hostel.Manager.Http.GetStringAsync("/owner/properties")).Should().NotContain("North Block").And.NotContain("South Block");
        Denied(await hostel.Manager.Http.GetAsync($"/owner/properties/{hostel.North}"));
    }

    [Fact]
    public async Task Another_hostel_cannot_reach_these_properties_at_all()
    {
        var hostel = await HostelAsync();
        var other = await HostelAsync();

        (await other.Owner.Http.GetAsync($"/owner/properties/{hostel.North}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await other.Owner.Http.GetAsync($"/owner/beds/{hostel.NorthBed}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await other.Owner.Http.GetStringAsync("/owner/properties")).Should().NotContain(hostel.North.ToString());
    }

    [Fact]
    public async Task The_staff_page_shows_and_saves_a_managers_properties()
    {
        var hostel = await HostelAsync();
        var page = $"/owner/staff/{hostel.ManagerMembership}/properties";

        (await hostel.Owner.Http.GetStringAsync("/owner/staff")).Should().Contain(page);
        var picker = await hostel.Owner.Http.GetStringAsync(page);
        picker.Should().Contain("North Block").And.Contain("South Block").And.NotContain("checked=\"checked\"");

        var saved = await AssignAsync(hostel, hostel.North, hostel.South);
        saved.Headers.Location!.OriginalString.Should().Be("/owner/staff");
        (await hostel.Owner.Http.GetStringAsync("/owner/staff")).Should().Contain("Properties saved.");
        CheckedBoxes().Matches(await hostel.Owner.Http.GetStringAsync(page)).Should().HaveCount(2);

        await AssignAsync(hostel);
        (await AssignmentCountAsync(hostel)).Should().Be(0, "an empty submit clears them");
    }

    [Fact]
    public async Task Only_managers_get_a_property_picker()
    {
        var hostel = await HostelAsync();
        var ownerMembership = await MembershipIdAsync(hostel.OwnerDetails.Phone);

        var response = await hostel.Owner.Http.GetAsync($"/owner/staff/{ownerMembership}/properties");

        response.Headers.Location!.OriginalString.Should().Be("/owner/staff");
        (await hostel.Owner.Http.GetStringAsync("/owner/staff")).Should().Contain("Only managers are assigned to properties.");
    }

    [Fact]
    public async Task Another_hostels_property_cannot_be_assigned()
    {
        var hostel = await HostelAsync();
        var other = await HostelAsync();

        await AssignAsync(hostel, other.North);

        (await hostel.Owner.Http.GetStringAsync("/owner/staff")).Should().Contain("That property doesn&#x27;t exist.");
        (await AssignmentCountAsync(hostel)).Should().Be(0);
    }

    [Fact]
    public async Task A_manager_who_becomes_a_warden_or_leaves_loses_the_assignments()
    {
        var hostel = await HostelAsync();
        await AssignAsync(hostel, hostel.North);
        (await AssignmentCountAsync(hostel)).Should().Be(1);

        await hostel.Owner.SubmitAsync("/owner/staff", new Dictionary<string, string> { ["role"] = "Warden" }, $"/owner/staff/{hostel.ManagerMembership}/role");
        (await AssignmentCountAsync(hostel)).Should().Be(0);

        await hostel.Owner.SubmitAsync("/owner/staff", new Dictionary<string, string> { ["role"] = "Manager" }, $"/owner/staff/{hostel.ManagerMembership}/role");
        await AssignAsync(hostel, hostel.South);
        await hostel.Owner.SubmitAsync("/owner/staff", new Dictionary<string, string>(), $"/owner/staff/{hostel.ManagerMembership}/remove");
        (await AssignmentCountAsync(hostel)).Should().Be(0);
    }

    private static void Denied(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.AbsolutePath.Should().Be("/account/denied");
    }

    private Task<HttpResponseMessage> AssignAsync(Hostel hostel, params Guid[] propertyIds)
    {
        var page = $"/owner/staff/{hostel.ManagerMembership}/properties";
        var fields = propertyIds.Select(id => new KeyValuePair<string, string>("propertyIds", id.ToString())).ToList();
        return hostel.Owner.SubmitAsync(page, fields);
    }

    /// <summary>An owner with two properties (one room each) and a manager who has accepted an invitation.</summary>
    private async Task<Hostel> HostelAsync()
    {
        var owner = Owner.Random();
        var ownerClient = new AccountTestClient(factory);
        (await ownerClient.RegisterAsync(owner)).StatusCode.Should().Be(HttpStatusCode.Redirect);
        var tenant = Guid.Parse(ownerClient.Principal()!.FindFirst(BaseraClaimTypes.TenantId)!.Value);

        var (north, northBed) = await PropertyAsync(tenant, "North Block");
        var (south, southBed) = await PropertyAsync(tenant, "South Block");

        var phone = $"98{Random.Shared.Next(0, 100_000_000):D8}";
        await ownerClient.SubmitAsync("/owner/staff/invite", new Dictionary<string, string> { ["Phone"] = phone, ["Role"] = nameof(Role.Manager) });
        var sms = factory.Services.GetRequiredService<MockSmsInbox>().Messages.First(m => m.Message.Purpose == "invite" && m.Message.To.NationalNumber == phone);
        var token = InviteLink().Match(sms.Message.Text).Groups[1].Value;
        var manager = new AccountTestClient(factory);
        (await manager.SubmitAsync($"/account/invite/{token}", new Dictionary<string, string> { ["DisplayName"] = "Maya Rai", ["Password"] = "Mngr-pass1" }))
            .StatusCode.Should().Be(HttpStatusCode.Redirect);

        return new Hostel(owner, ownerClient, manager, tenant, await MembershipIdAsync(phone), north, northBed, south, southBed);
    }

    private async Task<(Guid Property, Guid Bed)> PropertyAsync(Guid tenant, string name)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenant);
        var properties = scope.ServiceProvider.GetRequiredService<PropertyService>();
        var rooms = scope.ServiceProvider.GetRequiredService<RoomService>();
        var id = (await properties.CreateAsync(new PropertySettings(new PropertyDetails(name, "Kirtipur", null, null, null), 7, true))).Value;
        await rooms.AddFloorAsync(id, 1, null);
        await rooms.AddRoomAsync(id, new RoomDetails(1, "101", RoomType.Single, null, RoomAttributes.None, null));
        var bed = (await properties.FindAsync(id))!.Beds.Single().Id;
        return (id, bed);
    }

    private async Task<Guid> MembershipIdAsync(string phone)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var user = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.PhoneNumber == "+977" + phone);
        return await db.Memberships.IgnoreQueryFilters().Where(m => m.UserId == user.Id).Select(m => m.Id).SingleAsync();
    }

    private async Task<int> AssignmentCountAsync(Hostel hostel)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(hostel.Tenant);
        return await scope.ServiceProvider.GetRequiredService<PropertiesDbContext>().ManagerAssignments
            .CountAsync(a => a.MembershipId == hostel.ManagerMembership);
    }

    [GeneratedRegex(@"/account/invite/([A-Za-z0-9_-]+)")]
    private static partial Regex InviteLink();

    [GeneratedRegex("type=\"checkbox\" name=\"propertyIds\"[^>]*checked=\"checked\"")]
    private static partial Regex CheckedBoxes();

    private sealed record Hostel(
        Owner OwnerDetails, AccountTestClient Owner, AccountTestClient Manager, Guid Tenant, Guid ManagerMembership,
        Guid North, Guid NorthBed, Guid South, Guid SouthBed);
}
