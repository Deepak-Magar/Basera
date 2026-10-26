using Basera.Modules.Properties.Application;
using Basera.Shared.Authorization;
using Basera.Shared.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Basera.Modules.Properties.Infrastructure;

/// <summary>A property as an authorization resource: its id and the tenant it belongs to.</summary>
public sealed record PropertyRef(Guid Id, Guid TenantId);

/// <summary>
/// Resource authorization for every Properties action (ARCHITECTURE §7, PRODUCT_SPEC §6 rule 8): may this user
/// <see cref="View"/> or <see cref="Manage"/> this property? Checked after the tenant-filtered lookup, and like
/// Identity's <c>TenantMemberRequirement</c> it states the tenant rule again rather than relying on the filter.
/// </summary>
public sealed class PropertyAccessRequirement : IAuthorizationRequirement
{
    /// <summary>See the property, its floor map and beds.</summary>
    public static readonly PropertyAccessRequirement View = new(manage: false);

    /// <summary>Change the property, its rooms, rates and beds.</summary>
    public static readonly PropertyAccessRequirement Manage = new(manage: true);

    private PropertyAccessRequirement(bool manage) => IsManage = manage;

    public bool IsManage { get; }
}

/// <summary>
/// Owner: every property of the tenant. Manager: assigned properties only, to view and to manage.
/// Warden and Cook: view every property (the staff floor map), manage none. Others: nothing.
/// A singleton like Identity's handlers, so <c>IAuthorizationService</c> stays resolvable outside a scope; the
/// scoped <see cref="PropertyAccess"/> (it reads assignments through the DbContext) comes from the request.
/// Without a request there is no signed-in user to authorize, so access is refused.
/// </summary>
public sealed class PropertyAccessHandler(IHttpContextAccessor http) : AuthorizationHandler<PropertyAccessRequirement, PropertyRef>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PropertyAccessRequirement requirement, PropertyRef resource)
    {
        if (!Guid.TryParse(context.User.FindFirst(BaseraClaimTypes.TenantId)?.Value, out var tenantId)
            || tenantId == Guid.Empty
            || tenantId != resource.TenantId)
        {
            return;
        }

        var allowed = context.User.FindFirst(BaseraClaimTypes.Role)?.Value switch
        {
            BaseraRoles.Owner => true,
            BaseraRoles.Manager => http.HttpContext?.RequestServices.GetRequiredService<PropertyAccess>() is { } access
                && (await access.AssignedToAsync(context.User)).Contains(resource.Id),
            BaseraRoles.Warden or BaseraRoles.Cook => !requirement.IsManage,
            _ => false,
        };

        if (allowed)
        {
            context.Succeed(requirement);
        }
    }
}
