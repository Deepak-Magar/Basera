using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Basera.IntegrationTests.Host;

public sealed class HostTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
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
    [InlineData("/health")]
    [InlineData("/owner")]
    [InlineData("/staff")]
    [InlineData("/resident")]
    [InlineData("/parent")]
    [InlineData("/api")]
    public async Task Host_endpoints_respond_ok(string path)
    {
        var response = await factory.CreateClient().GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
