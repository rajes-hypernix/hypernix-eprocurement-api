using FSH.Modules.Platform.Contracts.Dtos;

namespace FSH.Modules.Sourcing.Features.v1.Rfqs;

/// <summary>
/// Resolves RFQ FX snapshot from Platform current rates (base → 1; missing foreign rate → null).
/// </summary>
internal static class RfqExchangeRateSupport
{
    internal static decimal? ResolveRateToBase(string currency, IReadOnlyList<ExchangeRateCurrentDto> rates)
    {
        ArgumentNullException.ThrowIfNull(rates);

        var code = string.IsNullOrWhiteSpace(currency) ? string.Empty : currency.Trim().ToUpperInvariant();
        if (code.Length == 0) return null;

        var hit = rates.FirstOrDefault(r =>
            string.Equals(r.CurrencyCode, code, StringComparison.OrdinalIgnoreCase));
        if (hit is null) return null;
        if (hit.IsBaseCurrency) return 1m;
        return hit.RateToBase;
    }

    internal static string ResolveBaseCurrency(IReadOnlyList<ExchangeRateCurrentDto> rates)
    {
        ArgumentNullException.ThrowIfNull(rates);
        return rates.FirstOrDefault(r => r.IsBaseCurrency)?.CurrencyCode ?? "MYR";
    }
}
