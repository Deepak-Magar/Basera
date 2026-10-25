using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Basera.IntegrationTests.Infrastructure;
using Basera.Shared.DataProtection;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Basera.IntegrationTests.Security;

/// <summary>
/// Two hosts on one database stand in for "before and after a restart" and for two instances behind a proxy.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed partial class DataProtectionTests(TestDatabase database)
{
    private const string Purpose = "Basera.IntegrationTests.DataProtection";

    [Fact]
    public async Task A_value_protected_by_one_host_is_read_by_another_sharing_the_database()
    {
        string payload;
        await using (var first = new BaseraWebFactory(database.ConnectionString))
        {
            payload = first.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector(Purpose).Protect("बसेरा 42");
        }

        await using var second = new BaseraWebFactory(database.ConnectionString);
        var protector = second.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector(Purpose);

        protector.Unprotect(payload).Should().Be("बसेरा 42");
    }

    [Fact]
    public async Task The_key_ring_lives_in_the_dataprotection_table()
    {
        await using var factory = new BaseraWebFactory(database.ConnectionString);
        factory.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector(Purpose).Protect("x");

        var options = factory.Services.GetRequiredService<IOptions<KeyManagementOptions>>().Value;
        options.XmlRepository.Should().BeOfType<EntityFrameworkCoreXmlRepository<DataProtectionKeysDbContext>>();

        using var scope = factory.Services.CreateScope();
        var rows = await scope.ServiceProvider.GetRequiredService<DataProtectionKeysDbContext>()
            .DataProtectionKeys.AsNoTracking().Select(key => key.Xml!).ToListAsync();
        var storedIds = rows.Select(xml => Guid.Parse(XElement.Parse(xml).Attribute("id")!.Value)).ToHashSet();
        var keyIds = factory.Services.GetRequiredService<IKeyManager>().GetAllKeys().Select(key => key.KeyId).ToList();

        keyIds.Should().NotBeEmpty().And.OnlyContain(id => storedIds.Contains(id));
    }

    [Fact]
    public async Task An_antiforgery_token_issued_by_one_host_is_accepted_by_another()
    {
        await using var first = new BaseraWebFactory(database.ConnectionString);
        await using var second = new BaseraWebFactory(database.ConnectionString);
        var options = new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false };

        var page = await first.CreateClient(options).GetAsync("/staff");
        var cookie = page.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith(".AspNetCore.Antiforgery.", StringComparison.Ordinal));
        var token = HtmxToken().Match(await page.Content.ReadAsStringAsync()).Groups[1].Value;

        var request = new HttpRequestMessage(HttpMethod.Post, "/language")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["culture"] = "en", ["returnUrl"] = "/staff" }),
        };
        request.Headers.Add("Cookie", cookie.Split(';')[0]);
        request.Headers.Add("RequestVerificationToken", token);
        var response = await second.CreateClient(options).SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
    }

    [Fact]
    public void Every_instance_uses_the_same_application_name()
    {
        using var factory = new BaseraWebFactory(database.ConnectionString);

        factory.Services.GetRequiredService<IOptions<DataProtectionOptions>>().Value.ApplicationDiscriminator
            .Should().Be(BaseraDataProtection.ApplicationName);
    }

    [GeneratedRegex("RequestVerificationToken&quot;:&quot;([^&]+)&quot;")]
    private static partial Regex HtmxToken();
}
