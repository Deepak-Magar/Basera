using Basera.Modules.Identity.Domain;
using Basera.Shared.Users;
using Microsoft.AspNetCore.Authorization;

namespace Basera.Modules.Identity.Infrastructure;

/// <summary>
/// Resource authorization (ARCHITECTURE §7): the signed-in user may manage this membership or invitation
/// only if it is in the user's own tenant. The tenant query filter already hides other tenants' rows; this
/// check doesn't rely on that and states the rule where the decision is made. It is the pattern later
/// handlers follow ("a resident may only see their own invoices").
/// </summary>
public sealed class TenantMemberRequirement : IAuthorizationRequirement
{
    public static readonly TenantMemberRequirement Instance = new();
}

public sealed class TenantMembershipHandler : AuthorizationHandler<TenantMemberRequirement, TenantMembership>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, TenantMemberRequirement requirement, TenantMembership resource)
    {
        if (SameTenant(context, resource.TenantId))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    internal static bool SameTenant(AuthorizationHandlerContext context, Guid resourceTenantId) =>
        Guid.TryParse(context.User.FindFirst(BaseraClaimTypes.TenantId)?.Value, out var tenantId)
        && tenantId != Guid.Empty
        && tenantId == resourceTenantId;
}

public sealed class StaffInvitationHandler : AuthorizationHandler<TenantMemberRequirement, StaffInvitation>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, TenantMemberRequirement requirement, StaffInvitation resource)
    {
        if (TenantMembershipHandler.SameTenant(context, resource.TenantId))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
