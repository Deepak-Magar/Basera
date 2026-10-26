using Basera.Modules.Properties.Application;
using Basera.Modules.Properties.Contracts;
using Basera.Shared.Domain;
using Basera.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace Basera.Modules.Properties.Infrastructure;

/// <summary><see cref="IBedAvailability"/> over <see cref="BedService"/>: the same rules the owner's screens use.</summary>
internal sealed class BedAvailability(BedService beds) : IBedAvailability
{
    public Task<IReadOnlyList<FreeBed>> FreeBedsAsync(Guid propertyId, RoomType? type = null, CancellationToken cancellationToken = default) =>
        beds.FreeBedsAsync(propertyId, type, cancellationToken);

    public Task<Result> ReserveAsync(Guid bedId, Guid? stayId, CancellationToken cancellationToken = default) =>
        beds.ReserveAsync(bedId, stayId, cancellationToken);

    public Task<Result> ReleaseAsync(Guid bedId, CancellationToken cancellationToken = default) =>
        beds.ReleaseAsync(bedId, cancellationToken);

    public Task<Result> OccupyAsync(Guid bedId, Guid stayId, DateOnly from, CancellationToken cancellationToken = default) =>
        beds.OccupyAsync(bedId, stayId, from, cancellationToken);

    public Task<Result> VacateAsync(Guid bedId, Guid stayId, DateOnly on, CancellationToken cancellationToken = default) =>
        beds.VacateAsync(bedId, stayId, on, cancellationToken);
}

internal sealed class RateResolver(BedService beds) : IRateResolver
{
    public Task<Result<Money>> MonthlyRateAsync(Guid bedId, DateOnly on, CancellationToken cancellationToken = default) =>
        beds.MonthlyRateAsync(bedId, on, cancellationToken);
}

/// <summary><see cref="IPropertyDirectory"/> over <c>properties.property</c>, tenant-filtered.</summary>
internal sealed class PropertyDirectory(PropertiesDbContext db) : IPropertyDirectory
{
    public async Task<IReadOnlyList<PropertySummary>> ListAsync(CancellationToken cancellationToken = default) =>
        await db.Properties.AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new PropertySummary(p.Id, p.Name, p.Address, p.IsActive))
            .ToListAsync(cancellationToken);

    public Task<PropertySummary?> FindAsync(Guid propertyId, CancellationToken cancellationToken = default) =>
        db.Properties.AsNoTracking()
            .Where(p => p.Id == propertyId)
            .Select(p => new PropertySummary(p.Id, p.Name, p.Address, p.IsActive))
            .SingleOrDefaultAsync(cancellationToken);
}
