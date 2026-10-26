using Basera.Modules.Identity.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Basera.Modules.Identity.Infrastructure;

/// <summary><see cref="ITenantMembers"/> over <c>identity.tenant_membership</c> and the user's name, tenant-filtered.</summary>
internal sealed class TenantMembers(IdentityDbContext db) : ITenantMembers
{
    public Task<MemberSummary?> FindByUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        db.Memberships.AsNoTracking()
            .Where(m => m.UserId == userId)
            .Join(db.Users, m => m.UserId, u => u.Id, (m, u) => new MemberSummary(m.Id, m.UserId, u.DisplayName, m.Role.ToString()))
            .SingleOrDefaultAsync(cancellationToken);
}
