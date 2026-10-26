using Basera.Shared.Domain;
using Basera.Shared.Domain.Calendar;
using Basera.Shared.Results;

namespace Basera.Modules.Properties.Domain;

/// <summary>What the owner types about a property. The phone is parsed here; a blank one means none.</summary>
public sealed record PropertyDetails(string? Name, string? Address, int? Ward, string? ContactPhone, string? Rules);

/// <summary>
/// A hostel branch (PRODUCT_SPEC §4.2) and the aggregate root of its floors, rooms, beds and rate plans. Every
/// rule that spans more than one bed lives here: a room's beds match its type, bed codes are unique, a bed
/// has one active stay (§6 rule 6), a reservation blocks a bed for <see cref="ReservationDays"/>, and a bed's
/// rate is its override or else the plan in effect on the day.
/// </summary>
public sealed class Property : Entity, ITenantOwned, IAuditable, ISoftDeletable
{
    public const int NameMaxLength = 200;
    public const int AddressMaxLength = 300;
    public const int RulesMaxLength = 4000;
    public const int MaxWard = 40;
    public const int DefaultReservationDays = 7;
    public const int MaxReservationDays = 60;

    private readonly List<Floor> floors = [];
    private readonly List<Room> rooms = [];
    private readonly List<RatePlan> ratePlans = [];

