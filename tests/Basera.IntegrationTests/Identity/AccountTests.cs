using System.Net;
using System.Text.RegularExpressions;
using Basera.IntegrationTests.Infrastructure;
using Basera.Modules.Identity.Domain;
using Basera.Modules.Identity.Infrastructure;
using Basera.Shared.Domain;
using Basera.Shared.Messaging;
using Basera.Shared.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Basera.IntegrationTests.Identity;

/// <summary>Owner registration, password sign-in, lockout and password reset, through the real pages.</summary>
[Collection(DatabaseCollection.Name)]
public sealed partial class AccountTests(TestDatabase database) : IAsyncLifetime
{
    // These tests post far more than the 10 per minute the "auth" limit allows; the limit has its own test.
    private readonly BaseraWebFactory factory = new(database.ConnectionString, settings: new Dictionary<string, string>
    {
        ["RateLimits:Auth:PermitLimit"] = "10000",
    });

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => factory.DisposeAsync().AsTask();

    [Fact]
    public async Task Registering_creates_the_hostel_the_owner_and_the_membership_and_signs_in()
    {
        var client = new AccountTestClient(factory);
        var owner = Owner.Random();

        var response = await client.RegisterAsync(owner);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Be("/owner");
        client.AuthCookie.Should().NotBeNull();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var tenant = await db.Tenants.SingleAsync(t => t.Name == owner.HostelName);
        tenant.Status.Should().Be(TenantStatus.Trial);
        var user = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Email == owner.Email);
        user.TenantId.Should().Be(tenant.Id);
        user.UserName.Should().Be("+977" + owner.Phone);
        user.DisplayName.Should().Be(owner.Name);
        var membership = await db.Memberships.IgnoreQueryFilters().SingleAsync(m => m.UserId == user.Id);
        membership.TenantId.Should().Be(tenant.Id);
        membership.Role.Should().Be(Role.Owner);
    }

    [Fact]
    public async Task The_cookie_carries_sub_name_tenant_role_and_language()
    {
        var owner = Owner.Random();
        await new AccountTestClient(factory).RegisterAsync(owner);
        var client = new AccountTestClient(factory);

        (await client.LoginAsync(owner.Email, owner.Password)).StatusCode.Should().Be(HttpStatusCode.Redirect);

        var principal = client.Principal()!;
        var (userId, tenantId) = await IdsOfAsync(owner);
        principal.FindFirst(BaseraClaimTypes.Subject)!.Value.Should().Be(userId.ToString());
        principal.FindFirst(BaseraClaimTypes.Name)!.Value.Should().Be(owner.Name);
        principal.FindFirst(BaseraClaimTypes.TenantId)!.Value.Should().Be(tenantId.ToString());
        principal.FindFirst(BaseraClaimTypes.Role)!.Value.Should().Be("Owner");
        principal.FindFirst(BaseraClaimTypes.Language)!.Value.Should().Be("en");
        principal.FindAll(BaseraClaimTypes.Role).Should().ContainSingle();
    }

    [Theory]
    [InlineData("national")]
    [InlineData("international")]
    [InlineData("email-upper-case")]
    public async Task Owners_sign_in_with_their_phone_in_any_form_or_their_email(string form)
    {
        var owner = Owner.Random();
        await new AccountTestClient(factory).RegisterAsync(owner);
        var login = form switch
        {
            "national" => owner.Phone,
            "international" => "+977 " + owner.Phone[..3] + "-" + owner.Phone[3..],
            _ => owner.Email.ToUpperInvariant(),
        };
        var client = new AccountTestClient(factory);

        var response = await client.LoginAsync(login, owner.Password);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Be("/owner", "an owner lands in their own area");
        client.AuthCookie.Should().NotBeNull();
    }

    [Fact]
    public async Task A_wrong_password_and_an_unknown_login_get_the_same_answer_and_no_cookie()
    {
        var owner = Owner.Random();
        await new AccountTestClient(factory).RegisterAsync(owner);
        var client = new AccountTestClient(factory);

        var wrong = await client.LoginAsync(owner.Email, "Wr0ng-password");
        var unknown = await client.LoginAsync("nobody-" + owner.Email, owner.Password);

        foreach (var response in new[] { wrong, unknown })
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.Content.ReadAsStringAsync()).Should().Contain("The email or mobile number and password don&#x27;t match.");
        }

        client.AuthCookie.Should().BeNull();
        await using var scope = factory.Services.CreateAsyncScope();
        var user = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Users.IgnoreQueryFilters()
            .SingleAsync(u => u.Email == owner.Email);
        user.AccessFailedCount.Should().Be(1);
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account_even_against_the_right_one()
    {
        var owner = Owner.Random();
        await new AccountTestClient(factory).RegisterAsync(owner);
        var client = new AccountTestClient(factory);

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            (await Body(await client.LoginAsync(owner.Phone, "Wr0ng-password"))).Should().Contain("don&#x27;t match");
        }

        (await Body(await client.LoginAsync(owner.Phone, "Wr0ng-password"))).Should().Contain("Too many wrong tries");
        var right = await client.LoginAsync(owner.Phone, owner.Password);

        right.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Body(right)).Should().Contain("Too many wrong tries");
        client.AuthCookie.Should().BeNull();
    }

    [Fact]
    public async Task A_taken_phone_number_is_refused_and_nothing_is_created()
    {
        var first = Owner.Random();
        await new AccountTestClient(factory).RegisterAsync(first);
        var second = Owner.Random() with { Phone = first.Phone };

        var response = await new AccountTestClient(factory).RegisterAsync(second);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Body(response)).Should().Contain("This mobile number already has a Basera account.");
        await using var scope = factory.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Tenants.AnyAsync(t => t.Name == second.HostelName))
            .Should().BeFalse();
    }

    [Fact]
    public async Task A_weak_password_is_refused_and_no_tenant_is_left_behind()
    {
        var owner = Owner.Random() with { Password = "short" };

        var response = await new AccountTestClient(factory).RegisterAsync(owner);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Body(response)).Should().Contain("Passwords must be at least 6 characters.");
        await using var scope = factory.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Tenants.AnyAsync(t => t.Name == owner.HostelName))
            .Should().BeFalse("the tenant, user and membership are saved together or not at all");
    }

    [Fact]
    public async Task Empty_and_malformed_fields_are_reported_next_to_their_inputs()
    {
        var response = await new AccountTestClient(factory).RegisterAsync(new Owner("", "", "", "not-an-email", ""));

        var html = await Body(response);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        Regex.Count(html, "This field is required.").Should().Be(4);
        html.Should().Contain("Enter a valid email address.");
    }

    [Fact]
    public async Task Signing_out_removes_the_cookie()
    {
        var client = new AccountTestClient(factory);
        await client.RegisterAsync(Owner.Random());

        var response = await client.SubmitAsync("/owner", new Dictionary<string, string>(), "/account/logout");

        response.Headers.Location!.OriginalString.Should().Be("/");
        client.AuthCookie.Should().BeNull();
    }

    [Fact]
    public async Task Login_never_redirects_off_site()
    {
        var owner = Owner.Random();
        await new AccountTestClient(factory).RegisterAsync(owner);

        var response = await new AccountTestClient(factory).SubmitAsync("/account/login", new Dictionary<string, string>
        {
            ["Login"] = owner.Email,
            ["Password"] = owner.Password,
            ["ReturnUrl"] = "https://evil.example/",
        });

        response.Headers.Location!.OriginalString.Should().Be("/owner");
    }

    [Fact]
    public async Task A_forgotten_password_is_reset_with_the_sms_code()
    {
        var owner = Owner.Random();
        await new AccountTestClient(factory).RegisterAsync(owner);
        var client = new AccountTestClient(factory);

        var sent = await client.SubmitAsync("/account/forgot", new Dictionary<string, string> { ["Phone"] = owner.Phone });
        sent.Headers.Location!.OriginalString.Should().StartWith("/account/reset");
        var code = LatestCode(owner.Phone);

        var wrong = await client.SubmitAsync("/account/reset", Reset(owner.Phone, code == "000000" ? "111111" : "000000", "N3w-password"));
        (await Body(wrong)).Should().Contain("That code is not right.");
        var reset = await client.SubmitAsync("/account/reset", Reset(owner.Phone, code, "N3w-password"));

        reset.Headers.Location!.OriginalString.Should().Be("/account/login");
        (await Body(await client.LoginAsync(owner.Phone, owner.Password))).Should().Contain("don&#x27;t match");
        (await client.LoginAsync(owner.Phone, "N3w-password")).StatusCode.Should().Be(HttpStatusCode.Redirect);
        await using var scope = factory.Services.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().OtpChallenges
            .Where(c => c.Phone == "+977" + owner.Phone).ToListAsync();
        stored.Should().ContainSingle().Which.CodeHash.Should().NotContain(code).And.HaveLength(64);
    }

    [Fact]
    public async Task A_reset_also_ends_a_lockout_and_proves_the_phone()
    {
        var owner = Owner.Random();
        await new AccountTestClient(factory).RegisterAsync(owner);
        var client = new AccountTestClient(factory);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await client.LoginAsync(owner.Phone, "Wr0ng-password");
        }

        await client.SubmitAsync("/account/forgot", new Dictionary<string, string> { ["Phone"] = owner.Phone });
        await client.SubmitAsync("/account/reset", Reset(owner.Phone, LatestCode(owner.Phone), "N3w-password"));

        (await client.LoginAsync(owner.Phone, "N3w-password")).StatusCode.Should().Be(HttpStatusCode.Redirect);
        await using var scope = factory.Services.CreateAsyncScope();
        var user = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Users.IgnoreQueryFilters()
            .SingleAsync(u => u.Email == owner.Email);
        user.PhoneVerifiedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task A_weak_new_password_does_not_use_up_the_code()
    {
        var owner = Owner.Random();
        await new AccountTestClient(factory).RegisterAsync(owner);
        var client = new AccountTestClient(factory);
        await client.SubmitAsync("/account/forgot", new Dictionary<string, string> { ["Phone"] = owner.Phone });
        var code = LatestCode(owner.Phone);

        (await Body(await client.SubmitAsync("/account/reset", Reset(owner.Phone, code, "weak")))).Should().Contain("Passwords must be");
        var retry = await client.SubmitAsync("/account/reset", Reset(owner.Phone, code, "N3w-password"));

        retry.Headers.Location!.OriginalString.Should().Be("/account/login");
    }

    [Fact]
    public async Task An_unknown_number_gets_the_same_answer_and_no_sms()
    {
        var phone = Owner.Random().Phone;

        var response = await new AccountTestClient(factory).SubmitAsync("/account/forgot", new Dictionary<string, string> { ["Phone"] = phone });

        response.Headers.Location!.OriginalString.Should().StartWith("/account/reset");
        factory.Services.GetRequiredService<MockSmsInbox>().Messages.Should().NotContain(m => m.Message.To.NationalNumber == phone);
    }

    [Fact]
    public async Task A_signed_in_request_rechecks_the_security_stamp_with_the_tenant_from_the_cookie()
    {
        // ValidationInterval zero: every request reloads the user, which needs the tenant before tenancy runs.
        await using var strict = new BaseraWebFactory(
            database.ConnectionString,
            services => services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.Zero),
            settings: new Dictionary<string, string> { ["RateLimits:Auth:PermitLimit"] = "10000" });
        var owner = Owner.Random();
        var client = new AccountTestClient(strict);
        await client.RegisterAsync(owner);

        var page = await client.Http.GetAsync("/owner");

        page.StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.Http.GetAsync("/account/login")).Headers.Location!.OriginalString.Should().Be("/", "the cookie is still valid");
    }

    [Fact]
    public async Task Account_posts_are_rate_limited_by_the_auth_policy_but_pages_are_not()
    {
        await using var limited = new BaseraWebFactory(database.ConnectionString, settings: new Dictionary<string, string>
        {
            ["RateLimits:Auth:PermitLimit"] = "2",
        });
        var client = new AccountTestClient(limited);

        (await client.LoginAsync("someone@basera.test", "x")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.LoginAsync("someone@basera.test", "x")).StatusCode.Should().Be(HttpStatusCode.OK);
        var rejected = await client.LoginAsync("someone@basera.test", "x");

        rejected.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        rejected.Headers.RetryAfter.Should().NotBeNull();
        (await client.Http.GetAsync("/account/login")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static Dictionary<string, string> Reset(string phone, string code, string password) => new()
    {
        ["Phone"] = phone,
        ["Code"] = code,
        ["NewPassword"] = password,
    };

    private string LatestCode(string nationalNumber)
    {
        var sms = factory.Services.GetRequiredService<MockSmsInbox>().Messages.First(m => m.Message.To.NationalNumber == nationalNumber);
        return SmsCode().Match(sms.Message.Text).Groups[1].Value;
    }

    private async Task<(Guid UserId, Guid TenantId)> IdsOfAsync(Owner owner)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var user = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Users.IgnoreQueryFilters()
            .SingleAsync(u => u.Email == owner.Email);
        return (user.Id, user.TenantId);
    }

    private static Task<string> Body(HttpResponseMessage response) => response.Content.ReadAsStringAsync();

    [GeneratedRegex(@"code: (\d{6})")]
    private static partial Regex SmsCode();
}
