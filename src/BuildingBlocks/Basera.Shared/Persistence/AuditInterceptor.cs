using Basera.Shared.Domain;
using Basera.Shared.Time;
using Basera.Shared.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Basera.Shared.Persistence;

/// <summary>
/// Fills <see cref="IAuditable"/> columns and turns deletes of <see cref="ISoftDeletable"/> rows into
/// updates of <c>deleted_at</c>. Created values are written once and never changed afterwards.
/// </summary>
public sealed class AuditInterceptor(IClock clock, ICurrentUser currentUser) : SaveChangesInterceptor
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

        var now = clock.UtcNow;
        var userId = currentUser.UserId;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry is { State: EntityState.Deleted, Entity: ISoftDeletable })
            {
                entry.State = EntityState.Modified;
                Set(entry, nameof(ISoftDeletable.DeletedAt), now);
                Set(entry, nameof(ISoftDeletable.DeletedBy), userId);
            }

            if (entry.Entity is not IAuditable)
            {
                continue;
            }

            switch (entry.State)
            {
                case EntityState.Added:
                    Set(entry, nameof(IAuditable.CreatedAt), now);
                    Set(entry, nameof(IAuditable.CreatedBy), userId);
                    break;
                case EntityState.Modified:
                    entry.Property(nameof(IAuditable.CreatedAt)).IsModified = false;
                    entry.Property(nameof(IAuditable.CreatedBy)).IsModified = false;
                    Set(entry, nameof(IAuditable.UpdatedAt), now);
                    Set(entry, nameof(IAuditable.UpdatedBy), userId);
                    break;
            }
        }
    }

    private static void Set(EntityEntry entry, string property, object? value) => entry.Property(property).CurrentValue = value;
}
