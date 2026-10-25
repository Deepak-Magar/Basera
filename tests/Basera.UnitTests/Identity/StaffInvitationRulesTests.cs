using System.Security.Claims;
using Basera.Modules.Identity.Domain;
using Basera.Modules.Identity.Infrastructure;
using Basera.Shared.Domain;
using Basera.Shared.Users;
using Microsoft.AspNetCore.Authorization;

namespace Basera.UnitTests.Identity;

public sealed class StaffInvitationRulesTests
{
    private static readonly PhoneNumber Phone = PhoneNumber.Create("9812345678").Value;
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(Role.Manager)]
    [InlineData(Role.Warden)]
    [InlineData(Role.Cook)]
    public void Staff_roles_can_be_invited(Role role) =>
        StaffInvitation.Create(Phone, role, "HASH", Now).Value.StateAt(Now).Should().Be(InvitationState.Pending);

    [Theory]
    [InlineData(Role.Owner)]
    [InlineData(Role.Resident)]
    [InlineData(Role.Parent)]
    public void Other_roles_cannot(Role role) =>
        StaffInvitation.Create(Phone, role, "HASH", Now).Error!.Target.Should().Be("role");

    [Fact]
    public void An_invitation_is_pending_for_seven_days_then_expires()
    {
        var invitation = StaffInvitation.Create(Phone, Role.Warden, "HASH", Now).Value;

        invitation.StateAt(Now.AddDays(7).AddTicks(-1)).Should().Be(InvitationState.Pending);
        invitation.StateAt(Now.AddDays(7)).Should().Be(InvitationState.Expired);
        invitation.Accept(Now.AddDays(7)).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void An_accepted_invitation_cannot_be_accepted_again_or_revoked()
    {
        var invitation = StaffInvitation.Create(Phone, Role.Warden, "HASH", Now).Value;

        invitation.Accept(Now).IsSuccess.Should().BeTrue();
        invitation.Revoke(Now);

        invitation.StateAt(Now).Should().Be(InvitationState.Accepted);
        invitation.Accept(Now).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void A_revoked_invitation_cannot_be_accepted()
    {
        var invitation = StaffInvitation.Create(Phone, Role.Warden, "HASH", Now).Value;

        invitation.Revoke(Now);

        invitation.Accept(Now).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Owners_are_not_managed_staff_and_roles_stay_within_staff()
    {
        var owner = new TenantMembership(Guid.NewGuid(), Role.Owner);
        var warden = new TenantMembership(Guid.NewGuid(), Role.Warden);

        owner.ChangeRole(Role.Cook).IsFailure.Should().BeTrue();
        warden.ChangeRole(Role.Owner).IsFailure.Should().BeTrue();
        warden.ChangeRole(Role.Cook).IsSuccess.Should().BeTrue();
        warden.Role.Should().Be(Role.Cook);
    }

    [Fact]
    public async Task The_membership_handler_allows_only_the_users_own_tenant()
    {
        var membership = new TenantMembership(Guid.NewGuid(), Role.Warden);
        // TenantId is stamped by the interceptor in real use; set it here the way EF would.
        typeof(TenantMembership).GetProperty(nameof(TenantMembership.TenantId))!.SetValue(membership, Guid.NewGuid());
        var handler = new TenantMembershipHandler();

        (await Authorize(handler, membership, membership.TenantId)).Should().BeTrue();
        (await Authorize(handler, membership, Guid.NewGuid())).Should().BeFalse();
        (await Authorize(handler, membership, tenantClaim: null)).Should().BeFalse();
    }

    private static async Task<bool> Authorize(IAuthorizationHandler handler, object resource, Guid? tenantClaim)
    {
        var claims = tenantClaim is null ? [] : new[] { new Claim(BaseraClaimTypes.TenantId, tenantClaim.Value.ToString()) };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        var context = new AuthorizationHandlerContext([TenantMemberRequirement.Instance], user, resource);
        await handler.HandleAsync(context);
        return context.HasSucceeded;
    }
}
