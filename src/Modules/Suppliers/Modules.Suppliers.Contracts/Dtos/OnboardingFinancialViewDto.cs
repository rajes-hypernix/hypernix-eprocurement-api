namespace FSH.Modules.Suppliers.Contracts.Dtos;

/// <summary>The live (not-yet-snapshotted) Altman Z' calculation, for the buyer's review "view calculation" drawer.</summary>
public sealed record OnboardingFinancialYearCalcDto(int YearIndex, double X1, double X2, double X3, double X4, double X5, double Z);

public sealed record OnboardingFinancialViewDto(
    string Band,
    string Risk,
    string Zone,
    double WeightedZ,
    int Score,
    string Statement,
    IReadOnlyList<OnboardingFinancialYearCalcDto> Years);
