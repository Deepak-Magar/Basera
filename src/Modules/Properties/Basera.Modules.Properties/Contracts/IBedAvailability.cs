using Basera.Shared.Domain;
using Basera.Shared.Results;

namespace Basera.Modules.Properties.Contracts;

/// <summary>A bed nobody is in or holding on the day asked about, with its monthly rate when one is set.</summary>
public sealed record FreeBed(
    Guid BedId, Guid PropertyId, string Code, int FloorNumber, string RoomNumber, RoomType RoomType, Money? MonthlyRate);

/// <summary>
/// What other modules (Residents' check-in and check-out, the waiting list) may do with beds. Every change goes
/// through the Property aggregate, so its rules hold: one active stay per bed (PRODUCT_SPEC §6 rule 6),
/// reservations run out after the property's reservation days, and each move in or out is kept as
/// occupancy history. Acts for the current tenant; another tenant's bed is not found. Expected refusals
/// (bed taken, stale edit) come back as failed results, never exceptions.
/// </summary>
public interface IBedAvailability
{
    /// <summary>Free beds of an active property today, by code; all types when <paramref name="type"/> is null.</summary>
    Task<IReadOnlyList<FreeBed>> FreeBedsAsync(Guid propertyId, RoomType? type = null, CancellationToken cancellationToken = default);

    /// <summary>Holds a free bed for <paramref name="stayId"/> (or by hand when null) for the property's reservation days.</summary>
    Task<Result> ReserveAsync(Guid bedId, Guid? stayId, CancellationToken cancellationToken = default);

    Task<Result> ReleaseAsync(Guid bedId, CancellationToken cancellationToken = default);

    /// <summary>The stay moves in on <paramref name="from"/> (check-in day); opens its occupancy record.</summary>
    Task<Result> OccupyAsync(Guid bedId, Guid stayId, DateOnly from, CancellationToken cancellationToken = default);

    /// <summary>The stay leaves on <paramref name="on"/> (check-out day); closes its occupancy record and frees the bed.</summary>
    Task<Result> VacateAsync(Guid bedId, Guid stayId, DateOnly on, CancellationToken cancellationToken = default);
}
