using Basera.Modules.Properties.Domain;
using Microsoft.EntityFrameworkCore;

namespace Basera.Modules.Properties.Infrastructure;

public static class PropertyQueries
{
    /// <summary>
    /// The whole <see cref="Property"/> aggregate, ready for its rules: floors, rate plans, rooms, beds and each
    /// bed's open occupancy. Closed occupancies are history and are queried on their own when shown. A hostel
    /// has tens to a few hundred beds, so loading them all for a change is cheap; split queries avoid one
    /// wide join multiplying rows.
    /// </summary>
    public static IQueryable<Property> WithAggregate(this IQueryable<Property> properties) => properties
        .Include(p => p.Floors)
        .Include(p => p.RatePlans)
        .Include(p => p.Rooms).ThenInclude(r => r.Beds).ThenInclude(b => b.Occupancies.Where(o => o.To == null))
        .AsSplitQuery();
}
