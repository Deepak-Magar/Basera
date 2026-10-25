using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
// Alias rather than a using: that namespace has its own IdentityDbContext, which would clash with ours.
using EfUserStore = Microsoft.AspNetCore.Identity.EntityFrameworkCore.UserOnlyStore<
    Basera.Modules.Identity.Infrastructure.ApplicationUser, Basera.Modules.Identity.Infrastructure.IdentityDbContext, System.Guid>;

namespace Basera.Modules.Identity.Infrastructure;

internal static class IdentityStoreExtensions
{
    /// <summary>
    /// Stops <c>UserManager.CreateAsync</c> from saving the user by itself, for the rest of this scope, so the
    /// caller saves the user together with its tenant and membership in one <c>SaveChanges</c>.
    /// </summary>
    public static void HoldChanges(this IUserStore<ApplicationUser> store)
    {
        var efStore = store as EfUserStore
            ?? throw new InvalidOperationException($"Expected the EF user store, found {store.GetType().Name}.");
        efStore.AutoSaveChanges = false;
    }

    /// <summary>A unique index refused the insert: the phone or email was taken by a concurrent request.</summary>
    public static bool IsUniqueViolation(this DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
