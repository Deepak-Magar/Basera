using Basera.Modules.Identity.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Basera.Modules.Identity.Infrastructure;

/// <summary><see cref="ITenantDirectory"/> over <c>identity.tenant</c>, cached for five minutes per tenant.</summary>
internal sealed class TenantDirectory(IdentityDbContext db, IMemoryCache cache) : ITenantDirectory
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(5);

    public async Task<TenantSummary?> FindAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var key = $"identity:tenant:{tenantId}";
        if (cache.TryGetValue(key, out TenantSummary? cached))
        {
            return cached;
        }

        var summary = await db.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => new TenantSummary(t.Id, t.Name))
            .SingleOrDefaultAsync(cancellationToken);
        if (summary is not null)
        {
            cache.Set(key, summary, CacheFor);
        }

        return summary;
    }
}
