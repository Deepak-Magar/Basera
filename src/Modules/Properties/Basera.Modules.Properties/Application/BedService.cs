using Basera.Modules.Properties.Contracts;
using Basera.Modules.Properties.Domain;
using Basera.Modules.Properties.Infrastructure;
using Basera.Shared.Domain;
using Basera.Shared.Results;
using Basera.Shared.Time;
using Microsoft.EntityFrameworkCore;

namespace Basera.Modules.Properties.Application;

/// <summary>One stay segment in a bed: who (the Residents stay) and when.</summary>
public sealed record BedHistoryEntry(Guid StayId, DateOnly From, DateOnly? To);

/// <summary>
/// One bed as the floor map's panel shows it: where it is, its status today, its rate (or why there is none)
/// and who has been in it.
/// </summary>
public sealed record BedDetails(
    Guid BedId,
    Guid PropertyId,
    string PropertyName,
    string Code,
    int FloorNumber,
    string RoomNumber,
    RoomType RoomType,
    BedStatus Status,
    DateOnly? ReservedUntil,
    bool ReservedForStay,
    Money? RateOverride,
    Money? MonthlyRate,
    string? RateProblem,
    IReadOnlyList<BedHistoryEntry> History);

/// <summary>
/// Bed status changes (reserve, release, maintenance, move in, move out), rate overrides and the bed's
/// details with history. Each change loads the bed's property and goes through its rules.
/// </summary>
public sealed class BedService(PropertiesDbContext db, PropertyStore store, IClock clock)
{
    public Task<Guid?> PropertyIdOfAsync(Guid bedId, CancellationToken cancellationToken = default) =>
        store.PropertyIdOfBedAsync(bedId, cancellationToken);

    public async Task<BedDetails?> GetAsync(Guid bedId, CancellationToken cancellationToken = default)
    {
        if (await store.PropertyIdOfBedAsync(bedId, cancellationToken) is not { } propertyId
            || await store.LoadAsync(propertyId, cancellationToken) is not { } property
            || property.FindBed(bedId) is not { } bed
            || property.RoomOf(bed) is not { } room)
        {
            return null;
        }

        var today = clock.Today;
        var rate = property.MonthlyRate(bedId, today);
        var history = await db.Occupancies.AsNoTracking()
            .Where(o => o.BedId == bedId)
            .OrderByDescending(o => o.From)
            .Select(o => new BedHistoryEntry(o.StayId, o.From, o.To))
            .ToListAsync(cancellationToken);

        var status = bed.StatusOn(today);
        return new BedDetails(
            bed.Id, property.Id, property.Name, bed.Code, room.FloorNumber, room.Number, room.Type, status,
            status == BedStatus.Reserved ? bed.ReservedUntil : null,
            status == BedStatus.Reserved && bed.ReservedFor is not null,
            bed.RateOverride,
            rate.IsSuccess ? rate.Value : null,
            rate.IsFailure ? rate.Error!.Message : null,
            history);
    }

    /// <summary>Free beds of an active property today, by code, with their rates.</summary>
    public async Task<IReadOnlyList<FreeBed>> FreeBedsAsync(Guid propertyId, RoomType? type = null, CancellationToken cancellationToken = default)
    {
        if (await store.LoadAsync(propertyId, cancellationToken) is not { IsActive: true } property)
        {
            return [];
        }

        var today = clock.Today;
        return property.Rooms
            .Where(room => type is null || room.Type == type)
            .SelectMany(room => room.Beds.Where(bed => bed.StatusOn(today) == BedStatus.Free).Select(bed => (room, bed)))
            .Select(x =>
            {
                var rate = property.MonthlyRate(x.bed.Id, today);
                return new FreeBed(x.bed.Id, property.Id, x.bed.Code, x.room.FloorNumber, x.room.Number, x.room.Type,
                    rate.IsSuccess ? rate.Value : null);
            })
            .ToList();
    }

    public Task<Result> ReserveAsync(Guid bedId, Guid? stayId = null, CancellationToken cancellationToken = default) =>
        store.ChangeBedAsync(bedId, property => property.Reserve(bedId, clock.Today, stayId), cancellationToken);

    public Task<Result> ReleaseAsync(Guid bedId, CancellationToken cancellationToken = default) =>
        store.ChangeBedAsync(bedId, property => property.Release(bedId), cancellationToken);

    public Task<Result> MarkMaintenanceAsync(Guid bedId, CancellationToken cancellationToken = default) =>
        store.ChangeBedAsync(bedId, property => property.MarkMaintenance(bedId, clock.Today), cancellationToken);

    public Task<Result> ReturnToServiceAsync(Guid bedId, CancellationToken cancellationToken = default) =>
        store.ChangeBedAsync(bedId, property => property.ReturnToService(bedId), cancellationToken);

    /// <summary>Sets or (with null) clears the bed's own monthly rate.</summary>
    public Task<Result> SetRateAsync(Guid bedId, Money? rate, CancellationToken cancellationToken = default) =>
        store.ChangeBedAsync(bedId, property => property.SetBedRate(bedId, rate), cancellationToken);

    public Task<Result> OccupyAsync(Guid bedId, Guid stayId, DateOnly from, CancellationToken cancellationToken = default) =>
        store.ChangeBedAsync(bedId, property => property.Occupy(bedId, stayId, from, clock.Today), cancellationToken);

    public Task<Result> VacateAsync(Guid bedId, Guid stayId, DateOnly on, CancellationToken cancellationToken = default) =>
        store.ChangeBedAsync(bedId, property => property.Vacate(bedId, stayId, on), cancellationToken);

    public async Task<Result<Money>> MonthlyRateAsync(Guid bedId, DateOnly on, CancellationToken cancellationToken = default)
    {
        if (await store.PropertyIdOfBedAsync(bedId, cancellationToken) is not { } propertyId
            || await store.LoadAsync(propertyId, cancellationToken) is not { } property)
        {
            return PropertyStore.BedNotFound;
        }

        return property.MonthlyRate(bedId, on);
    }
}
