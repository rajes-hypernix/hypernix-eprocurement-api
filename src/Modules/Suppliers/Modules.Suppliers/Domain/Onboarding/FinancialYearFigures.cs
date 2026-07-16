namespace FSH.Modules.Suppliers.Domain.Onboarding;

/// <summary>
/// One year's 11 financial line items (RM'000). Owned by <see cref="VendorFinancialAssessment"/>.
/// <see cref="YearIndex"/> is 0 (FY-2), 1 (FY-1) or 2 (current) and selects the weight in
/// <see cref="AltmanZModel.YearWeights"/>.
/// </summary>
public sealed class FinancialYearFigures
{
    public int YearIndex { get; private set; }
    public decimal Revenue { get; private set; }
    public decimal NetProfit { get; private set; }
    public decimal Ebit { get; private set; }
    public decimal TotalAssets { get; private set; }
    public decimal CurrentAssets { get; private set; }
    public decimal Inventory { get; private set; }
    public decimal CurrentLiabilities { get; private set; }
    public decimal TotalLiabilities { get; private set; }
    public decimal Equity { get; private set; }
    public decimal RetainedEarnings { get; private set; }
    public decimal FixedAssets { get; private set; }

    public FinancialYearFigures(
        int yearIndex,
        decimal revenue,
        decimal netProfit,
        decimal ebit,
        decimal totalAssets,
        decimal currentAssets,
        decimal inventory,
        decimal currentLiabilities,
        decimal totalLiabilities,
        decimal equity,
        decimal retainedEarnings,
        decimal fixedAssets)
    {
        YearIndex = yearIndex;
        Revenue = revenue;
        NetProfit = netProfit;
        Ebit = ebit;
        TotalAssets = totalAssets;
        CurrentAssets = currentAssets;
        Inventory = inventory;
        CurrentLiabilities = currentLiabilities;
        TotalLiabilities = totalLiabilities;
        Equity = equity;
        RetainedEarnings = retainedEarnings;
        FixedAssets = fixedAssets;
    }
}
