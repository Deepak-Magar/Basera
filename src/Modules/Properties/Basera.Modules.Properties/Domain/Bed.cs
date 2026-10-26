using Basera.Shared.Domain;
using Basera.Shared.Results;

namespace Basera.Modules.Properties.Domain;

public enum BedStatus
{
    Free,
    Reserved,
    Occupied,
    Maintenance,
}

/// <summary>
/// One bed in a room, with the code people read (<c>B{floor}-{room}-{slot}</c>, ARCHITECTURE §6), its status,
/// an optional monthly rate that overrides the rate plan, and its occupancy history. Changed only through
/// <see cref="Property"/>, which owns the rules across beds (unique codes, bed counts).
/// </summary>
public sealed class Bed : Entity, ITenantOwned, IAuditable, ISoftDeletable
{
    private readonly List<BedOccupancy> occupancies = [];

    internal Bed(Guid propertyId, Guid roomId, int slot, string code)
    {
        PropertyId = propertyId;
        RoomId = roomId;
        Slot = slot;
        Code = code;
        Status = BedStatus.Free;
    }

    // For EF Core.
    private Bed()
    {
        Code = "";
    }

    public Guid TenantId { get; private set; }

    public Guid PropertyId { get; private set; }

    public Guid RoomId { get; private set; }

    /// <summary>1 to the room's bed count.</summary>
    public int Slot { get; private set; }

    /// <summary>"B1-101-2": floor 1, room 101, second bed. Unique among a property's beds.</summary>
    public string Code { get; private set; }

    /// <summary>The stored status. A reservation that has run out still says Reserved here; use <see cref="StatusOn"/>.</summary>
    public BedStatus Status { get; private set; }

    /// <summary>The first day the bed is free again if nobody moves in (exclusive end of the reservation).</summary>
    public DateOnly? ReservedUntil { get; private set; }

    /// <summary>The stay the bed is held for, when the reservation came from a check-in; null for a manual hold.</summary>
    public Guid? ReservedFor { get; private set; }

    /// <summary>A monthly rate for this bed only, used instead of the rate plan.</summary>
    public Money? RateOverride { get; private set; }

    /// <summary>PostgreSQL <c>xmin</c>: two people changing the same bed at once can't both win.</summary>
    public uint Version { get; private set; }

    /// <summary>Loaded occupancies; the repository loads only the open one, the history is queried separately.</summary>
    public IReadOnlyList<BedOccupancy> Occupancies => occupancies;

    /// <summary>The stay in the bed now, if any.</summary>
    public BedOccupancy? CurrentOccupancy => occupancies.FirstOrDefault(o => o.IsOpen);

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedBy { get; private set; }

    /// <summary>The status as people should see it on <paramref name="today"/>: a reservation that has run out is Free.</summary>
    public BedStatus StatusOn(DateOnly today) => EffectiveStatus(Status, ReservedUntil, today);

    public bool IsReservationExpired(DateOnly today) => Status == BedStatus.Reserved && ReservedUntil <= today;

    /// <summary><see cref="StatusOn"/> for a bed read as plain columns (lists, the floor map) rather than loaded whole.</summary>
    public static BedStatus EffectiveStatus(BedStatus status, DateOnly? reservedUntil, DateOnly today) =>
        status == BedStatus.Reserved && reservedUntil <= today ? BedStatus.Free : status;

    /// <summary>A bed can go (smaller room, removed room) only when nobody is in it or holding it.</summary>
    public bool CanBeRemoved(DateOnly today) =>
        StatusOn(today) is BedStatus.Free or BedStatus.Maintenance && CurrentOccupancy is null;

    internal void Recode(string code) => Code = code;

    internal Result Reserve(DateOnly today, int days, Guid? stayId)
    {
        var status = StatusOn(today);
        if (status != BedStatus.Free)
        {
            return Refuse(status, "reserved");
        }

        Status = BedStatus.Reserved;
        ReservedUntil = today.AddDays(days);
        ReservedFor = stayId;
        return Result.Success();
    }

    internal Result Release()
    {
        if (Status != BedStatus.Reserved)
        {
            return Result.Failure(DomainError.RuleViolation($"Bed {Code} is not reserved."));
        }

        ClearReservation();
        Status = BedStatus.Free;
        return Result.Success();
    }

    /// <summary>
    /// A stay moves in from <paramref name="from"/>. The bed must be free, or reserved for this stay (or held
    /// without a stay), and have no open occupancy: one active stay per bed (PRODUCT_SPEC §6 rule 6).
    /// </summary>
    internal Result<BedOccupancy> Occupy(Guid stayId, DateOnly from, DateOnly today)
    {
        if (CurrentOccupancy is not null)
        {
            return DomainError.RuleViolation($"Bed {Code} already has a resident.");
        }

        var status = StatusOn(today);
        if (status == BedStatus.Reserved && ReservedFor is { } holder && holder != stayId)
        {
            return DomainError.RuleViolation($"Bed {Code} is reserved for someone else until {ReservedUntil:yyyy-MM-dd}.");
        }

        if (status is not (BedStatus.Free or BedStatus.Reserved))
        {
            return Refuse(status, "occupied");
        }

        var occupancy = new BedOccupancy(Id, stayId, from);
        occupancies.Add(occupancy);
        ClearReservation();
        Status = BedStatus.Occupied;
        return occupancy;
    }

    /// <summary>The stay leaves on <paramref name="on"/> (check-out day, not counted). The bed is free again.</summary>
    internal Result<BedOccupancy> Vacate(Guid stayId, DateOnly on)
    {
        if (CurrentOccupancy is not { } occupancy || occupancy.StayId != stayId)
        {
            return DomainError.RuleViolation($"That stay is not in bed {Code}.");
        }

        if (on < occupancy.From)
        {
            return DomainError.Validation("The check-out date can't be before the check-in date.", "on");
        }

        occupancy.Close(on);
        Status = BedStatus.Free;
        return occupancy;
    }

    internal Result MarkMaintenance(DateOnly today)
    {
        var status = StatusOn(today);
        if (status != BedStatus.Free)
        {
            return Refuse(status, "taken out for maintenance");
        }

        ClearReservation();
        Status = BedStatus.Maintenance;
        return Result.Success();
    }

    internal Result ReturnToService()
    {
        if (Status != BedStatus.Maintenance)
        {
            return Result.Failure(DomainError.RuleViolation($"Bed {Code} is not under maintenance."));
        }

        Status = BedStatus.Free;
        return Result.Success();
    }

    internal Result SetRateOverride(Money? rate)
    {
        if (rate is { } value && value <= Money.Zero)
        {
            return Result.Failure(DomainError.Validation("A bed's rate must be more than zero.", "rateOverride"));
        }

        RateOverride = rate;
        return Result.Success();
    }

    private void ClearReservation()
    {
        ReservedUntil = null;
        ReservedFor = null;
    }

    private Result<BedOccupancy> Refuse(BedStatus status, string action) =>
        DomainError.RuleViolation($"Bed {Code} is {status.ToString().ToLowerInvariant()} and can't be {action}.");
}
