using System.Security.Claims;
using Basera.Shared.Tenancy;
using Basera.Shared.Users;
using Microsoft.AspNetCore.Http;

namespace Basera.UnitTests.Shared.Tenancy;

public sealed class TenantResolutionMiddlewareTests
{
    private static readonly Guid Tenant = Guid.Parse("0192a1b2-0000-7000-8000-00000000000a");

    [Fact]
    public async Task Signed_in_user_with_a_tenant_claim_resolves_the_tenant()
    {
        var tenantContext = await RunAsync(SignedIn(new Claim(BaseraClaimTypes.TenantId, Tenant.ToString())));

        tenantContext.TenantId.Should().Be(Tenant);
    }

    [Fact]
    public async Task Anonymous_request_stays_unresolved_even_with_the_claim()
    {
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity([new Claim(BaseraClaimTypes.TenantId, Tenant.ToString())]));

        var tenantContext = await RunAsync(anonymous);

        tenantContext.IsResolved.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task Missing_or_invalid_claim_stays_unresolved(string? claim)
    {
        var user = claim is null ? SignedIn() : SignedIn(new Claim(BaseraClaimTypes.TenantId, claim));

        var tenantContext = await RunAsync(user);

        tenantContext.IsResolved.Should().BeFalse();
    }

    [Fact]
    public async Task Calls_the_next_middleware()
    {
        var called = false;
        var middleware = new TenantResolutionMiddleware(_ => { called = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(new DefaultHttpContext(), new TenantContext());

        called.Should().BeTrue();
    }

    private static ClaimsPrincipal SignedIn(params Claim[] claims) => new(new ClaimsIdentity(claims, "Test"));

    private static async Task<TenantContext> RunAsync(ClaimsPrincipal user)
    {
        var tenantContext = new TenantContext();
        var middleware = new TenantResolutionMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(new DefaultHttpContext { User = user }, tenantContext);
        return tenantContext;
    }
}
