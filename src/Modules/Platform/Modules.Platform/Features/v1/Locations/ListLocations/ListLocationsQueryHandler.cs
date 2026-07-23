using FSH.Modules.Platform.Contracts.Dtos;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using FSH.Modules.Platform.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.Locations.ListLocations;

public sealed class ListLocationsQueryHandler(PlatformDbContext dbContext)
    : IQueryHandler<ListLocationsQuery, IReadOnlyList<LocationDto>>
{
    public async ValueTask<IReadOnlyList<LocationDto>> Handle(ListLocationsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var q = dbContext.Locations.AsNoTracking().AsQueryable();
        if (query.ActiveOnly)
            q = q.Where(l => l.IsActive);

        var locations = await q.OrderBy(l => l.Code).ToListAsync(cancellationToken).ConfigureAwait(false);
        return [.. locations.Select(ConfigurationMapping.ToDto)];
    }
}
