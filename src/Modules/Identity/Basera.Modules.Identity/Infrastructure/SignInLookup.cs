using Basera.Shared.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Basera.Modules.Identity.Infrastructure;

/// <summary>The user a login names, and the tenant to act for while signing them in.</summary>
public sealed record SignInCandidate(Guid UserId, Guid TenantId);

/// <summary>
/// Finds which user (and so which tenant) an email or phone number belongs to, before any tenant is known.
/// This is the one place in Identity that reads users with <c>IgnoreQueryFilters()</c>: on the login page
/// nobody is signed in, so there is no tenant to filter by, and the filter would throw. It returns only
/// ids. The caller sets the tenant context to <see cref="SignInCandidate.TenantId"/> and then loads the
/// user through <c>UserManager</c>, so the password check, lockout counters and everything after run
/// tenant-filtered as usual.
/// </summary>
public sealed class SignInLookup(IdentityDbContext db, ILookupNormalizer normalizer)
{
    /// <summary><paramref name="login"/> is an email address (contains "@") or a Nepal mobile number in any common form.</summary>
    public async Task<SignInCandidate?> FindAsync(string? login, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(login))
        {
            return null;
        }

        var users = db.Users.IgnoreQueryFilters().AsNoTracking();
        if (login.Contains('@', StringComparison.Ordinal))
        {
            var email = normalizer.NormalizeEmail(login.Trim());
            users = users.Where(user => user.NormalizedEmail == email);
        }
        else
        {
            var phone = PhoneNumber.Create(login);
            if (phone.IsFailure)
            {
                return null;
            }

            var userName = normalizer.NormalizeName(phone.Value.E164);
            users = users.Where(user => user.NormalizedUserName == userName);
        }

        // Unique indexes on both columns: at most one row.
        return await users
            .Select(user => new SignInCandidate(user.Id, user.TenantId))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
