using Basera.Modules.Identity.Contracts;
using Basera.Shared.Results;

namespace Basera.Modules.Identity.Infrastructure;

/// <summary>
/// Stub for when no module provides properties: there is nothing to assign. Clearing succeeds, so removing a
/// manager works either way.
/// </summary>
internal sealed class NoStaffPropertyAssignments : IStaffPropertyAssignments
{
    public Task<IReadOnlyList<AssignableProperty>> ListAsync(Guid membershipId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AssignableProperty>>([]);

    public Task<Result> SetAsync(Guid membershipId, IReadOnlyCollection<Guid> propertyIds, CancellationToken cancellationToken = default) =>
        Task.FromResult(propertyIds.Count == 0
            ? Result.Success()
            : Result.Failure(DomainError.NotFound("That property doesn't exist.")));
}
