using System.Net;
using System.Text.RegularExpressions;
using Basera.IntegrationTests.Infrastructure;
using Basera.Modules.Identity.Domain;
using Basera.Modules.Identity.Infrastructure;
using Basera.Shared.Messaging;
using Basera.Shared.Modules;
using Basera.Shared.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Basera.IntegrationTests.Identity;

/// <summary>The role-aware shell: where each role lands, who may open which area, the header, language, profile, seed.</summary>
[Collection(DatabaseCollection.Name)]
public sealed partial class ShellTests(TestDatabase database) : IAsyncLifetime
{
    private readonly BaseraWebFactory factory = new(database.ConnectionString, settings: new Dictionary<string, string>
    {
        ["RateLimits:Auth:PermitLimit"] = "10000",
    });

    public async Task InitializeAsync() => await factory.Services.RunDevSeedersAsync();

    public Task DisposeAsync() => factory.DisposeAsync().AsTask();

    [Fact]
    public async Task Seeding_twice_changes_nothing()
    {
        await factory.Services.RunDevSeedersAsync();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        (await db.Tenants.CountAsync(t => t.Name == "Sunrise Boys Hostel, Kirtipur")).Should().Be(1);
        (await db.Users.IgnoreQueryFilters().CountAsync(u => u.Email == "owner@basera.local")).Should().Be(1);
    }

    [Theory]
    [InlineData("owner@basera.local", "/owner")]
    [InlineData("warden@basera.local", "/staff")]
    [InlineData("9800000002", "/staff")]
    public async Task Seeded_staff_sign_in_with_the_published_password_and_land_in_their_area(string login, string home)
    {
        var response = await new AccountTestClient(factory).LoginAsync(login, "Passw0rd!");

        response.Headers.Location!.OriginalString.Should().Be(home);
    }

    [Fact]
    public async Task The_seeded_resident_signs_in_by_otp_and_lands_in_the_resident_area()
    {
        var client = new AccountTestClient(factory);
        await client.SubmitAsync("/account/otp", new Dictionary<string, string> { ["Phone"] = "9800000001" });
        var sms = factory.Services.GetRequiredService<MockSmsInbox>().Messages.First(m => m.Message.To.NationalNumber == "9800000001");

        var response = await client.SubmitAsync("/account/otp/verify?phone=9800000001",
            new Dictionary<string, string> { ["Phone"] = "9800000001", ["Code"] = SmsCode().Match(sms.Message.Text).Groups[1].Value },
            "/account/otp/verify");

        response.Headers.Location!.OriginalString.Should().Be("/resident");
        var page = await client.Http.GetStringAsync("/resident");
        page.Should().Contain("<html lang=\"ne\">").And.Contain("Ram Thapa").And.Contain("Sunrise Boys Hostel, Kirtipur");
    }

    [Fact]
    public async Task A_signed_in_visit_to_the_landing_page_goes_to_the_role_area()
    {
        var client = await SignInAsync("warden@basera.local");

        var response = await client.Http.GetAsync("/");

        response.Headers.Location!.OriginalString.Should().Be("/staff");
    }

    [Fact]
    public async Task The_return_url_wins_over_the_role_area_when_it_is_ours()
    {
        var response = await new AccountTestClient(factory).SubmitAsync("/account/login", new Dictionary<string, string>
        {
            ["Login"] = "owner@basera.local",
            ["Password"] = "Passw0rd!",
            ["ReturnUrl"] = "/owner/staff",
        });

        response.Headers.Location!.OriginalString.Should().Be("/owner/staff");
    }

    [Theory]
    [InlineData("owner@basera.local", "/owner", HttpStatusCode.OK)]
    [InlineData("owner@basera.local", "/staff", HttpStatusCode.OK)]
    [InlineData("owner@basera.local", "/resident", HttpStatusCode.Redirect)]
    [InlineData("warden@basera.local", "/staff", HttpStatusCode.OK)]
    [InlineData("warden@basera.local", "/owner", HttpStatusCode.Redirect)]
    [InlineData("warden@basera.local", "/parent", HttpStatusCode.Redirect)]
    public async Task Each_area_admits_its_roles_and_denies_the_rest(string login, string path, HttpStatusCode expected)
    {
        var client = await SignInAsync(login);

        var response = await client.Http.GetAsync(path);

        response.StatusCode.Should().Be(expected);
        if (expected == HttpStatusCode.Redirect)
        {
            response.Headers.Location!.AbsolutePath.Should().Be("/account/denied");
        }
    }

    [Theory]
    [InlineData("owner@basera.local", "/owner", "Sunil Maharjan")]
    [InlineData("warden@basera.local", "/staff", "Bikash Tamang")]
    public async Task Every_area_header_shows_the_user_the_hostel_and_a_sign_out_button(string login, string path, string name)
    {
        var client = await SignInAsync(login);

        var html = await client.Http.GetStringAsync(path);

        var menu = Regex.Match(html, "<div class=\"d-flex align-items-center flex-wrap gap-2 small user-menu\">.*?</div>", RegexOptions.Singleline).Value;
        menu.Should().Contain(name).And.Contain("Sunrise Boys Hostel, Kirtipur").And.Contain("action=\"/account/logout\"")
            .And.Contain("name=\"__RequestVerificationToken\"").And.Contain("href=\"/account/profile\"");
    }

