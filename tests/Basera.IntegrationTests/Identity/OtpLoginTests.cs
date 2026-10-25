using System.Collections.Concurrent;
using System.Net;
using System.Text.RegularExpressions;
using Basera.IntegrationTests.Infrastructure;
using Basera.Modules.Identity.Contracts;
using Basera.Modules.Identity.Domain;
using Basera.Modules.Identity.Infrastructure;
using Basera.Shared.Domain;
using Basera.Shared.Domain.Calendar;
using Basera.Shared.Messaging;
using Basera.Shared.Time;
using Basera.Shared.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Basera.IntegrationTests.Identity;

/// <summary>
/// OTP sign-in for residents and parents and every limit of ARCHITECTURE §7, through the real pages. The clock
/// is controllable, and Residents' phone lookup is a fake that the tests fill.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed partial class OtpLoginTests(TestDatabase database) : IAsyncLifetime
{
    private readonly TestClock clock = new(DateTimeOffset.UtcNow);
    private readonly FakeResidentPhoneLookup residents = new();
    private BaseraWebFactory factory = null!;
    private Tenant tenant = null!;

    public async Task InitializeAsync()
    {
        factory = new BaseraWebFactory(
            database.ConnectionString,
            services => services
                .AddSingleton<IClock>(clock)
                .AddSingleton<IResidentPhoneLookup>(residents),
            settings: new Dictionary<string, string> { ["RateLimits:Auth:PermitLimit"] = "10000" });

        await using var scope = factory.Services.CreateAsyncScope();
        tenant = Tenant.Register("OTP Hostel", PhoneNumber.Create(RandomPhone()).Value, clock.Today).Value;
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => factory.DisposeAsync().AsTask();

    [Fact]
    public async Task The_first_correct_code_creates_the_resident_user_and_signs_in_for_two_weeks()
    {
        var phone = AddResident(PhoneHolderKind.Resident, "Ram Thapa");
        var client = new AccountTestClient(factory);

        (await RequestCodeAsync(client, phone)).Headers.Location!.OriginalString.Should().Be($"/account/otp/verify?phone={phone}");
        var response = await VerifyAsync(client, phone, LatestCode(phone));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.GetValues("Set-Cookie").Should().Contain(c => c.StartsWith(".Basera.Auth=") && c.Contains("expires="),
            "residents should not pay for an SMS every visit");
        var principal = client.Principal()!;
        principal.FindFirst(BaseraClaimTypes.Role)!.Value.Should().Be("Resident");
        principal.FindFirst(BaseraClaimTypes.Name)!.Value.Should().Be("Ram Thapa");
        principal.FindFirst(BaseraClaimTypes.TenantId)!.Value.Should().Be(tenant.Id.ToString());
        principal.FindFirst(BaseraClaimTypes.Language)!.Value.Should().Be("ne");

        var user = await UserAsync(phone);
        user.PhoneNumberConfirmed.Should().BeTrue();
        user.PhoneVerifiedAt.Should().BeCloseTo(clock.UtcNow, TimeSpan.FromMilliseconds(1), "PostgreSQL keeps microseconds");
        user.PasswordHash.Should().BeNull();
    }

    [Fact]
    public async Task A_guardian_signs_in_as_a_parent()
    {
        var phone = AddResident(PhoneHolderKind.Parent, "Gita Thapa");
        var client = new AccountTestClient(factory);

        await RequestCodeAsync(client, phone);
        await VerifyAsync(client, phone, LatestCode(phone));

        client.Principal()!.FindFirst(BaseraClaimTypes.Role)!.Value.Should().Be("Parent");
    }

    [Fact]
    public async Task The_next_sign_in_reuses_the_same_user()
    {
        var phone = AddResident(PhoneHolderKind.Resident, "Ram Thapa");
        await SignInAsync(new AccountTestClient(factory), phone);
        var first = await UserAsync(phone);

        var client = new AccountTestClient(factory);
        await SignInAsync(client, phone);

        client.Principal()!.FindFirst(BaseraClaimTypes.Subject)!.Value.Should().Be(first.Id.ToString());
        await using var scope = factory.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Memberships.IgnoreQueryFilters()
            .CountAsync(m => m.UserId == first.Id)).Should().Be(1);
    }

    [Fact]
    public async Task An_unknown_number_is_refused_with_a_clear_message_and_no_sms()
    {
        var phone = RandomPhone();

        var response = await RequestCodeAsync(new AccountTestClient(factory), phone);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("यो नम्बर कुनै होस्टलमा दर्ता छैन।");
        Inbox(phone).Should().BeEmpty();
    }

    [Fact]
    public async Task Staff_are_sent_to_the_password_page_and_no_sms()
    {
        var owner = Owner.Random();
        await new AccountTestClient(factory).RegisterAsync(owner);

        var response = await RequestCodeAsync(new AccountTestClient(factory), owner.Phone);

        (await response.Content.ReadAsStringAsync()).Should().Contain("यो नम्बर होस्टल कर्मचारीको हो।");
        Inbox(owner.Phone).Should().BeEmpty();
    }

    [Fact]
    public async Task The_code_has_six_digits_and_only_its_keyed_hash_is_stored()
    {
        var phone = AddResident(PhoneHolderKind.Resident, "Ram Thapa");
        var client = new AccountTestClient(factory);
        await RequestCodeAsync(client, phone);
        clock.Advance(TimeSpan.FromSeconds(1));
        await RequestCodeAsync(client, phone);

        var codes = Inbox(phone).Select(sms => SmsCode().Match(sms.Message.Text).Groups[1].Value).ToList();
        codes.Should().HaveCount(2).And.OnlyContain(code => code.Length == 6 && code.All(char.IsAsciiDigit));
        var hashes = await ChallengesAsync(phone);
        hashes.Select(c => c.CodeHash).Should().OnlyContain(hash => hash.Length == 64 && Regex.IsMatch(hash, "^[0-9A-F]+$"));
        hashes.Select(c => c.CodeHash).Should().NotContain(hash => codes.Any(code => hash.Contains(code)));
    }

    [Fact]
    public async Task A_code_works_until_five_minutes_and_not_after()
    {
        var phone = AddResident(PhoneHolderKind.Resident, "Ram Thapa");
        var client = new AccountTestClient(factory);
        await RequestCodeAsync(client, phone);
        var code = LatestCode(phone);

        clock.Advance(TimeSpan.FromMinutes(5));
        var late = await VerifyAsync(client, phone, code);

        (await late.Content.ReadAsStringAsync()).Should().Contain("यो कोड अब मान्य छैन।");
        client.AuthCookie.Should().BeNull();

        var other = AddResident(PhoneHolderKind.Resident, "Hari Rai");
        await RequestCodeAsync(client, other);
        clock.Advance(TimeSpan.FromMinutes(5) - TimeSpan.FromSeconds(1));
        (await VerifyAsync(client, other, LatestCode(other))).StatusCode.Should().Be(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task Five_wrong_codes_end_the_code_and_the_right_one_no_longer_works()
    {
        var phone = AddResident(PhoneHolderKind.Resident, "Ram Thapa");
        var client = new AccountTestClient(factory);
        await RequestCodeAsync(client, phone);
        var code = LatestCode(phone);
        var wrong = code == "000000" ? "111111" : "000000";

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            (await Body(await VerifyAsync(client, phone, wrong))).Should().Contain("कोड मिलेन।");
        }

        (await Body(await VerifyAsync(client, phone, wrong))).Should().Contain("धेरै पटक गलत भयो।");
        (await Body(await VerifyAsync(client, phone, code))).Should().Contain("धेरै पटक गलत भयो।");
        client.AuthCookie.Should().BeNull();
        (await ChallengesAsync(phone)).Single().Attempts.Should().Be(5);
    }

    [Fact]
    public async Task Three_codes_per_phone_in_ten_minutes_then_a_wait()
    {
        var phone = AddResident(PhoneHolderKind.Resident, "Ram Thapa");
        var client = new AccountTestClient(factory);

        for (var send = 1; send <= 3; send++)
        {
            (await RequestCodeAsync(client, phone)).StatusCode.Should().Be(HttpStatusCode.Redirect);
            clock.Advance(TimeSpan.FromMinutes(1));
        }

        var fourth = await RequestCodeAsync(client, phone);
        (await Body(fourth)).Should().Contain("यो नम्बरमा तीन पटक कोड पठाइसकियो।");
        Inbox(phone).Should().HaveCount(3);

        // The first code was sent at minute 0; at minute 10 it leaves the window.
        clock.Advance(TimeSpan.FromMinutes(7));
        (await RequestCodeAsync(client, phone)).StatusCode.Should().Be(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task Password_reset_codes_never_sign_in_and_count_towards_the_same_limit()
    {
        var resident = AddResident(PhoneHolderKind.Resident, "Ram Thapa");
        await SignInAsync(new AccountTestClient(factory), resident);
        var client = new AccountTestClient(factory);

        // The resident has a user now, so the forgot page sends a reset code: the second SMS in the window.
        await client.SubmitAsync("/account/forgot", new Dictionary<string, string> { ["Phone"] = resident });
        var asLogin = await VerifyAsync(client, resident, LatestCode(resident));

        (await Body(asLogin)).Should().Contain("यो कोड अब मान्य छैन।");
        client.AuthCookie.Should().BeNull("a password-reset code is not a login code");

        await client.SubmitAsync("/account/forgot", new Dictionary<string, string> { ["Phone"] = resident });
        (await Body(await RequestCodeAsync(client, resident))).Should().Contain("यो नम्बरमा तीन पटक कोड पठाइसकियो।");
        Inbox(resident).Should().HaveCount(3);
    }

    [Fact]
    public async Task Only_the_newest_code_works()
    {
        var phone = AddResident(PhoneHolderKind.Resident, "Ram Thapa");
        var client = new AccountTestClient(factory);
        await RequestCodeAsync(client, phone);
        var older = LatestCode(phone);
        clock.Advance(TimeSpan.FromSeconds(30));
        await RequestCodeAsync(client, phone);
        var newer = LatestCode(phone);

        if (older != newer)
        {
            (await Body(await VerifyAsync(client, phone, older))).Should().Contain("कोड मिलेन।");
        }

        (await VerifyAsync(client, phone, newer)).StatusCode.Should().Be(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task A_code_typed_in_nepali_digits_is_accepted()
    {
        var phone = AddResident(PhoneHolderKind.Resident, "Ram Thapa");
        var client = new AccountTestClient(factory);
        await RequestCodeAsync(client, phone);

        var response = await VerifyAsync(client, phone, NepaliDigits.Convert(LatestCode(phone)));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task The_otp_pages_start_in_nepali()
    {
        var html = await new AccountTestClient(factory).Http.GetStringAsync("/account/otp");

        html.Should().Contain("<html lang=\"ne\">").And.Contain("SMS कोडबाट साइन इन");
    }

    private string AddResident(PhoneHolderKind kind, string name)
    {
        var phone = RandomPhone();
        residents.Holders[PhoneNumber.Create(phone).Value.E164] = new PhoneHolder(tenant.Id, Guid.NewGuid(), kind, name);
        return phone;
    }

    private static Task<HttpResponseMessage> RequestCodeAsync(AccountTestClient client, string phone) =>
        client.SubmitAsync("/account/otp", new Dictionary<string, string> { ["Phone"] = phone });

    private static Task<HttpResponseMessage> VerifyAsync(AccountTestClient client, string phone, string code) =>
        client.SubmitAsync($"/account/otp/verify?phone={phone}", new Dictionary<string, string> { ["Phone"] = phone, ["Code"] = code },
            "/account/otp/verify");

    private async Task SignInAsync(AccountTestClient client, string phone)
    {
        await RequestCodeAsync(client, phone);
        (await VerifyAsync(client, phone, LatestCode(phone))).StatusCode.Should().Be(HttpStatusCode.Redirect);
    }

    private IReadOnlyList<SentSms> Inbox(string phone) =>
        factory.Services.GetRequiredService<MockSmsInbox>().Messages.Where(m => m.Message.To.NationalNumber == phone).ToList();

    private string LatestCode(string phone) => SmsCode().Match(Inbox(phone)[0].Message.Text).Groups[1].Value;

    private async Task<ApplicationUser> UserAsync(string phone)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Users.IgnoreQueryFilters()
            .SingleAsync(u => u.PhoneNumber == "+977" + phone);
    }

    private async Task<List<OtpChallenge>> ChallengesAsync(string phone)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().OtpChallenges
            .Where(c => c.Phone == "+977" + phone).ToListAsync();
    }

    private static string RandomPhone() => $"98{Random.Shared.Next(0, 100_000_000):D8}";

    private static Task<string> Body(HttpResponseMessage response) => response.Content.ReadAsStringAsync();

    [GeneratedRegex(@"code: (\d{6})")]
    private static partial Regex SmsCode();

    private sealed class FakeResidentPhoneLookup : IResidentPhoneLookup
    {
        public ConcurrentDictionary<string, PhoneHolder> Holders { get; } = new();

        public Task<PhoneHolder?> FindAsync(PhoneNumber phone, CancellationToken cancellationToken = default) =>
            Task.FromResult(Holders.TryGetValue(phone.E164, out var holder) ? holder : null);
    }
}

/// <summary>A clock the test moves by hand.</summary>
public sealed class TestClock(DateTimeOffset start) : IClock
{
    public DateTimeOffset UtcNow { get; private set; } = start;

    public DateTimeOffset NepalNow => UtcNow.ToOffset(SystemClock.NepalOffset);

    public DateOnly Today => DateOnly.FromDateTime(NepalNow.DateTime);

    public NepaliDate TodayBs => NepaliDate.FromDateOnly(Today);

    public void Advance(TimeSpan by) => UtcNow += by;
}
