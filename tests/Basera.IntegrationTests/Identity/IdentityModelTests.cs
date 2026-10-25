using System.Security.Claims;
using Basera.IntegrationTests.Infrastructure;
using Basera.Modules.Identity.Domain;
using Basera.Modules.Identity.Infrastructure;
using Basera.Shared.Authorization;
using Basera.Shared.Domain;
using Basera.Shared.Tenancy;
using Basera.Shared.Time;
using Basera.Shared.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Basera.IntegrationTests.Identity;

/// <summary>The identity schema through the host's real registration: users and memberships are tenant-filtered.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class IdentityModelTests(TestDatabase database) : IAsyncLifetime
{
    private const string Password = "Passw0rd!";
    private readonly BaseraWebFactory factory = new(database.ConnectionString);
    private Tenant tenantA = null!;
    private Tenant tenantB = null!;

    public async Task InitializeAsync()
    {
        tenantA = await CreateTenantAsync("Hostel A");
        tenantB = await CreateTenantAsync("Hostel B");
    }

    public Task DisposeAsync() => factory.DisposeAsync().AsTask();

    [Fact]
    public async Task A_new_user_and_membership_get_the_current_tenant()
    {
        var user = await CreateUserAsync(tenantA, Role.Owner);

        await using var scope = Scope(tenantA);
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var stored = await db.Users.SingleAsync(u => u.Id == user.Id);
        stored.TenantId.Should().Be(tenantA.Id);
        stored.CreatedAt.Should().BeAfter(DateTimeOffset.UtcNow.AddMinutes(-1));
        var membership = await db.Memberships.SingleAsync(m => m.UserId == user.Id);
        membership.TenantId.Should().Be(tenantA.Id);
        membership.Role.Should().Be(Role.Owner);
    }

    [Fact]
    public async Task Another_tenant_cannot_find_the_user_or_the_membership()
    {
        var user = await CreateUserAsync(tenantA, Role.Warden);

        await using var scope = Scope(tenantB);
        (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(user.Id.ToString())).Should().BeNull();
        (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByNameAsync(user.UserName!)).Should().BeNull();
        (await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Memberships.AnyAsync(m => m.UserId == user.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Without_a_tenant_the_user_store_throws_instead_of_searching_everyone()
    {
        var user = await CreateUserAsync(tenantA, Role.Owner);

        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        await users.Invoking(u => u.FindByNameAsync(user.UserName!)).Should().ThrowAsync<TenantNotResolvedException>();
    }

    [Fact]
    public async Task The_sign_in_lookup_finds_the_user_and_tenant_by_phone_or_email_with_no_tenant_set()
    {
        var user = await CreateUserAsync(tenantB, Role.Owner);
        var phone = PhoneNumber.Create(user.PhoneNumber).Value;

        await using var scope = factory.Services.CreateAsyncScope();
        var lookup = scope.ServiceProvider.GetRequiredService<SignInLookup>();
        var expected = new SignInCandidate(user.Id, tenantB.Id);

        (await lookup.FindAsync(phone.NationalNumber)).Should().Be(expected);
        (await lookup.FindAsync(phone.ToInternationalString())).Should().Be(expected);
        (await lookup.FindAsync(user.Email!.ToUpperInvariant())).Should().Be(expected);
        (await lookup.FindAsync("nobody@basera.test")).Should().BeNull();
        (await lookup.FindAsync("12345")).Should().BeNull();
        (await lookup.FindAsync("  ")).Should().BeNull();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().IsResolved.Should().BeFalse();
    }

    [Fact]
    public async Task A_phone_number_belongs_to_one_user_across_all_tenants()
    {
        var existing = await CreateUserAsync(tenantA, Role.Owner);

        // Identity's duplicate check only sees tenant B, so the unique index is what refuses it.
        var duplicate = () => CreateUserAsync(tenantB, Role.Owner, existing.PhoneNumber);

        await duplicate.Should().ThrowAsync<DbUpdateException>();
    }

    [Theory]
    [InlineData(BaseraPolicies.RequireOwner, "Owner")]
    [InlineData(BaseraPolicies.RequireManagement, "Owner,Manager")]
    [InlineData(BaseraPolicies.RequireStaff, "Owner,Manager,Warden,Cook")]
    [InlineData(BaseraPolicies.RequireResident, "Resident")]
    [InlineData(BaseraPolicies.RequireParent, "Parent")]
    public async Task Each_policy_admits_exactly_its_roles(string policy, string admitted)
    {
        var authorization = factory.Services.GetRequiredService<IAuthorizationService>();
        var allowed = admitted.Split(',');

        foreach (var role in BaseraRoles.All)
        {
            var principal = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(BaseraClaimTypes.Subject, Guid.NewGuid().ToString()), new Claim(BaseraClaimTypes.Role, role)],
                authenticationType: "Test"));
            var result = await authorization.AuthorizeAsync(principal, policy);
            result.Succeeded.Should().Be(allowed.Contains(role), $"{policy} for {role}");
        }

        var anonymous = new ClaimsPrincipal(new ClaimsIdentity([new Claim(BaseraClaimTypes.Role, allowed[0])]));
        (await authorization.AuthorizeAsync(anonymous, policy)).Succeeded.Should().BeFalse("an unauthenticated identity never passes");
    }

    private AsyncServiceScope Scope(Tenant tenant)
    {
        var scope = factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenant.Id);
        return scope;
    }

    private async Task<Tenant> CreateTenantAsync(string name)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var today = scope.ServiceProvider.GetRequiredService<IClock>().Today;
        var tenant = Tenant.Register(name, RandomPhone(), today).Value;
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        return tenant;
    }

    private async Task<ApplicationUser> CreateUserAsync(Tenant tenant, Role role, string? phone = null)
    {
        await using var scope = Scope(tenant);
        var e164 = phone ?? RandomPhone().E164;
        var user = new ApplicationUser
        {
            UserName = e164,
            PhoneNumber = e164,
            Email = $"{Guid.NewGuid():N}@basera.test",
            DisplayName = "Test user",
        };
        var created = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(user, Password);
        created.Succeeded.Should().BeTrue(string.Join("; ", created.Errors.Select(e => e.Description)));

        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        db.Memberships.Add(new TenantMembership(user.Id, role));
        await db.SaveChangesAsync();
        return user;
    }

    // 98 + 8 random digits: unique enough across one test database.
    private static PhoneNumber RandomPhone() => PhoneNumber.Create($"98{Random.Shared.Next(0, 100_000_000):D8}").Value;
}
