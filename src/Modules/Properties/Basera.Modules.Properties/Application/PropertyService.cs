using Basera.Modules.Properties.Domain;
using Basera.Modules.Properties.Infrastructure;
using Basera.Shared.Results;
using Basera.Shared.Time;
using Microsoft.EntityFrameworkCore;

namespace Basera.Modules.Properties.Application;

/// <summary>A property in the owner's list, with how its beds stand today.</summary>
public sealed record PropertyListItem(Guid Id, string Name, string Address, bool IsActive, int Beds, int FreeBeds, int OccupiedBeds);

/// <summary>Everything the owner edits about a property itself.</summary>
public sealed record PropertySettings(PropertyDetails Details, int ReservationDays, bool IsActive);

/// <summary>Create, list, edit and remove properties (PRODUCT_SPEC §4.2). Tenant-filtered throughout.</summary>
public sealed class PropertyService(PropertiesDbContext db, PropertyStore store, IClock clock)
{
    public async Task<IReadOnlyList<PropertyListItem>> ListAsync(IReadOnlyCollection<Guid>? onlyIds = null, CancellationToken cancellationToken = default)
    {
        var properties = await db.Properties.AsNoTracking()
            .Where(p => onlyIds == null || onlyIds.Contains(p.Id))
            .OrderBy(p => p.Name)
            .Select(p => new { p.Id, p.Name, p.Address, p.IsActive })
            .ToListAsync(cancellationToken);
        var ids = properties.Select(p => p.Id).ToList();
        var beds = await db.Beds.AsNoTracking()
            .Where(b => ids.Contains(b.PropertyId))
            .Select(b => new { b.PropertyId, b.Status, b.ReservedUntil })
            .ToListAsync(cancellationToken);

        var today = clock.Today;
        return properties.Select(p =>
        {
            var statuses = beds.Where(b => b.PropertyId == p.Id).Select(b => Bed.EffectiveStatus(b.Status, b.ReservedUntil, today)).ToList();
            return new PropertyListItem(p.Id, p.Name, p.Address, p.IsActive, statuses.Count,
                statuses.Count(s => s == BedStatus.Free), statuses.Count(s => s == BedStatus.Occupied));
        }).ToList();
    }

    /// <summary>The whole aggregate, for pages that show rooms, beds and plans.</summary>
    public Task<Property?> FindAsync(Guid propertyId, CancellationToken cancellationToken = default) =>
        store.LoadAsync(propertyId, cancellationToken);

    public async Task<Result<Guid>> CreateAsync(PropertySettings settings, CancellationToken cancellationToken = default)
    {
        var created = Property.Create(settings.Details);
        if (created.IsFailure)
        {
            return created.Error!;
        }

        var property = created.Value;
        var applied = Apply(property, settings);
        if (applied.IsFailure)
        {
            return applied.Error!;
        }

        db.Properties.Add(property);
        var saved = await store.SaveAsync(cancellationToken);
        return saved.IsSuccess ? property.Id : saved.Error!;
    }

    public Task<Result> UpdateAsync(Guid propertyId, PropertySettings settings, CancellationToken cancellationToken = default) =>
        store.ChangeAsync(propertyId, property =>
        {
            var updated = property.UpdateDetails(settings.Details);
            return updated.IsFailure ? updated : Apply(property, settings);
        }, cancellationToken);

    /// <summary>Soft-deletes the property with its rooms and beds; refused while any bed is occupied or held.</summary>
    public async Task<Result> RemoveAsync(Guid propertyId, CancellationToken cancellationToken = default)
    {
        if (await store.LoadAsync(propertyId, cancellationToken) is not { } property)
        {
            return PropertyStore.PropertyNotFound;
        }

        var removable = property.CanBeRemoved(clock.Today);
        if (removable.IsFailure)
        {
            return removable;
        }

        db.Properties.Remove(property);
        return await store.SaveAsync(cancellationToken);
    }

    public Task<Result> SetPaymentQrAsync(Guid propertyId, string? fileKey, CancellationToken cancellationToken = default) =>
        store.ChangeAsync(propertyId, property =>
        {
            property.SetPaymentQr(fileKey);
            return Result.Success();
        }, cancellationToken);

    public Task<Result> SetLogoAsync(Guid propertyId, string? fileKey, CancellationToken cancellationToken = default) =>
        store.ChangeAsync(propertyId, property =>
        {
            property.SetLogo(fileKey);
            return Result.Success();
        }, cancellationToken);

    private static Result Apply(Property property, PropertySettings settings)
    {
        var days = property.SetReservationDays(settings.ReservationDays);
        if (days.IsFailure)
        {
            return days;
        }

        if (settings.IsActive)
        {
            property.Activate();
        }
        else
        {
            property.Deactivate();
        }

        return Result.Success();
    }
}
