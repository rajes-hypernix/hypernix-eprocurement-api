using FSH.Modules.Platform.Contracts.Dtos;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using FSH.Modules.Platform.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.ExchangeRates.ListExchangeRateHistory;

public sealed class ListExchangeRateHistoryQueryHandler(PlatformDbContext dbContext)
    : IQueryHandler<ListExchangeRateHistoryQuery, IReadOnlyList<ExchangeRateDto>>
{
    public async ValueTask<IReadOnlyList<ExchangeRateDto>> Handle(
        ListExchangeRateHistoryQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var code = query.CurrencyCode.Trim().ToUpperInvariant();

        return await dbContext.ExchangeRates.AsNoTracking()
            .Where(r => r.CurrencyCode == code)
            .OrderByDescending(r => r.EffectiveDate)
            .ThenByDescending(r => r.CreatedOnUtc)
            .Select(r => new ExchangeRateDto(
                r.Id, r.CurrencyCode, r.RateToBase, r.EffectiveDate, r.EnteredByUserId, r.CreatedOnUtc))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
