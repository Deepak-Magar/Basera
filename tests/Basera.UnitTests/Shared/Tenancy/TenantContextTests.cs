using Basera.Shared.Tenancy;

namespace Basera.UnitTests.Shared.Tenancy;

public sealed class TenantContextTests
{
    private static readonly Guid TenantA = Guid.Parse("0192a1b2-0000-7000-8000-00000000000a");
    private static readonly Guid TenantB = Guid.Parse("0192a1b2-0000-7000-8000-00000000000b");

    [Fact]
    public void Starts_unresolved_and_required_id_throws()
    {
        var context = new TenantContext();

        context.IsResolved.Should().BeFalse();
        context.TenantId.Should().BeNull();
        context.Invoking(c => c.RequiredTenantId).Should().Throw<TenantNotResolvedException>();
    }

    [Fact]
    public void Set_resolves_the_tenant()
    {
        var context = new TenantContext();

        context.Set(TenantA);

        context.IsResolved.Should().BeTrue();
        context.RequiredTenantId.Should().Be(TenantA);
    }

    [Fact]
    public void Setting_the_same_tenant_again_is_allowed()
    {
        var context = new TenantContext();
        context.Set(TenantA);

        context.Invoking(c => c.Set(TenantA)).Should().NotThrow();
    }

    [Fact]
    public void Switching_to_another_tenant_in_the_same_scope_is_refused()
    {
        var context = new TenantContext();
        context.Set(TenantA);

        context.Invoking(c => c.Set(TenantB)).Should().Throw<InvalidOperationException>();
        context.TenantId.Should().Be(TenantA);
    }

    [Fact]
    public void Empty_tenant_id_is_refused()
    {
        new TenantContext().Invoking(c => c.Set(Guid.Empty)).Should().Throw<ArgumentException>();
    }
}
