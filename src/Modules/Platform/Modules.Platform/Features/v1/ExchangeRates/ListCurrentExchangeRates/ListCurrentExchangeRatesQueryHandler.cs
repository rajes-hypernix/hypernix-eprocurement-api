using FSH.Modules.Platform.Contracts.Dtos;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using FSH.Modules.Platform.Data;
using FSH.Modules.Platform.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.ExchangeRates.ListCurrentExchangeRates;

public sealed class ListCurrentExchangeRatesQueryHandler(PlatformDbContext dbContext)
    : IQueryHandler<ListCurrentExchangeRatesQuery, IReadOnlyList<ExchangeRateCurrentDto>>
{
    public async ValueTask<IReadOnlyList<ExchangeRateCurrentDto>> Handle(
        ListCurrentExchangeRatesQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var baseCurrency = await dbContext.Settings.AsNoTracking()
            .Where(s => s.Key == SettingKeys.BaseCurrency)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var currencies = await dbContext.Currencies.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Code)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var codes = currencies.Select(c => c.Code).ToList();
        var rates = await dbContext.ExchangeRates.AsNoTracking()
            .Where(r => codes.Contains(r.CurrencyCode))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var latestByCode = rates
            .GroupBy(r => r.CurrencyCode)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(r => r.EffectiveDate).ThenByDescending(r => r.CreatedOnUtc).First());

        var result = new List<ExchangeRateCurrentDto>(currencies.Count);
        foreach (var currency in currencies)
        {
            bool isBase = !string.IsNullOrWhiteSpace(baseCurrency)
                && string.Equals(currency.Code, baseCurrency, StringComparison.OrdinalIgnoreCase);

            if (isBase)
            {
                result.Add(new ExchangeRateCurrentDto(currency.Code, currency.Name, 1m, null, true));
                continue;
            }

            if (latestByCode.TryGetValue(currency.Code, out var latest))
            {
                result.Add(new ExchangeRateCurrentDto(
                    currency.Code, currency.Name, latest.RateToBase, latest.EffectiveDate, false));
            }
            else
            {
                result.Add(new ExchangeRateCurrentDto(currency.Code, currency.Name, null, null, false));
            }
        }

        return result;
    }
}
