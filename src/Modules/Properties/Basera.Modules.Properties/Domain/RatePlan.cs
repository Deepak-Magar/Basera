using Basera.Modules.Properties.Contracts;
using Basera.Shared.Domain;
using Basera.Shared.Domain.Calendar;
using Basera.Shared.Results;

namespace Basera.Modules.Properties.Domain;

/// <summary>
/// A named set of monthly rates by room type, in effect from a BS date (PRODUCT_SPEC §4.2). A property's plan
/// on a day is the one with the latest start on or before it; a bed's own rate override beats any plan.
/// One column per room type: the types are fixed, and a report can read a rate without unpacking anything.
/// </summary>
public sealed class RatePlan : Entity, ITenantOwned, IAuditable
{
    public const int NameMaxLength = 100;

    private RatePlan(Guid propertyId, string name, DateOnly effectiveFrom)
    {
        PropertyId = propertyId;
        Name = name;
        EffectiveFrom = effectiveFrom;
    }

    // For EF Core.
    private RatePlan()
    {
        Name = "";
    }

    public Guid TenantId { get; private set; }

    public Guid PropertyId { get; private set; }

    public string Name { get; private set; }

    /// <summary>Stored in AD (§6 rule 10); entered and shown in BS.</summary>
    public DateOnly EffectiveFrom { get; private set; }

    public NepaliDate EffectiveFromBs => NepaliDate.FromDateOnly(EffectiveFrom);

    public Money? SingleRate { get; private set; }

    public Money? DoubleRate { get; private set; }

    public Money? TripleRate { get; private set; }

    public Money? FourRate { get; private set; }

    public Money? DormRate { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    /// <summary>The rates this plan sets; a type it leaves out has no rate under this plan.</summary>
    public IReadOnlyDictionary<RoomType, Money> Rates => RoomTypes.All
        .Where(type => RateFor(type) is not null)
        .ToDictionary(type => type, type => RateFor(type)!.Value);

    public Money? RateFor(RoomType type) => type switch
    {
        RoomType.Single => SingleRate,
        RoomType.Double => DoubleRate,
        RoomType.Triple => TripleRate,
        RoomType.Four => FourRate,
        RoomType.Dorm => DormRate,
        _ => null,
    };

    internal static Result<RatePlan> Create(Guid propertyId, string? name, NepaliDate effectiveFrom, IReadOnlyDictionary<RoomType, Money> rates)
    {
        var plan = new RatePlan(propertyId, "", effectiveFrom.ToDateOnly());
        var set = plan.Set(name, effectiveFrom, rates);
        return set.IsSuccess ? plan : set.Error!;
    }

    internal Result Update(string? name, NepaliDate effectiveFrom, IReadOnlyDictionary<RoomType, Money> rates) =>
        Set(name, effectiveFrom, rates);

    private Result Set(string? name, NepaliDate effectiveFrom, IReadOnlyDictionary<RoomType, Money> rates)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return Result.Failure(DomainError.Validation("Enter a name for the rate plan.", "name"));
        }

        if (trimmed.Length > NameMaxLength)
        {
            return Result.Failure(DomainError.Validation($"A plan name can be at most {NameMaxLength} characters.", "name"));
        }

        if (rates.Count == 0)
        {
            return Result.Failure(DomainError.Validation("Give a monthly rate for at least one room type.", "rates"));
        }

        foreach (var (type, rate) in rates)
        {
            if (rate <= Money.Zero)
            {
                return Result.Failure(DomainError.Validation($"The {type} rate must be more than zero.", $"rate{type}"));
            }
        }

        Name = trimmed;
        EffectiveFrom = effectiveFrom.ToDateOnly();
        SingleRate = Rate(RoomType.Single);
        DoubleRate = Rate(RoomType.Double);
        TripleRate = Rate(RoomType.Triple);
        FourRate = Rate(RoomType.Four);
        DormRate = Rate(RoomType.Dorm);
        return Result.Success();

        Money? Rate(RoomType type) => rates.TryGetValue(type, out var rate) ? rate : null;
    }
}
