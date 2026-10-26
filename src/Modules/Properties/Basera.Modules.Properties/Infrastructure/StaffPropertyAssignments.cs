using Basera.Modules.Identity.Contracts;
using Basera.Modules.Properties.Domain;
using Basera.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace Basera.Modules.Properties.Infrastructure;

/// <summary>
/// Answers Identity's <see cref="IStaffPropertyAssignments"/> from <c>properties.manager_property_assignment</c>.
/// Registered with <c>services.Replace</c>, because Identity (added first) registered its stub with <c>TryAdd</c>.
/// </summary>
internal sealed class StaffPropertyAssignments(PropertiesDbContext db) : IStaffPropertyAssignments
{
    public async Task<IReadOnlyList<AssignableProperty>> ListAsync(Guid membershipId, CancellationToken cancellationToken = default)
    {
        var assigned = await db.ManagerAssignments
            .Where(a => a.MembershipId == membershipId)
            .Select(a => a.PropertyId)
            .ToListAsync(cancellationToken);
        return await db.Properties.AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new AssignableProperty(p.Id, p.Name, assigned.Contains(p.Id)))
            .ToListAsync(cancellationToken);
    }

    public async Task<Result> SetAsync(Guid membershipId, IReadOnlyCollection<Guid> propertyIds, CancellationToken cancellationToken = default)
    {
        var wanted = propertyIds.Distinct().ToList();
        var known = await db.Properties.CountAsync(p => wanted.Contains(p.Id), cancellationToken);
        if (known != wanted.Count)
        {
            return Result.Failure(DomainError.NotFound("That property doesn't exist."));
        }

        var current = await db.ManagerAssignments.Where(a => a.MembershipId == membershipId).ToListAsync(cancellationToken);
        db.ManagerAssignments.RemoveRange(current.Where(a => !wanted.Contains(a.PropertyId)));
        db.ManagerAssignments.AddRange(wanted
            .Where(id => current.All(a => a.PropertyId != id))
            .Select(id => new ManagerPropertyAssignment(membershipId, id)));
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