    private Property()
    {
        Name = "";
        Address = "";
        IsActive = true;
        ReservationDays = DefaultReservationDays;
    }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; }

    public string Address { get; private set; }

    /// <summary>Ward number of the municipality, for the ward rent-tax statement.</summary>
    public int? Ward { get; private set; }

    public PhoneNumber? ContactPhone { get; private set; }

    /// <summary>Hostel rules shown to residents.</summary>
    public string? Rules { get; private set; }

    /// <summary>File storage key of the hostel's payment QR image (phase-one payments), never a public URL.</summary>
    public string? PaymentQrPath { get; private set; }

    /// <summary>File storage key of the property's logo.</summary>
    public string? LogoPath { get; private set; }

    /// <summary>An inactive property keeps its data but takes no new residents.</summary>
    public bool IsActive { get; private set; }

    /// <summary>How long a reservation blocks a bed (§6 rule 6, default 7).</summary>
    public int ReservationDays { get; private set; }

    /// <summary>PostgreSQL <c>xmin</c>.</summary>
    public uint Version { get; private set; }

    public IReadOnlyList<Floor> Floors => floors.OrderBy(f => f.Number).ToList();

    public IReadOnlyList<Room> Rooms => rooms.OrderBy(r => r.FloorNumber).ThenBy(r => r.Number, StringComparer.Ordinal).ToList();

    public IReadOnlyList<RatePlan> RatePlans => ratePlans.OrderBy(p => p.EffectiveFrom).ToList();

    public IEnumerable<Bed> Beds => Rooms.SelectMany(r => r.Beds);

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedBy { get; private set; }

    public static Result<Property> Create(PropertyDetails details)
    {
        var property = new Property();
        var updated = property.UpdateDetails(details);
        return updated.IsSuccess ? property : updated.Error!;
    }

    public Result UpdateDetails(PropertyDetails details)
    {
        var name = details.Name?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return Fail(DomainError.Validation("Enter the property's name.", "name"));
        }

        if (name.Length > NameMaxLength)
        {
            return Fail(DomainError.Validation($"The name can be at most {NameMaxLength} characters.", "name"));
        }

        var address = details.Address?.Trim();
        if (string.IsNullOrEmpty(address))
        {
            return Fail(DomainError.Validation("Enter the address.", "address"));
        }

        if (address.Length > AddressMaxLength)
        {
            return Fail(DomainError.Validation($"The address can be at most {AddressMaxLength} characters.", "address"));
        }

        if (details.Ward is < 1 or > MaxWard)
        {
            return Fail(DomainError.Validation($"A ward number is 1 to {MaxWard}.", "ward"));
        }

        PhoneNumber? phone = null;
        if (!string.IsNullOrWhiteSpace(details.ContactPhone))
        {
            var parsed = PhoneNumber.Create(details.ContactPhone);
            if (parsed.IsFailure)
            {
                return Fail(parsed.Error! with { Target = "contactPhone" });
            }

            phone = parsed.Value;
        }

        var rules = string.IsNullOrWhiteSpace(details.Rules) ? null : details.Rules.Trim();
        if (rules is { Length: > RulesMaxLength })
        {
            return Fail(DomainError.Validation($"The rules can be at most {RulesMaxLength} characters.", "rules"));
        }

        Name = name;
        Address = address;
        Ward = details.Ward;
        ContactPhone = phone;
        Rules = rules;
        return Result.Success();
    }

    public Result SetReservationDays(int days)
    {
        if (days is < 1 or > MaxReservationDays)
        {
            return Fail(DomainError.Validation($"A reservation lasts 1 to {MaxReservationDays} days.", "reservationDays"));
        }

        ReservationDays = days;
        return Result.Success();
    }

    public void SetPaymentQr(string? fileKey) => PaymentQrPath = fileKey;

    public void SetLogo(string? fileKey) => LogoPath = fileKey;

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    /// <summary>A property can be removed only when no bed is occupied or held.</summary>
    public Result CanBeRemoved(DateOnly today) =>
        Beds.FirstOrDefault(b => !b.CanBeRemoved(today)) is { } busy
            ? Fail(DomainError.RuleViolation($"Bed {busy.Code} is in use, so {Name} can't be removed."))
            : Result.Success();

    // Floors

    public Result<Floor> AddFloor(int number, string? label)
    {
        var checkedLabel = CheckFloor(number, label, except: null);
        if (checkedLabel.IsFailure)
        {
            return checkedLabel.Error!;
        }

        var floor = new Floor(Id, number, Clean(label));
        floors.Add(floor);
        return floor;
    }

    /// <summary>Renumbering a floor moves its rooms with it, so their bed codes change too.</summary>
    public Result UpdateFloor(Guid floorId, int number, string? label)
    {
        if (FindFloor(floorId) is not { } floor)
        {
            return Fail(DomainError.NotFound("That floor doesn't exist."));
        }

        var valid = CheckFloor(number, label, except: floor);
        if (valid.IsFailure)
        {
            return valid;
        }

        foreach (var room in rooms.Where(r => r.FloorNumber == floor.Number))
        {
            room.MoveToFloor(number);
        }

        floor.Update(number, Clean(label));
        return EnsureUniqueBedCodes();
    }

    public Result RemoveFloor(Guid floorId)
    {
        if (FindFloor(floorId) is not { } floor)
        {
            return Fail(DomainError.NotFound("That floor doesn't exist."));
        }

        if (rooms.Any(r => r.FloorNumber == floor.Number))
        {
            return Fail(DomainError.RuleViolation($"Floor {floor.Number} still has rooms. Remove or move them first."));
        }

        floors.Remove(floor);
        return Result.Success();
    }

    public Floor? FindFloor(Guid floorId) => floors.FirstOrDefault(f => f.Id == floorId);

    // Rooms and beds

    public Result<Room> AddRoom(int floorNumber, string? number, RoomType type, int? dormBeds, RoomAttributes attributes, string? notes)
    {
        if (floors.All(f => f.Number != floorNumber))
        {
            return DomainError.Validation($"Floor {floorNumber} doesn't exist. Add the floor first.", "floorNumber");
        }

        var normalized = CheckRoomNumber(number, except: null);
        if (normalized.IsFailure)
        {
            return normalized.Error!;
        }

        var room = Room.Create(Id, floorNumber, normalized.Value, type, dormBeds, attributes, notes);
        if (room.IsFailure)
        {
            return room;
        }

        rooms.Add(room.Value);
        var unique = EnsureUniqueBedCodes();
        if (unique.IsFailure)
        {
            rooms.Remove(room.Value);
            return unique.Error!;
        }

        return room;
    }

    public Result UpdateRoom(
        Guid roomId, string? number, RoomType type, int? dormBeds, RoomAttributes attributes, string? notes, DateOnly today)
    {
        if (FindRoom(roomId) is not { } room)
        {
            return Fail(DomainError.NotFound("That room doesn't exist."));
        }

        var normalized = CheckRoomNumber(number, except: room);
        if (normalized.IsFailure)
        {
            return normalized;
        }

        var updated = room.Update(normalized.Value, type, dormBeds, attributes, notes, today);
        return updated.IsFailure ? updated : EnsureUniqueBedCodes();
    }

    /// <summary>Moves a room to another floor; its bed codes follow.</summary>
    public Result MoveRoom(Guid roomId, int floorNumber)
    {
        if (FindRoom(roomId) is not { } room)
        {
            return Fail(DomainError.NotFound("That room doesn't exist."));
        }

        if (floors.All(f => f.Number != floorNumber))
        {
            return Fail(DomainError.Validation($"Floor {floorNumber} doesn't exist.", "floorNumber"));
        }

        room.MoveToFloor(floorNumber);
        return EnsureUniqueBedCodes();
    }

    /// <summary>Removes the room and its beds (soft delete: their history stays). Every bed must be free or under maintenance.</summary>
    public Result RemoveRoom(Guid roomId, DateOnly today)
    {
        if (FindRoom(roomId) is not { } room)
        {
            return Fail(DomainError.NotFound("That room doesn't exist."));
        }

        var removable = room.CanBeRemoved(today);
        if (removable.IsFailure)
        {
            return removable;
        }

        rooms.Remove(room);
        return Result.Success();
    }

    public Room? FindRoom(Guid roomId) => rooms.FirstOrDefault(r => r.Id == roomId);

    public Bed? FindBed(Guid bedId) => rooms.SelectMany(r => r.Beds).FirstOrDefault(b => b.Id == bedId);

    public Room? RoomOf(Bed bed) => rooms.FirstOrDefault(r => r.Id == bed.RoomId);

    /// <summary>
    /// Holds a free bed for <see cref="ReservationDays"/> from <paramref name="today"/>: for a stay being checked
    /// in, or by hand (<paramref name="stayId"/> null). After that it counts as free again (<see cref="Bed.StatusOn"/>).
    /// </summary>
    public Result Reserve(Guid bedId, DateOnly today, Guid? stayId = null) =>
        WithBed(bedId, bed => IsActive
            ? bed.Reserve(today, ReservationDays, stayId)
            : Fail(DomainError.RuleViolation($"{Name} is not active, so its beds can't be reserved.")));

    public Result Release(Guid bedId) => WithBed(bedId, bed => bed.Release());

    /// <summary>A stay moves into the bed from <paramref name="from"/>; adds an open <see cref="BedOccupancy"/>.</summary>
    public Result<BedOccupancy> Occupy(Guid bedId, Guid stayId, DateOnly from, DateOnly today)
    {
        if (FindBed(bedId) is not { } bed)
        {
            return DomainError.NotFound("That bed doesn't exist.");
        }

        if (!IsActive)
        {
            return DomainError.RuleViolation($"{Name} is not active, so nobody can move in.");
        }

        return bed.Occupy(stayId, from, today);
    }

    /// <summary>The stay leaves on <paramref name="on"/>; closes its occupancy and frees the bed.</summary>
    public Result<BedOccupancy> Vacate(Guid bedId, Guid stayId, DateOnly on) =>
        FindBed(bedId) is { } bed ? bed.Vacate(stayId, on) : DomainError.NotFound("That bed doesn't exist.");

    public Result MarkMaintenance(Guid bedId, DateOnly today) => WithBed(bedId, bed => bed.MarkMaintenance(today));

    public Result ReturnToService(Guid bedId) => WithBed(bedId, bed => bed.ReturnToService());

    public Result SetBedRate(Guid bedId, Money? rate) => WithBed(bedId, bed => bed.SetRateOverride(rate));

    /// <summary>Turns reservations that have run out back into free beds; returns how many.</summary>
    public int ReleaseExpiredReservations(DateOnly today)
    {
        var expired = Beds.Where(b => b.IsReservationExpired(today)).ToList();
        foreach (var bed in expired)
        {
            bed.Release();
        }

        return expired.Count;
    }

    // Rates

    public Result<RatePlan> AddRatePlan(string? name, NepaliDate effectiveFrom, IReadOnlyDictionary<RoomType, Money> rates)
    {
        if (ratePlans.Any(p => p.EffectiveFrom == effectiveFrom.ToDateOnly()))
        {
            return DomainError.Conflict($"Another plan already starts on {effectiveFrom}.") with { Target = "effectiveFrom" };
        }

        var plan = RatePlan.Create(Id, name, effectiveFrom, rates);
        if (plan.IsSuccess)
        {
            ratePlans.Add(plan.Value);
        }

        return plan;
    }

    public Result UpdateRatePlan(Guid planId, string? name, NepaliDate effectiveFrom, IReadOnlyDictionary<RoomType, Money> rates)
    {
        if (FindRatePlan(planId) is not { } plan)
        {
            return Fail(DomainError.NotFound("That rate plan doesn't exist."));
        }

        if (ratePlans.Any(p => p != plan && p.EffectiveFrom == effectiveFrom.ToDateOnly()))
        {
            return Fail(DomainError.Conflict($"Another plan already starts on {effectiveFrom}.") with { Target = "effectiveFrom" });
        }

        return plan.Update(name, effectiveFrom, rates);
    }

    public RatePlan? FindRatePlan(Guid planId) => ratePlans.FirstOrDefault(p => p.Id == planId);

    /// <summary>The plan with the latest start on or before <paramref name="on"/>; null before the first plan.</summary>
    public RatePlan? PlanInEffect(DateOnly on) =>
        ratePlans.Where(p => p.EffectiveFrom <= on).MaxBy(p => p.EffectiveFrom);

    /// <summary>
    /// A bed's monthly rate on <paramref name="on"/>: its own override if it has one, else the rate for its room
    /// type in the plan in effect that day. Fails when neither gives a rate.
    /// </summary>
    public Result<Money> MonthlyRate(Guid bedId, DateOnly on)
    {
        if (FindBed(bedId) is not { } bed || RoomOf(bed) is not { } room)
        {
            return DomainError.NotFound("That bed doesn't exist.");
        }

        if (bed.RateOverride is { } own)
        {
            return own;
        }

        if (PlanInEffect(on) is not { } plan)
        {
            return DomainError.RuleViolation($"No rate plan is in effect on {NepaliDate.FromDateOnly(on)} and bed {bed.Code} has no rate of its own.");
        }

        return plan.RateFor(room.Type) is { } rate
            ? rate
            : DomainError.RuleViolation($"Plan \"{plan.Name}\" has no rate for {room.Type} rooms and bed {bed.Code} has no rate of its own.");
    }

    // Rules

    /// <summary>Bed codes are unique within the property. Unique room numbers make it so; this states it where it matters.</summary>
    private Result EnsureUniqueBedCodes()
    {
        var duplicate = Beds.GroupBy(b => b.Code, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
        return duplicate is null
            ? Result.Success()
            : Fail(DomainError.Conflict($"Bed code {duplicate.Key} would be used twice."));
    }

    private Result CheckFloor(int number, string? label, Floor? except)
    {
        if (number is < 0 or > Floor.MaxNumber)
        {
            return Fail(DomainError.Validation($"A floor number is 0 (ground) to {Floor.MaxNumber}.", "number"));
        }

        if (floors.Any(f => f != except && f.Number == number))
        {
            return Fail(DomainError.Conflict($"Floor {number} already exists.") with { Target = "number" });
        }

        return Clean(label) is { Length: > Floor.LabelMaxLength }
            ? Fail(DomainError.Validation($"A floor label can be at most {Floor.LabelMaxLength} characters.", "label"))
            : Result.Success();
    }

    private Result<string> CheckRoomNumber(string? number, Room? except)
    {
        var normalized = Room.NormalizeNumber(number);
        if (normalized.IsSuccess && rooms.Any(r => r != except && r.Number == normalized.Value))
        {
            return DomainError.Conflict($"Room {normalized.Value} already exists in {Name}.") with { Target = "number" };
        }

        return normalized;
    }

    private Result WithBed(Guid bedId, Func<Bed, Result> change) =>
        FindBed(bedId) is { } bed ? change(bed) : Fail(DomainError.NotFound("That bed doesn't exist."));

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static Result Fail(DomainError error) => Result.Failure(error);
}
