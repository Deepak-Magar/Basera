using Basera.Shared.Domain;
using Basera.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Basera.Shared.Persistence;

/// <summary>
/// The write side of tenancy: stamps the current tenant on inserted <see cref="ITenantOwned"/> rows and
/// refuses to save a row that belongs to, or is being moved to, any other tenant.
/// </summary>
public sealed class TenantInterceptor(ITenantContext tenantContext) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Apply(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<ITenantOwned>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            var tenantId = tenantContext.RequiredTenantId;
            var property = entry.Property(nameof(ITenantOwned.TenantId));

            if (entry.State == EntityState.Added)
            {
                if ((Guid)property.CurrentValue! == Guid.Empty)
                {
                    property.CurrentValue = tenantId;
                }
                else if ((Guid)property.CurrentValue! != tenantId)
                {
                    throw CrossTenantWrite(entry.Metadata.ClrType, (Guid)property.CurrentValue!, tenantId);
                }

                continue;
            }

            // Modified or Deleted: both the row we loaded and any new value must be the current tenant's.
            var original = (Guid)property.OriginalValue!;
            var current = (Guid)property.CurrentValue!;
            if (original != tenantId || current != tenantId)
            {
                throw CrossTenantWrite(entry.Metadata.ClrType, original != tenantId ? original : current, tenantId);
            }
        }
    }

    private static InvalidOperationException CrossTenantWrite(Type type, Guid rowTenant, Guid currentTenant) =>
        new($"Refusing to save a {type.Name} for tenant {rowTenant} while acting for tenant {currentTenant}.");
}
