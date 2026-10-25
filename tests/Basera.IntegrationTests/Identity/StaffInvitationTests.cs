using System.Net;
using System.Text.RegularExpressions;
using Basera.IntegrationTests.Infrastructure;
using Basera.Modules.Identity.Domain;
using Basera.Modules.Identity.Infrastructure;
using Basera.Shared.Messaging;
using Basera.Shared.Time;
using Basera.Shared.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Basera.IntegrationTests.Identity;

/// <summary>Owner invites staff by SMS link; the owner's staff pages; resource authorization across tenants.</summary>
[Collection(DatabaseCollection.Name)]
public sealed partial class StaffInvitationTests(TestDatabase database) : IAsyncLifetime
{
    private readonly TestClock clock = new(DateTimeOffset.UtcNow);
    private BaseraWebFactory factory = null!;

    public Task InitializeAsync()
    {
        factory = new BaseraWebFactory(
            database.ConnectionString,
            services => services.AddSingleton<IClock>(clock),
            settings: new Dictionary<string, string> { ["RateLimits:Auth:PermitLimit"] = "10000" });
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => factory.DisposeAsync().AsTask();

    [Fact]
    public async Task An_invited_warden_sets_a_password_joins_the_staff_and_can_sign_in_later()
    {
        var (owner, ownerClient) = await RegisterOwnerAsync();
        var phone = RandomPhone();

        var invited = await InviteAsync(ownerClient, phone, Role.Warden);
        invited.Headers.Location!.OriginalString.Should().Be("/owner/staff");
        var token = LatestToken(phone);
        (await ownerClient.Http.GetStringAsync("/owner/staff")).Should().Contain("Pending invitations").And.Contain(phone[3..]);

        var warden = new AccountTestClient(factory);
        (await warden.Http.GetStringAsync($"/account/invite/{token}")).Should().Contain($"{owner.HostelName} invited you as Warden.");
        var accepted = await AcceptAsync(warden, token, "Hari Gurung");

        accepted.Headers.Location!.OriginalString.Should().Be("/staff", "a warden lands in the staff area");
        var principal = warden.Principal()!;
        principal.FindFirst(BaseraClaimTypes.Role)!.Value.Should().Be("Warden");
        principal.FindFirst(BaseraClaimTypes.Name)!.Value.Should().Be("Hari Gurung");
        (await new AccountTestClient(factory).LoginAsync(phone, "Wardn-pass1")).StatusCode.Should().Be(HttpStatusCode.Redirect);
        var list = await ownerClient.Http.GetStringAsync("/owner/staff");
        list.Should().Contain("Hari Gurung").And.Contain("No pending invitations.");

        var user = await UserAsync(phone);
        user.PhoneVerifiedAt.Should().NotBeNull("the link reached the phone by SMS");
    }

    [Fact]
    public async Task Only_a_hash_of_the_link_secret_is_stored()
    {
        var (_, ownerClient) = await RegisterOwnerAsync();
        var phone = RandomPhone();
        await InviteAsync(ownerClient, phone, Role.Cook);
        var token = LatestToken(phone);

        var invitation = await InvitationAsync(phone);

        token.Should().HaveLength(75);
        invitation.TokenHash.Should().HaveLength(64).And.NotContain(token[32..]);
        token[..32].Should().Be(invitation.TenantId.ToString("N"));
        invitation.Role.Should().Be(Role.Cook);
    }

    [Fact]
    public async Task A_link_works_once()
    {
        var (_, ownerClient) = await RegisterOwnerAsync();
        var phone = RandomPhone();
        await InviteAsync(ownerClient, phone, Role.Warden);
        var token = LatestToken(phone);
        await AcceptAsync(new AccountTestClient(factory), token, "Hari Gurung");

        var again = await new AccountTestClient(factory).Http.GetStringAsync($"/account/invite/{token}");

        again.Should().Contain("This invitation can&#x27;t be used");
    }

    [Fact]
    public async Task A_link_expires_after_seven_days()
    {
        var (_, ownerClient) = await RegisterOwnerAsync();
        var phone = RandomPhone();
        await InviteAsync(ownerClient, phone, Role.Warden);
        var token = LatestToken(phone);

        clock.Advance(TimeSpan.FromDays(7));

        (await new AccountTestClient(factory).Http.GetStringAsync($"/account/invite/{token}")).Should().Contain("can&#x27;t be used");
        (await AcceptAsync(new AccountTestClient(factory), token, "Hari")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await UserExistsAsync(phone)).Should().BeFalse();
    }

    [Fact]
    public async Task A_withdrawn_or_replaced_invitation_stops_working()
    {
        var (_, ownerClient) = await RegisterOwnerAsync();
        var phone = RandomPhone();
        await InviteAsync(ownerClient, phone, Role.Warden);
        var first = LatestToken(phone);
        await InviteAsync(ownerClient, phone, Role.Cook);
        var second = LatestToken(phone);

        (await new AccountTestClient(factory).Http.GetStringAsync($"/account/invite/{first}")).Should().Contain("can&#x27;t be used");

        var pending = await InvitationAsync(phone, pendingOnly: true);
        await ownerClient.SubmitAsync("/owner/staff", new Dictionary<string, string>(), $"/owner/staff/invitations/{pending.Id}/revoke");
        (await new AccountTestClient(factory).Http.GetStringAsync($"/account/invite/{second}")).Should().Contain("can&#x27;t be used");
    }

    [Fact]
    public async Task A_tampered_link_finds_nothing()
    {
        var (_, ownerClient) = await RegisterOwnerAsync();
        var (_, otherOwner) = await RegisterOwnerAsync();
        var phone = RandomPhone();
        await InviteAsync(ownerClient, phone, Role.Warden);
        var token = LatestToken(phone);
        var otherTenant = otherOwner.Principal()!.FindFirst(BaseraClaimTypes.TenantId)!.Value;

        foreach (var tampered in new[] { Guid.Parse(otherTenant).ToString("N") + token[32..], token[..32] + new string('A', 43), token[..^1], "x" })
        {
            (await new AccountTestClient(factory).Http.GetStringAsync($"/account/invite/{tampered}")).Should().Contain("can&#x27;t be used");
        }
    }

    [Fact]
    public async Task A_number_registered_with_another_hostel_cannot_be_invited()
    {
        var (_, ownerClient) = await RegisterOwnerAsync();
        var (other, _) = await RegisterOwnerAsync();

        var response = await InviteAsync(ownerClient, other.Phone, Role.Manager);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("This number already has a Basera account with another hostel.");
        factory.Services.GetRequiredService<MockSmsInbox>().Messages.Should().NotContain(m => m.Message.Purpose == "invite" && m.Message.To.NationalNumber == other.Phone);
    }

    [Fact]
    public async Task The_owner_changes_a_role_and_removes_a_member_who_then_cannot_sign_in()
    {
        var (_, ownerClient) = await RegisterOwnerAsync();
        var phone = RandomPhone();
        await InviteAsync(ownerClient, phone, Role.Warden);
        await AcceptAsync(new AccountTestClient(factory), LatestToken(phone), "Hari Gurung");
        var membership = await MembershipAsync(phone);
        var stampBefore = (await UserAsync(phone)).SecurityStamp;

        await ownerClient.SubmitAsync("/owner/staff", new Dictionary<string, string> { ["role"] = "Cook" }, $"/owner/staff/{membership.Id}/role");

        (await MembershipAsync(phone)).Role.Should().Be(Role.Cook);
        (await UserAsync(phone)).SecurityStamp.Should().NotBe(stampBefore, "the old cookie, with the old role, must stop working");

        var removed = await ownerClient.SubmitAsync("/owner/staff", new Dictionary<string, string>(), $"/owner/staff/{membership.Id}/remove");
        removed.Headers.Location!.OriginalString.Should().Be("/owner/staff");
        (await ownerClient.Http.GetStringAsync("/owner/staff")).Should().Contain("Removed from staff.").And.NotContain("Hari Gurung");
        (await (await new AccountTestClient(factory).LoginAsync(phone, "Wardn-pass1")).Content.ReadAsStringAsync())
            .Should().Contain("Your account has no access to this hostel any more.");
    }

    [Fact]
    public async Task A_removed_member_can_be_invited_again_and_keeps_the_same_user()
    {
        var (_, ownerClient) = await RegisterOwnerAsync();
        var phone = RandomPhone();
        await InviteAsync(ownerClient, phone, Role.Warden);
        await AcceptAsync(new AccountTestClient(factory), LatestToken(phone), "Hari Gurung");
        var first = await UserAsync(phone);
        await ownerClient.SubmitAsync("/owner/staff", new Dictionary<string, string>(), $"/owner/staff/{(await MembershipAsync(phone)).Id}/remove");

        await InviteAsync(ownerClient, phone, Role.Manager);
        var client = new AccountTestClient(factory);
        var acceptedAgain = await AcceptAsync(client, LatestToken(phone), "Hari G.", "N3w-manager");
        acceptedAgain.StatusCode.Should().Be(HttpStatusCode.Redirect, await acceptedAgain.Content.ReadAsStringAsync());

        client.Principal()!.FindFirst(BaseraClaimTypes.Subject)!.Value.Should().Be(first.Id.ToString());
        client.Principal()!.FindFirst(BaseraClaimTypes.Role)!.Value.Should().Be("Manager");
        (await new AccountTestClient(factory).LoginAsync(phone, "N3w-manager")).StatusCode.Should().Be(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task The_owner_cannot_change_or_remove_their_own_membership_here()
    {
        var (owner, ownerClient) = await RegisterOwnerAsync();
        var membership = await MembershipAsync(owner.Phone);

        await ownerClient.SubmitAsync("/owner/staff", new Dictionary<string, string> { ["role"] = "Cook" }, $"/owner/staff/{membership.Id}/role");
        var removed = await ownerClient.SubmitAsync("/owner/staff", new Dictionary<string, string>(), $"/owner/staff/{membership.Id}/remove");

        removed.Headers.Location!.OriginalString.Should().Be("/owner/staff");
        (await ownerClient.Http.GetStringAsync("/owner/staff")).Should().Contain("The owner can&#x27;t be removed here.");
        (await MembershipAsync(owner.Phone)).Role.Should().Be(Role.Owner);
    }

    [Fact]
    public async Task Another_hostels_owner_cannot_touch_this_hostels_staff()
    {
        var (_, ownerA) = await RegisterOwnerAsync();
        var (_, ownerB) = await RegisterOwnerAsync();
        var phone = RandomPhone();
        await InviteAsync(ownerA, phone, Role.Warden);
        await AcceptAsync(new AccountTestClient(factory), LatestToken(phone), "Hari Gurung");
        var membership = await MembershipAsync(phone);
        var invitationPhone = RandomPhone();
        await InviteAsync(ownerA, invitationPhone, Role.Cook);
        var invitation = await InvitationAsync(invitationPhone);

        var remove = await ownerB.SubmitAsync("/owner/staff", new Dictionary<string, string>(), $"/owner/staff/{membership.Id}/remove");
        var role = await ownerB.SubmitAsync("/owner/staff", new Dictionary<string, string> { ["role"] = "Cook" }, $"/owner/staff/{membership.Id}/role");
        var revoke = await ownerB.SubmitAsync("/owner/staff", new Dictionary<string, string>(), $"/owner/staff/invitations/{invitation.Id}/revoke");

        remove.StatusCode.Should().Be(HttpStatusCode.NotFound);
        role.StatusCode.Should().Be(HttpStatusCode.NotFound);
        revoke.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await MembershipAsync(phone)).Role.Should().Be(Role.Warden);
        (await InvitationAsync(invitationPhone)).RevokedAt.Should().BeNull();
        (await ownerB.Http.GetStringAsync("/owner/staff")).Should().NotContain("Hari Gurung");
    }

    [Fact]
    public async Task Staff_pages_are_for_owners_only()
    {
        var (_, ownerClient) = await RegisterOwnerAsync();
        var phone = RandomPhone();
        await InviteAsync(ownerClient, phone, Role.Manager);
        var manager = new AccountTestClient(factory);
        await AcceptAsync(manager, LatestToken(phone), "Maya Rai");

        var asManager = await manager.Http.GetAsync("/owner/staff");
        var anonymous = await new AccountTestClient(factory).Http.GetAsync("/owner/staff");

        asManager.StatusCode.Should().Be(HttpStatusCode.Redirect);
        asManager.Headers.Location!.AbsolutePath.Should().Be("/account/denied");
        anonymous.Headers.Location!.PathAndQuery.Should().Be("/account/login?ReturnUrl=%2Fowner%2Fstaff");
    }

    private async Task<(Owner Owner, AccountTestClient Client)> RegisterOwnerAsync()
    {
        var owner = Owner.Random();
        var client = new AccountTestClient(factory);
        (await client.RegisterAsync(owner)).StatusCode.Should().Be(HttpStatusCode.Redirect);
        return (owner, client);
    }

    private static Task<HttpResponseMessage> InviteAsync(AccountTestClient owner, string phone, Role role) =>
        owner.SubmitAsync("/owner/staff/invite", new Dictionary<string, string> { ["Phone"] = phone, ["Role"] = role.ToString() });

    private static Task<HttpResponseMessage> AcceptAsync(AccountTestClient client, string token, string name, string password = "Wardn-pass1") =>
        client.SubmitAsync($"/account/invite/{token}", new Dictionary<string, string> { ["DisplayName"] = name, ["Password"] = password });

    private string LatestToken(string phone)
    {
        var sms = factory.Services.GetRequiredService<MockSmsInbox>().Messages
            .First(m => m.Message.Purpose == "invite" && m.Message.To.NationalNumber == phone);
        return InviteLink().Match(sms.Message.Text).Groups[1].Value;
    }

    private async Task<T> QueryAsync<T>(Func<IdentityDbContext, Task<T>> query)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<IdentityDbContext>());
    }

