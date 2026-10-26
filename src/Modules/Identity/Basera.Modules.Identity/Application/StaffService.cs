using Basera.Modules.Identity.Contracts;
using Basera.Modules.Identity.Domain;
using Basera.Modules.Identity.Infrastructure;
using Basera.Shared.Domain;
using Basera.Shared.Results;
using Basera.Shared.Time;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Basera.Modules.Identity.Application;

public sealed record StaffMember(Guid MembershipId, string Name, PhoneNumber Phone, Role Role, bool CanManage);

public sealed record PendingInvitation(Guid InvitationId, PhoneNumber Phone, Role Role, DateTimeOffset ExpiresAt);

public sealed record StaffOverview(IReadOnlyList<StaffMember> Members, IReadOnlyList<PendingInvitation> Invitations);

/// <summary>
/// The owner's staff list: who has which role, pending invitations, role changes and removal. Tenant-filtered
/// throughout. Changing or removing a membership also changes the user's security stamp, so their cookie
/// stops working at its next check (<see cref="BaseraIdentity.SecurityStampInterval"/>) and a fresh sign-in
/// picks up the new role. A membership that stops being a Manager loses its property assignments.
/// </summary>
public sealed class StaffService(
    IdentityDbContext db, UserManager<ApplicationUser> users, IStaffPropertyAssignments assignments, IClock clock)
{
    public async Task<StaffOverview> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var rows = await db.Memberships
            .Join(db.Users, m => m.UserId, u => u.Id, (m, u) => new { m.Id, u.DisplayName, u.PhoneNumber, m.Role })
            .ToListAsync(cancellationToken);
        var members = rows
            .Where(r => r.Role is not (Role.Resident or Role.Parent))
            .Select(r => new StaffMember(r.Id, r.DisplayName, PhoneNumber.Create(r.PhoneNumber).Value, r.Role,
                StaffInvitation.InvitableRoles.Contains(r.Role)))
            .OrderBy(m => m.Role)
            .ThenBy(m => m.Name, StringComparer.CurrentCulture)
            .ToList();

        var now = clock.UtcNow;
        var invitations = (await db.Invitations
                .Where(i => i.AcceptedAt == null && i.RevokedAt == null && i.ExpiresAt > now)
                .OrderBy(i => i.ExpiresAt)
                .ToListAsync(cancellationToken))
            .Select(i => new PendingInvitation(i.Id, PhoneNumber.Create(i.Phone).Value, i.Role, i.ExpiresAt))
            .ToList();
        return new StaffOverview(members, invitations);
    }

    public Task<TenantMembership?> FindMembershipAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Memberships.SingleOrDefaultAsync(m => m.Id == id, cancellationToken);

    public async Task<string> NameOfAsync(TenantMembership membership, CancellationToken cancellationToken = default) =>
        await db.Users.Where(u => u.Id == membership.UserId).Select(u => u.DisplayName).SingleOrDefaultAsync(cancellationToken) ?? "";

    public async Task<Result> ChangeRoleAsync(TenantMembership membership, Role role, CancellationToken cancellationToken = default)
    {
        var changed = membership.ChangeRole(role);
        if (changed.IsFailure)
        {
            return changed;
        }

        await db.SaveChangesAsync(cancellationToken);
        if (membership.Role != Role.Manager)
        {
            await assignments.SetAsync(membership.Id, [], cancellationToken);
        }

        await SignOutEverywhereAsync(membership.UserId);
        return Result.Success();
    }

    /// <summary>The tenant's properties with this manager's assignments marked; refused for other roles.</summary>
    public async Task<Result<IReadOnlyList<AssignableProperty>>> PropertiesOfAsync(TenantMembership membership, CancellationToken cancellationToken = default) =>
        membership.Role == Role.Manager
            ? Result<IReadOnlyList<AssignableProperty>>.Success(await assignments.ListAsync(membership.Id, cancellationToken))
            : DomainError.RuleViolation("Only managers are assigned to properties.");

    public Task<Result> AssignPropertiesAsync(TenantMembership membership, IReadOnlyCollection<Guid> propertyIds, CancellationToken cancellationToken = default) =>
        membership.Role == Role.Manager
            ? assignments.SetAsync(membership.Id, propertyIds, cancellationToken)
            : Task.FromResult(Result.Failure(DomainError.RuleViolation("Only managers are assigned to properties.")));

    /// <summary>Ends the membership. The user stays (a later invitation to the same phone reuses it) but can't sign in.</summary>
    public async Task<Result> RemoveAsync(TenantMembership membership, CancellationToken cancellationToken = default)
    {
        if (!membership.IsManagedStaff)
        {
            return DomainError.RuleViolation("The owner can't be removed here.");
        }

        db.Memberships.Remove(membership);
        await db.SaveChangesAsync(cancellationToken);
        await assignments.SetAsync(membership.Id, [], cancellationToken);
        await SignOutEverywhereAsync(membership.UserId);
        return Result.Success();
    }

    private async Task SignOutEverywhereAsync(Guid userId)
    {
        if (await users.FindByIdAsync(userId.ToString()) is { } user)
        {
            await users.UpdateSecurityStampAsync(user);
        }
    }
}
