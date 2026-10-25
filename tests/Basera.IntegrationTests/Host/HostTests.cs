using System.Net;
using Basera.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Basera.IntegrationTests.Host;

// The host needs a database at startup now: Hangfire storage and its recurring jobs.
[Collection(DatabaseCollection.Name)]
public sealed class HostTests(TestDatabase database) : IDisposable
{
    private readonly BaseraWebFactory factory = new(database.ConnectionString);

    private static readonly string[] AllModules =
    [
        "Audit", "Billing", "Compliance", "Identity", "Mess", "Notifications",
        "Operations", "Payments", "Properties", "Reporting", "Residents",
    ];

    [Fact]
    public async Task Landing_page_lists_all_eleven_modules()
    {
        var html = await factory.CreateClient().GetStringAsync("/");

        html.Should().Contain("Loaded modules (11)");
        foreach (var module in AllModules)
        {
            html.Should().Contain($">{module}</li>");
        }
    }

    [Theory]
    [InlineData("/owner")]
    [InlineData("/staff")]
    [InlineData("/resident")]
    [InlineData("/parent")]
    public async Task Area_pages_send_anonymous_visitors_to_sign_in(string path)
    {
        var response = await factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }).GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.PathAndQuery.Should().Be($"/account/login?ReturnUrl={Uri.EscapeDataString(path)}");
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/api")]
    [InlineData("/account/login")]
    public async Task Host_endpoints_respond_ok(string path)
    {
        var response = await factory.CreateClient().GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    public void Dispose() => factory.Dispose();
}
