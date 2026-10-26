using Basera.Shared.Domain;

namespace Basera.Modules.Properties.Domain;

/// <summary>
/// A floor of a property: its number (0 is the ground floor) and an optional label ("Ground floor", "Top").
/// Rooms point to a floor by number, not by id, so a removed room keeps the floor it was on in the history.
/// Changed only through <see cref="Property"/>.
/// </summary>
public sealed class Floor : Entity, ITenantOwned, IAuditable
{
    public const int MaxNumber = 99;
    public const int LabelMaxLength = 50;

    internal Floor(Guid propertyId, int number, string? label)
    {
        PropertyId = propertyId;
        Number = number;
        Label = label;
    }

    public Guid TenantId { get; private set; }

    public Guid PropertyId { get; private set; }

    public int Number { get; private set; }

    public string? Label { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    internal void Update(int number, string? label)
    {
        Number = number;
        Label = label;
    }
}
