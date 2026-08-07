using FSH.Modules.Platform.Contracts.Dtos;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using FSH.Modules.Platform.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.Currencies.ListCurrencies;

public sealed class ListCurrenciesQueryHandler(PlatformDbContext dbContext)
    : IQueryHandler<ListCurrenciesQuery, IReadOnlyList<CurrencyDto>>
{
    public async ValueTask<IReadOnlyList<CurrencyDto>> Handle(ListCurrenciesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var q = dbContext.Currencies.AsNoTracking().AsQueryable();
        if (query.ActiveOnly)
            q = q.Where(c => c.IsActive);

        return await q.OrderBy(c => c.Code)
            .Select(c => new CurrencyDto(c.Id, c.Code, c.Name, c.Symbol, c.Decimals, c.IsActive, c.CreatedOnUtc))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