    [Fact]
    public async Task The_language_switch_is_saved_on_the_user_and_follows_them_to_another_device()
    {
        var owner = Owner.Random();
        var client = new AccountTestClient(factory);
        await client.RegisterAsync(owner);
        (await client.Http.GetStringAsync("/owner")).Should().Contain("<html lang=\"en\">");

        await client.SubmitAsync("/owner", new Dictionary<string, string> { ["culture"] = "ne", ["returnUrl"] = "/owner" }, "/language");

        client.Principal()!.FindFirst(BaseraClaimTypes.Language)!.Value.Should().Be("ne", "the cookie is re-issued with the new claim");
        var phone = new AccountTestClient(factory);
        await phone.LoginAsync(owner.Email, owner.Password);
        (await phone.Http.GetStringAsync("/owner")).Should().Contain("<html lang=\"ne\">", "a new device has no culture cookie, only the user");
        await using var scope = factory.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Users.IgnoreQueryFilters()
            .SingleAsync(u => u.Email == owner.Email)).PreferredLanguage.Should().Be("ne");
    }

    [Fact]
    public async Task The_profile_changes_the_name_in_the_header_at_once()
    {
        var owner = Owner.Random();
        var client = new AccountTestClient(factory);
        await client.RegisterAsync(owner);

        var saved = await client.SubmitAsync("/account/profile", new Dictionary<string, string>
        {
            ["Details.DisplayName"] = "Sita S. Sharma",
            ["Details.PreferredLanguage"] = "",
        });

        saved.Headers.Location!.OriginalString.Should().Be("/account/profile");
        client.Principal()!.FindFirst(BaseraClaimTypes.Name)!.Value.Should().Be("Sita S. Sharma");
        (await client.Http.GetStringAsync("/owner")).Should().Contain("Sita S. Sharma");
    }

    [Fact]
    public async Task An_empty_name_is_reported_under_its_input()
    {
        var client = new AccountTestClient(factory);
        await client.RegisterAsync(Owner.Random());

        var response = await client.SubmitAsync("/account/profile", new Dictionary<string, string> { ["Details.DisplayName"] = "" });

        var html = await response.Content.ReadAsStringAsync();
        html.Should().MatchRegex("data-valmsg-for=\"Details.DisplayName\"[^>]*>This field is required.");
    }

    [Fact]
    public async Task Changing_the_password_needs_the_current_one_and_keeps_this_session()
    {
        var owner = Owner.Random();
        var client = new AccountTestClient(factory);
        await client.RegisterAsync(owner);

        var wrong = await client.SubmitAsync("/account/profile", Password("Wr0ng-current", "N3w-password"), "/account/profile/password");
        (await wrong.Content.ReadAsStringAsync()).Should().MatchRegex("data-valmsg-for=\"Password.CurrentPassword\"[^>]*>The current password is not right.");

        var changed = await client.SubmitAsync("/account/profile", Password(owner.Password, "N3w-password"), "/account/profile/password");
        changed.Headers.Location!.OriginalString.Should().Be("/account/profile");
        (await client.Http.GetAsync("/owner")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await new AccountTestClient(factory).LoginAsync(owner.Email, "N3w-password")).Headers.Location!.OriginalString.Should().Be("/owner");
    }

    [Fact]
    public async Task Residents_have_no_password_to_change()
    {
        var client = new AccountTestClient(factory);
        await client.SubmitAsync("/account/otp", new Dictionary<string, string> { ["Phone"] = "9800000001" });
        var sms = factory.Services.GetRequiredService<MockSmsInbox>().Messages.First(m => m.Message.To.NationalNumber == "9800000001");
        await client.SubmitAsync("/account/otp/verify?phone=9800000001",
            new Dictionary<string, string> { ["Phone"] = "9800000001", ["Code"] = SmsCode().Match(sms.Message.Text).Groups[1].Value },
            "/account/otp/verify");

        var html = await client.Http.GetStringAsync("/account/profile");

        html.Should().Contain("Ram Thapa").And.NotContain("Password.CurrentPassword");
    }

    [Fact]
    public async Task The_profile_needs_a_sign_in()
    {
        var response = await new AccountTestClient(factory).Http.GetAsync("/account/profile");

        response.Headers.Location!.AbsolutePath.Should().Be("/account/login");
    }

    private async Task<AccountTestClient> SignInAsync(string login)
    {
        var client = new AccountTestClient(factory);
        (await client.LoginAsync(login, "Passw0rd!")).StatusCode.Should().Be(HttpStatusCode.Redirect);
        return client;
    }

    private static Dictionary<string, string> Password(string current, string next) => new()
    {
        ["Password.CurrentPassword"] = current,
        ["Password.NewPassword"] = next,
    };

    [GeneratedRegex(@"code: (\d{6})")]
    private static partial Regex SmsCode();
}
