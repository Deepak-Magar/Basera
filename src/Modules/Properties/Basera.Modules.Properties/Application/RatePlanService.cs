using Basera.Modules.Properties.Contracts;
using Basera.Shared.Domain;
using Basera.Shared.Domain.Calendar;
using Basera.Shared.Results;

namespace Basera.Modules.Properties.Application;

/// <summary>A rate plan as the owner enters it: a name, a BS start date and the rates it sets.</summary>
public sealed record RatePlanDetails(string? Name, NepaliDate EffectiveFrom, IReadOnlyDictionary<RoomType, Money> Rates);

/// <summary>Rate plans of one property (PRODUCT_SPEC §4.2). Listing them reads the property aggregate.</summary>
public sealed class RatePlanService(PropertyStore store)
{
    public Task<Result<Guid>> AddAsync(Guid propertyId, RatePlanDetails plan, CancellationToken cancellationToken = default) =>
        store.ChangeAsync(propertyId, property => property.AddRatePlan(plan.Name, plan.EffectiveFrom, plan.Rates).Map(p => p.Id), cancellationToken);

    public Task<Result> UpdateAsync(Guid propertyId, Guid planId, RatePlanDetails plan, CancellationToken cancellationToken = default) =>
        store.ChangeAsync(propertyId, property => property.UpdateRatePlan(planId, plan.Name, plan.EffectiveFrom, plan.Rates), cancellationToken);
}
