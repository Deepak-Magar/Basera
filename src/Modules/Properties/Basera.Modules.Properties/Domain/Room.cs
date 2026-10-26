using Basera.Shared.Domain;
using Basera.Shared.Results;

namespace Basera.Modules.Properties.Domain;

/// <summary>
/// A room on a floor: its number, type, attributes, notes and beds. The beds always match the type
/// (<see cref="RoomTypes.BedCount"/>): slots 1..n, each with a code built from floor, room and slot.
/// Changed only through <see cref="Property"/>.
/// </summary>
public sealed class Room : Entity, ITenantOwned, IAuditable, ISoftDeletable
{
    public const int NumberMaxLength = 10;
    public const int NotesMaxLength = 500;

    private readonly List<Bed> beds = [];

    private Room(Guid propertyId, int floorNumber, string number, RoomType type, RoomAttributes attributes, string? notes)
    {
        PropertyId = propertyId;
        FloorNumber = floorNumber;
        Number = number;
        Type = type;
        Attributes = attributes;
        Notes = notes;
    }

    // For EF Core.
    private Room()
    {
        Number = "";
    }

    public Guid TenantId { get; private set; }

    public Guid PropertyId { get; private set; }

    public int FloorNumber { get; private set; }

    /// <summary>Letters and digits, upper case ("101", "12A"). Unique within the property.</summary>
    public string Number { get; private set; }

    public RoomType Type { get; private set; }

    public RoomAttributes Attributes { get; private set; }

    public string? Notes { get; private set; }

    /// <summary>PostgreSQL <c>xmin</c>.</summary>
    public uint Version { get; private set; }

    /// <summary>The room's beds, by slot.</summary>
    public IReadOnlyList<Bed> Beds => beds.OrderBy(b => b.Slot).ToList();

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedBy { get; private set; }

    public bool Has(RoomAttributes attribute) => (Attributes & attribute) == attribute;

    /// <summary>"B{floor}-{room}-{slot}" (ARCHITECTURE §6).</summary>
    public static string BedCode(int floorNumber, string roomNumber, int slot) => $"B{floorNumber}-{roomNumber}-{slot}";

    /// <summary>Trimmed and upper-cased; letters and digits only, so a code never gets an extra "-".</summary>
    public static Result<string> NormalizeNumber(string? number)
    {
        var trimmed = number?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(trimmed))
        {
            return DomainError.Validation("Enter the room number.", "number");
        }

        if (trimmed.Length > NumberMaxLength || !trimmed.All(char.IsAsciiLetterOrDigit))
        {
            return DomainError.Validation($"A room number is up to {NumberMaxLength} letters and digits, e.g. 101 or 12A.", "number");
        }

        return trimmed;
    }

    internal static Result<Room> Create(
        Guid propertyId, int floorNumber, string number, RoomType type, int? dormBeds, RoomAttributes attributes, string? notes)
    {
        var count = RoomTypes.BedCount(type, dormBeds);
        if (count.IsFailure)
        {
            return count.Error!;
        }

        if (CheckNotes(notes) is { } invalid)
        {
            return invalid;
        }

        var room = new Room(propertyId, floorNumber, number, type, attributes, Clean(notes));
        room.ResizeTo(count.Value);
        return room;
    }

    /// <summary>
    /// Changes number, type, attributes and notes. A bigger type adds beds at the end; a smaller one removes the
    /// last beds, which must be free (or under maintenance) on <paramref name="today"/>.
    /// </summary>
    internal Result Update(string number, RoomType type, int? dormBeds, RoomAttributes attributes, string? notes, DateOnly today)
    {
        var count = RoomTypes.BedCount(type, dormBeds);
        if (count.IsFailure)
        {
            return count;
        }

        if (CheckNotes(notes) is { } invalid)
        {
            return invalid;
        }

        var leaving = beds.Where(b => b.Slot > count.Value).ToList();
        if (leaving.FirstOrDefault(b => !b.CanBeRemoved(today)) is { } busy)
        {
            return Result.Failure(DomainError.RuleViolation(
                $"Bed {busy.Code} is in use, so room {Number} can't have fewer than {busy.Slot} beds.", "type"));
        }

        Number = number;
        Type = type;
        Attributes = attributes;
        Notes = Clean(notes);
        ResizeTo(count.Value);
        Recode();
        return Result.Success();
    }

    internal void MoveToFloor(int floorNumber)
    {
        FloorNumber = floorNumber;
        Recode();
    }

    /// <summary>Removing a room removes its beds; all of them must be removable.</summary>
    internal Result CanBeRemoved(DateOnly today) =>
        beds.FirstOrDefault(b => !b.CanBeRemoved(today)) is { } busy
            ? Result.Failure(DomainError.RuleViolation($"Bed {busy.Code} is in use, so room {Number} can't be removed."))
            : Result.Success();

    private void ResizeTo(int count)
    {
        beds.RemoveAll(b => b.Slot > count);
        for (var slot = beds.Count + 1; slot <= count; slot++)
        {
            beds.Add(new Bed(PropertyId, Id, slot, BedCode(FloorNumber, Number, slot)));
        }
    }

    private void Recode()
    {
        foreach (var bed in beds)
        {
            bed.Recode(BedCode(FloorNumber, Number, bed.Slot));
        }
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static DomainError? CheckNotes(string? notes) =>
        Clean(notes) is { Length: > NotesMaxLength }
            ? DomainError.Validation($"Notes can be at most {NotesMaxLength} characters.", "notes")
            : null;
}
