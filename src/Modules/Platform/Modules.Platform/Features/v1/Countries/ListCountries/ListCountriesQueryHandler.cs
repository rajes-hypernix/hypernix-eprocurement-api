using FSH.Modules.Platform.Contracts.Dtos;
using FSH.Modules.Platform.Contracts.v1.Countries;
using FSH.Modules.Platform.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.Countries.ListCountries;

public sealed class ListCountriesQueryHandler(PlatformDbContext dbContext)
    : IQueryHandler<ListCountriesQuery, IReadOnlyList<CountryDto>>
{
    public async ValueTask<IReadOnlyList<CountryDto>> Handle(ListCountriesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var q = dbContext.Countries.AsNoTracking().Where(c => !c.IsDeleted);
        if (query.ActiveOnly)
            q = q.Where(c => c.IsActive);

        return await q.OrderBy(c => c.Name)
            .Select(c => new CountryDto(c.Id, c.Code, c.Name, c.IsActive))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
