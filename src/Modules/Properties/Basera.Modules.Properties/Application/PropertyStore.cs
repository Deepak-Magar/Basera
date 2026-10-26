using Basera.Modules.Properties.Domain;
using Basera.Modules.Properties.Infrastructure;
using Basera.Shared.Results;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Basera.Modules.Properties.Application;

/// <summary>
/// Loads a <see cref="Property"/> aggregate, lets a service apply one change through it, and saves. A stale
/// edit (<c>xmin</c>) or a lost race on a unique index (two people filling one bed, two rooms named alike)
/// becomes a Conflict result the page can show, not an exception. Every service goes through here, so they
/// all load the same way and fail the same way.
/// </summary>
public sealed class PropertyStore(PropertiesDbContext db)
{
    public static readonly DomainError PropertyNotFound = DomainError.NotFound("That property doesn't exist.");
    public static readonly DomainError BedNotFound = DomainError.NotFound("That bed doesn't exist.");

    private static readonly DomainError Stale =
        DomainError.Conflict("Someone else changed this at the same time. Reload the page and try again.");

    public Task<Property?> LoadAsync(Guid propertyId, CancellationToken cancellationToken = default) =>
        db.Properties.WithAggregate().SingleOrDefaultAsync(p => p.Id == propertyId, cancellationToken);

    /// <summary>The property a bed belongs to; null for an unknown (or another tenant's) bed.</summary>
    public Task<Guid?> PropertyIdOfBedAsync(Guid bedId, CancellationToken cancellationToken = default) =>
        db.Beds.Where(b => b.Id == bedId).Select(b => (Guid?)b.PropertyId).SingleOrDefaultAsync(cancellationToken);

    public async Task<Result<T>> ChangeAsync<T>(Guid propertyId, Func<Property, Result<T>> change, CancellationToken cancellationToken = default)
    {
        if (await LoadAsync(propertyId, cancellationToken) is not { } property)
        {
            return PropertyNotFound;
        }

        var result = change(property);
        if (result.IsFailure)
        {
            return result;
        }

        var saved = await SaveAsync(cancellationToken);
        return saved.IsSuccess ? result : saved.Error!;
    }

    public async Task<Result> ChangeAsync(Guid propertyId, Func<Property, Result> change, CancellationToken cancellationToken = default) =>
        await ChangeAsync(propertyId, property => change(property) is { IsFailure: true } failed ? failed.Error! : Result.Success(true), cancellationToken);

    /// <summary>Loads the bed's property and applies <paramref name="change"/> to it.</summary>
    public async Task<Result> ChangeBedAsync(Guid bedId, Func<Property, Result> change, CancellationToken cancellationToken = default) =>
        await PropertyIdOfBedAsync(bedId, cancellationToken) is { } propertyId
            ? await ChangeAsync(propertyId, change, cancellationToken)
            : BedNotFound;

    public async Task<Result> SaveAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Stale;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Stale;
        }
    }
}
