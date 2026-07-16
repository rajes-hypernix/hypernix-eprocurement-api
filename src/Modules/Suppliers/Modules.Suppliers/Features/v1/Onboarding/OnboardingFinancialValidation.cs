using FSH.Modules.Suppliers.Contracts.Dtos;
using FSH.Modules.Suppliers.Domain.Onboarding;

namespace FSH.Modules.Suppliers.Features.v1.Onboarding;

internal static class OnboardingFinancialValidation
{
    /// <summary>Guards against callers entering full ringgit instead of RM'000 (thousands).</summary>
    private const decimal FinancialFigureCap = 1_000_000_000_000m;

    internal static void Validate(IReadOnlyList<OnboardingFinancialYearDto> years)
    {
        ArgumentNullException.ThrowIfNull(years);

        var indexes = years.Select(y => y.YearIndex).ToList();
        if (years.Count != 3 || indexes.Distinct().Count() != 3 || indexes.Any(i => i is < 0 or > 2))
        {
            throw new OnboardingRuleException("Financials must have exactly three years indexed 0, 1 and 2.");
        }

        foreach (var year in years)
        {
            decimal[] figures =
            [
                year.Revenue, year.NetProfit, year.Ebit, year.TotalAssets, year.CurrentAssets, year.Inventory,
                year.CurrentLiabilities, year.TotalLiabilities, year.Equity, year.RetainedEarnings, year.FixedAssets,
            ];
            if (figures.Any(f => Math.Abs(f) >= FinancialFigureCap))
            {
                throw new OnboardingRuleException("Please enter amounts in RM'000 (thousands), not full ringgit.");
            }
        }
    }

    internal static IEnumerable<FinancialYearFigures> ToDomain(IReadOnlyList<OnboardingFinancialYearDto> years) =>
        years.Select(y => new FinancialYearFigures(
            y.YearIndex, y.Revenue, y.NetProfit, y.Ebit, y.TotalAssets, y.CurrentAssets, y.Inventory,
            y.CurrentLiabilities, y.TotalLiabilities, y.Equity, y.RetainedEarnings, y.FixedAssets));
}
