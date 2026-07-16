namespace FSH.Modules.Suppliers.Domain.Onboarding;

/// <summary>
/// The SPSB private-firm Altman Z′ financial pre-qualification model — pure, deterministic.
/// Coefficients, year weights, band thresholds and the score clamp are fixed; a later change
/// produces a NEW <see cref="FinancialSnapshot"/>, it never rewrites a stored one.
///
/// Arithmetic is in <see cref="double"/> to match the original prototype's JavaScript number
/// semantics exactly (including Math.round = floor(x+0.5)); stored money line items remain
/// <see cref="decimal"/>. No I/O, no clock, no dependencies.
/// </summary>
public static class AltmanZModel
{
    private const double C1 = 0.717, C2 = 0.847, C3 = 3.107, C4 = 0.420, C5 = 0.998;

    /// <summary>Year weights FY-2 / FY-1 / current, indexed by <see cref="FinancialYearFigures.YearIndex"/> (0,1,2).</summary>
    public static readonly IReadOnlyList<double> YearWeights = [0.2, 0.3, 0.5];

    private const double BandA = 2.9, BandB = 2.0, BandC = 1.23;

    /// <summary>
    /// One year's Altman Z′ from its figures. A zero denominator (Total assets / Total liabilities
    /// of 0) yields a 0 ratio rather than NaN/Infinity, so Z is always finite.
    /// </summary>
    public static double Z(FinancialYearFigures figures)
    {
        ArgumentNullException.ThrowIfNull(figures);
        var c = Components(figures);
        return (C1 * c.X1) + (C2 * c.X2) + (C3 * c.X3) + (C4 * c.X4) + (C5 * c.X5);
    }

    private static double Div(double numerator, double denominator) => denominator == 0 ? 0 : numerator / denominator;

    /// <summary>The five weighted Z′ components (X1..X5) for one year — for the review calc drawer.</summary>
    public static (double X1, double X2, double X3, double X4, double X5) Components(FinancialYearFigures figures)
    {
        ArgumentNullException.ThrowIfNull(figures);
        double ca = (double)figures.CurrentAssets;
        double cl = (double)figures.CurrentLiabilities;
        double ta = (double)figures.TotalAssets;
        double tl = (double)figures.TotalLiabilities;
        double eq = (double)figures.Equity;
        double rev = (double)figures.Revenue;
        double ebit = (double)figures.Ebit;
        double re = (double)figures.RetainedEarnings;
        return (Div(ca - cl, ta), Div(re, ta), Div(ebit, ta), Div(eq, tl), Div(rev, ta));
    }

    /// <summary>
    /// Weighted Z′ across the supplied years using <see cref="YearWeights"/>. Each year contributes
    /// Z × weight[YearIndex]; a year with an out-of-range index contributes nothing.
    /// </summary>
    public static double Weighted(IEnumerable<FinancialYearFigures> years)
    {
        ArgumentNullException.ThrowIfNull(years);
        return years.Sum(y => y.YearIndex >= 0 && y.YearIndex < YearWeights.Count ? Z(y) * YearWeights[y.YearIndex] : 0);
    }

    /// <summary>Band from a (weighted) Z′: A ≥2.9, B ≥2.0, C ≥1.23, else D.</summary>
    public static FinancialBand BandFor(double z) =>
        z >= BandA ? FinancialBand.A : z >= BandB ? FinancialBand.B : z >= BandC ? FinancialBand.C : FinancialBand.D;

    /// <summary>Score /100: clamp(round(z/4.5×100), 5, 99), where round is JavaScript's Math.round (= floor(x + 0.5)).</summary>
    public static int ScoreFor(double z)
    {
        if (!double.IsFinite(z))
        {
            return 5;
        }

        int rounded = (int)Math.Floor((z / 4.5 * 100.0) + 0.5);
        return Math.Max(5, Math.Min(99, rounded));
    }

    public static RiskCategory RiskFor(FinancialBand band) => band switch
    {
        FinancialBand.A => RiskCategory.Low,
        FinancialBand.B => RiskCategory.LowMedium,
        FinancialBand.C => RiskCategory.Medium,
        _ => RiskCategory.High,
    };

    /// <summary>The auto conditional statement for a band.</summary>
    public static string StatementFor(FinancialBand band) => band switch
    {
        FinancialBand.A => "Financially sound across the review period with strong liquidity, solvency and consistent profitability. Recommended for registration without financial conditions.",
        FinancialBand.B => "Generally stable financial position with adequate liquidity and acceptable gearing. Recommended for registration; a routine annual financial review is advised.",
        FinancialBand.C => "Moderate financial risk — weakening liquidity and rising gearing over the period. Recommend conditional registration with enhanced annual monitoring.",
        _ => "Elevated financial-distress indicators with the Z-score in the distress zone. Recommend conditional registration subject to a performance bond / bank guarantee and close monitoring.",
    };
}
