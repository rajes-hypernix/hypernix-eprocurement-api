using FSH.Modules.Platform.Contracts.Dtos;
using FSH.Modules.Platform.Contracts.v1.Catalog;
using FSH.Modules.Platform.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.Catalog.GetGeoCatalog;

public sealed class GetGeoCatalogQueryHandler(PlatformDbContext dbContext)
    : IQueryHandler<GetGeoCatalogQuery, GeoCatalogDto>
{
    public async ValueTask<GeoCatalogDto> Handle(GetGeoCatalogQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var countries = await dbContext.Countries
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var countryIds = countries.Select(c => c.Id).ToList();
        var states = await dbContext.States
            .AsNoTracking()
            .Where(s => s.IsActive && countryIds.Contains(s.CountryId))
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var stateIds = states.Select(s => s.Id).ToList();
        var cities = await dbContext.Cities
            .AsNoTracking()
            .Where(c => c.IsActive && stateIds.Contains(c.StateId))
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var banksQuery = dbContext.Banks.AsNoTracking().Where(b => b.IsActive);
        if (!string.IsNullOrWhiteSpace(query.BankCountryCode))
        {
            var code = query.BankCountryCode.Trim().ToUpperInvariant();
            banksQuery = banksQuery.Where(b => b.CountryCode == code);
        }

        var banks = await banksQuery
            .OrderBy(b => b.Name)
            .Select(b => new BankDto(b.Id, b.Name, b.SwiftCode, b.CountryCode, b.IsActive))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var nested = countries.Select(c =>
        {
            var countryStates = states.Where(s => s.CountryId == c.Id).Select(s =>
            {
                var stateCities = cities
                    .Where(city => city.StateId == s.Id)
                    .Select(city => new CityLookupDto(city.Id, city.Name))
                    .ToList();
                return new StateLookupDto(s.Id, s.Code, s.Name, stateCities);
            }).ToList();
            return new CountryLookupDto(c.Id, c.Code, c.Name, countryStates);
        }).ToList();

        return new GeoCatalogDto(nested, banks);
    }
}
