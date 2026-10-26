using System.Security.Claims;
using Basera.Modules.Identity.Contracts;
using Basera.Modules.Properties.Infrastructure;
using Basera.Shared.Authorization;
using Basera.Shared.Users;
using Microsoft.EntityFrameworkCore;

namespace Basera.Modules.Properties.Application;

/// <summary>
/// Which properties a signed-in staff member may see (PRODUCT_SPEC §6 rule 8). The Owner sees all of them; a
/// Manager only those assigned on the owner's staff page; Wardens and Cooks see all of them read-only (their
/// assignment is not part of this session). Used to filter lists and by <see cref="PropertyAccessHandler"/>.
/// </summary>
public sealed class PropertyAccess(PropertiesDbContext db, ITenantMembers members)
{
    private IReadOnlyCollection<Guid>? assigned;

    /// <summary>Null means every property of the tenant.</summary>
    public async Task<IReadOnlyCollection<Guid>?> VisiblePropertyIdsAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default) =>
        user.FindFirst(BaseraClaimTypes.Role)?.Value == BaseraRoles.Manager
            ? await AssignedToAsync(user, cancellationToken)
            : null;

    /// <summary>The properties assigned to the user's membership; read once per request.</summary>
    public async Task<IReadOnlyCollection<Guid>> AssignedToAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        if (assigned is not null)
        {
            return assigned;
        }

        var userId = Guid.TryParse(user.FindFirst(BaseraClaimTypes.Subject)?.Value, out var id) ? id : Guid.Empty;
        var member = userId == Guid.Empty ? null : await members.FindByUserAsync(userId, cancellationToken);
        assigned = member is null
            ? []
            : await db.ManagerAssignments.AsNoTracking()
                .Where(a => a.MembershipId == member.MembershipId)
                .Select(a => a.PropertyId)
                .ToListAsync(cancellationToken);
        return assigned;
    }
}
