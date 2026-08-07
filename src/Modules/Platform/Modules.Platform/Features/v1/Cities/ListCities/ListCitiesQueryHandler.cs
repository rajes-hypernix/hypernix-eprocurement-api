using FSH.Modules.Platform.Contracts.Dtos;
using FSH.Modules.Platform.Contracts.v1.Cities;
using FSH.Modules.Platform.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.Cities.ListCities;

public sealed class ListCitiesQueryHandler(PlatformDbContext dbContext)
    : IQueryHandler<ListCitiesQuery, IReadOnlyList<CityDto>>
{
    public async ValueTask<IReadOnlyList<CityDto>> Handle(ListCitiesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var q = dbContext.Cities.AsNoTracking().Where(c => c.StateId == query.StateId && !c.IsDeleted);
        if (query.ActiveOnly)
            q = q.Where(c => c.IsActive);

        return await q.OrderBy(c => c.Name)
            .Select(c => new CityDto(c.Id, c.StateId, c.Name, c.IsActive, c.CreatedOnUtc))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
