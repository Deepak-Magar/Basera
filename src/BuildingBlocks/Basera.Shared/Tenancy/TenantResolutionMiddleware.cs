using Basera.Shared.Users;
using Microsoft.AspNetCore.Http;

namespace Basera.Shared.Tenancy;

/// <summary>
/// Copies the signed-in user's <c>tenant_id</c> claim into <see cref="TenantContext"/>. Runs after
/// authentication (ARCHITECTURE §4). Anonymous requests, and users without the claim, stay unresolved,
/// so any tenant-owned query they reach throws instead of returning another tenant's rows.
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context, TenantContext tenantContext)
    {
        var claim = context.User.FindFirst(BaseraClaimTypes.TenantId)?.Value;
        if (context.User.Identity?.IsAuthenticated == true && Guid.TryParse(claim, out var tenantId) && tenantId != Guid.Empty)
        {
            tenantContext.Set(tenantId);
        }

        return next(context);
    }
}