    private Task<ApplicationUser> UserAsync(string phone) =>
        QueryAsync(db => db.Users.IgnoreQueryFilters().AsNoTracking().SingleAsync(u => u.PhoneNumber == "+977" + phone));

    private Task<bool> UserExistsAsync(string phone) =>
        QueryAsync(db => db.Users.IgnoreQueryFilters().AnyAsync(u => u.PhoneNumber == "+977" + phone));

    private Task<TenantMembership> MembershipAsync(string phone) => QueryAsync(async db =>
    {
        var user = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.PhoneNumber == "+977" + phone);
        return await db.Memberships.IgnoreQueryFilters().AsNoTracking().SingleAsync(m => m.UserId == user.Id);
    });

    private Task<StaffInvitation> InvitationAsync(string phone, bool pendingOnly = false) => QueryAsync(db => db.Invitations
        .IgnoreQueryFilters().AsNoTracking()
        .Where(i => i.Phone == "+977" + phone && (!pendingOnly || i.RevokedAt == null))
        .OrderByDescending(i => i.CreatedAt)
        .FirstAsync());

    private static string RandomPhone() => $"98{Random.Shared.Next(0, 100_000_000):D8}";

    [GeneratedRegex(@"/account/invite/([A-Za-z0-9_-]+)")]
    private static partial Regex InviteLink();
}
