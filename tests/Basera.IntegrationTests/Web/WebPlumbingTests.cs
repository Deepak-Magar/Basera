using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Basera.IntegrationTests.Infrastructure;
using Basera.Shared.Domain;
using Basera.Shared.Messaging;
using Basera.Shared.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Basera.IntegrationTests.Web;

/// <summary>Feature 6 through the real pipeline: envelope, errors, rate limits, HTMX, i18n, PWA, SMS, health.</summary>
[Collection(DatabaseCollection.Name)]
public sealed partial class WebPlumbingTests(TestDatabase database) : IDisposable
{
    private readonly BaseraWebFactory factory = new(database.ConnectionString, services =>
        services.AddControllersWithViews().AddApplicationPart(typeof(ThrowingController).Assembly));

    public void Dispose() => factory.Dispose();

    [Fact]
    public async Task Api_success_is_an_envelope_and_echoes_a_supplied_correlation_id()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, "test-correlation-0001");

        var response = await client.GetAsync("/api/ping");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Should().Equal("test-correlation-0001");
        body.GetProperty("success").GetBoolean().Should().BeTrue();
        body.GetProperty("data").GetProperty("status").GetString().Should().Be("ok");
        body.GetProperty("correlationId").GetString().Should().Be("test-correlation-0001");
    }

    [Fact]
    public async Task Unsafe_correlation_ids_are_replaced()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, "<script>");

        var response = await client.GetAsync("/api/ping");

        response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single().Should().MatchRegex("^[A-Za-z0-9-]+$");
    }

    [Fact]
    public async Task Unknown_api_route_is_a_not_found_envelope()
    {
        var response = await factory.CreateClient().GetAsync("/api/does-not-exist");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        body.GetProperty("success").GetBoolean().Should().BeFalse();
        body.GetProperty("error").GetProperty("code").GetString().Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task Unknown_page_is_a_not_found_page_in_the_area_language()
    {
        var response = await factory.CreateClient().GetAsync("/staff/does-not-exist");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        html.Should().Contain("पृष्ठ फेला परेन");
    }

    [Fact]
    public async Task Api_exception_is_a_500_envelope_with_the_correlation_id()
    {
        var response = await factory.CreateClient().GetAsync("/api/test/boom");
        var text = await response.Content.ReadAsStringAsync();
        text.Should().StartWith("{", "an /api error is JSON, got: {0}", text[..Math.Min(text.Length, 3000)]);
        var body = JsonDocument.Parse(text).RootElement;

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        body.GetProperty("error").GetProperty("code").GetString().Should().Be("INTERNAL");
        body.GetProperty("error").GetProperty("message").GetString().Should().Contain("Boom for the test", "Development shows details");
        body.GetProperty("correlationId").GetString().Should().Be(response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single());
    }

    [Fact]
    public async Task Page_exception_is_the_error_page_with_a_reference()
    {
        var response = await factory.CreateClient().GetAsync("/test/boom");
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        html.Should().Contain("Something went wrong");
        html.Should().Contain(response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single());
    }

    [Fact]
    public async Task Exception_details_are_hidden_outside_development()
    {
        using var production = new BaseraWebFactory(database.ConnectionString, services =>
            services.AddControllersWithViews().AddApplicationPart(typeof(ThrowingController).Assembly), environment: "Production");

        var api = await (await production.CreateClient().GetAsync("/api/test/boom")).Content.ReadAsStringAsync();
        var page = await (await production.CreateClient().GetAsync("/test/boom")).Content.ReadAsStringAsync();

        api.Should().NotContain("Boom for the test");
        page.Should().NotContain("Boom for the test").And.NotContain("InvalidOperationException");
    }

    [Fact]
    public async Task Htmx_exception_keeps_the_page_and_raises_a_client_event()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(HtmxHeaders.Request, "true");

        var response = await client.GetAsync("/test/boom");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Headers.GetValues(HtmxHeaders.Reswap).Should().Equal("none");
        var trigger = JsonDocument.Parse(response.Headers.GetValues(HtmxHeaders.Trigger).Single()).RootElement;
        trigger.GetProperty(BaseraExceptionHandler.HtmxErrorEvent).GetProperty("correlationId").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Api_is_rate_limited_per_caller_with_a_429_envelope()
    {
        using var limited = new BaseraWebFactory(database.ConnectionString, settings: new Dictionary<string, string>
        {
            ["RateLimits:Api:PermitLimit"] = "3",
        });
        var client = limited.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            (await client.GetAsync("/api/ping")).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var rejected = await client.GetAsync("/api/ping");
        var body = await rejected.Content.ReadFromJsonAsync<JsonElement>();

        rejected.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        rejected.Headers.RetryAfter.Should().NotBeNull();
        body.GetProperty("error").GetProperty("code").GetString().Should().Be("RATE_LIMITED");
        (await client.GetAsync("/owner")).StatusCode.Should().Be(HttpStatusCode.OK, "pages outside /api and /account are not limited");
    }

    // The area pages themselves need a sign-in now; ShellTests checks a signed-in user's language. These are the
    // anonymous pages, and BaseraCultures.DefaultFor is unit-tested for every area.
    [Theory]
    [InlineData("/account/login", "en")]
    [InlineData("/account/otp", "ne")]
    [InlineData("/", "en")]
    public async Task Each_anonymous_page_has_its_default_language(string path, string language)
    {
        var html = await factory.CreateClient().GetStringAsync(path);

        html.Should().Contain($"<html lang=\"{language}\">");
    }

    [Fact]
    public async Task The_language_switch_form_carries_its_own_token_so_it_works_without_javascript()
    {
        var html = await factory.CreateClient().GetStringAsync("/");

        var form = Regex.Match(html, "<form[^>]*action=\"/language\".*?</form>", RegexOptions.Singleline).Value;
        form.Should().Contain("name=\"__RequestVerificationToken\"");
    }

    [Fact]
    public async Task Language_switch_needs_the_antiforgery_token()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.PostAsync("/language", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["culture"] = "en",
            ["returnUrl"] = "/staff",
        }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Language_switch_with_the_htmx_token_header_sets_the_cookie_and_goes_back()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var otp = await client.GetStringAsync("/account/otp");
        otp.Should().Contain("SMS कोडबाट साइन इन");

        var request = new HttpRequestMessage(HttpMethod.Post, "/language")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["culture"] = "en", ["returnUrl"] = "/account/otp" }),
        };
        request.Headers.Add("RequestVerificationToken", HtmxToken().Match(otp).Groups[1].Value);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Be("/account/otp");
        response.Headers.GetValues("Set-Cookie").Should().Contain(cookie => cookie.StartsWith(".AspNetCore.Culture=c%3Den%7Cuic%3Den"));
        var english = await client.GetStringAsync("/account/otp");
        english.Should().Contain("<html lang=\"en\">").And.Contain("Sign in with an SMS code");
    }

    [Fact]
    public async Task Language_switch_never_redirects_off_site()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var token = HtmxToken().Match(await client.GetStringAsync("/")).Groups[1].Value;
        var request = new HttpRequestMessage(HttpMethod.Post, "/language")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["culture"] = "ne", ["returnUrl"] = "https://evil.example/" }),
        };
        request.Headers.Add("RequestVerificationToken", token);

        var response = await client.SendAsync(request);

        response.Headers.Location!.OriginalString.Should().Be("/");
    }

    [Fact]
    public async Task Mock_sms_appears_in_the_development_inbox()
    {
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISmsSender>()
                .SendAsync(new SmsMessage(PhoneNumber.Create("9812345678").Value, "तपाईंको OTP 482913 हो।", "otp"));
        }

        var html = await factory.CreateClient().GetStringAsync("/dev/sms");

        html.Should().Contain("तपाईंको OTP 482913 हो।").And.Contain("tel:&#x2B;9779812345678").And.Contain("otp");
    }

    [Fact]
    public async Task Dev_pages_do_not_exist_outside_development()
    {
        using var production = new BaseraWebFactory(database.ConnectionString, environment: "Production");

        (await production.CreateClient().GetAsync("/dev/sms")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Pwa_manifest_and_service_worker_are_served_correctly()
    {
        var client = factory.CreateClient();

        var manifest = await client.GetAsync("/manifest.webmanifest");
        var worker = await client.GetAsync("/sw.js");
        var page = await client.GetStringAsync("/");

        manifest.Content.Headers.ContentType!.MediaType.Should().Be("application/manifest+json");
        JsonDocument.Parse(await manifest.Content.ReadAsStringAsync()).RootElement.GetProperty("icons").GetArrayLength().Should().Be(2);
        worker.StatusCode.Should().Be(HttpStatusCode.OK);
        worker.Headers.CacheControl!.NoCache.Should().BeTrue();
        (await client.GetAsync("/icons/icon-512.png")).StatusCode.Should().Be(HttpStatusCode.OK);
        page.Should().Contain("rel=\"manifest\"");
    }

    [Fact]
    public async Task Health_reports_database_storage_and_hangfire()
    {
        var response = await factory.CreateClient().GetAsync("/health");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("status").GetString().Should().Be("Healthy");
        body.GetProperty("checks").EnumerateArray()
            .Select(check => (check.GetProperty("name").GetString(), check.GetProperty("status").GetString()))
            .Should().BeEquivalentTo([("database", "Healthy"), ("storage", "Healthy"), ("hangfire", "Healthy")]);
    }

    [Fact]
    public async Task Responses_carry_the_baseline_security_headers()
    {
        var response = await factory.CreateClient().GetAsync("/");

        response.Headers.GetValues("X-Content-Type-Options").Should().Equal("nosniff");
        response.Headers.GetValues("X-Frame-Options").Should().Equal("DENY");
        response.Headers.GetValues("Referrer-Policy").Should().Equal("strict-origin-when-cross-origin");
    }

    // hx-headers='{&quot;RequestVerificationToken&quot;:&quot;TOKEN&quot;}' on the layout's body.
    [GeneratedRegex("RequestVerificationToken&quot;:&quot;([^&]+)&quot;")]
    private static partial Regex HtmxToken();
}
