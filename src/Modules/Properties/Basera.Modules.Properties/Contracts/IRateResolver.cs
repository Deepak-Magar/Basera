using Basera.Shared.Domain;
using Basera.Shared.Results;

namespace Basera.Modules.Properties.Contracts;

/// <summary>
/// A bed's monthly rate on a day (PRODUCT_SPEC §4.2): its own override, else its room type's rate in the plan
/// in effect that day. Residents uses it to propose a stay's agreed rate; the stay keeps the rate it agreed,
/// so later plan changes don't rewrite past invoices. Fails when neither override nor plan gives a rate.
/// </summary>
public interface IRateResolver
{
    Task<Result<Money>> MonthlyRateAsync(Guid bedId, DateOnly on, CancellationToken cancellationToken = default);
}
