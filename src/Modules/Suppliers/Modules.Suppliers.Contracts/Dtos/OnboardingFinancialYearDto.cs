namespace FSH.Modules.Suppliers.Contracts.Dtos;

/// <summary>Figures are RM'000 (thousands), not full currency units.</summary>
public sealed record OnboardingFinancialYearDto(
    int YearIndex,
    decimal Revenue,
    decimal NetProfit,
    decimal Ebit,
    decimal TotalAssets,
    decimal CurrentAssets,
    decimal Inventory,
    decimal CurrentLiabilities,
    decimal TotalLiabilities,
    decimal Equity,
    decimal RetainedEarnings,
    decimal FixedAssets);
