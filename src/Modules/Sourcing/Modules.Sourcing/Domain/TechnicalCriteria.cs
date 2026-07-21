namespace FSH.Modules.Sourcing.Domain;

/// <summary>Fixed weighting for the 4 technical-evaluation criteria — ported from the old system's evaluation model.</summary>
public static class TechnicalCriteria
{
    public const int Threshold = 70;
    public const int TechWeight = 70;
    public const int CommWeight = 30;

    public static IReadOnlyDictionary<TechnicalCriterion, int> Weights { get; } = new Dictionary<TechnicalCriterion, int>
    {
        [TechnicalCriterion.Compliance] = 35,
        [TechnicalCriterion.Experience] = 20,
        [TechnicalCriterion.Delivery] = 20,
        [TechnicalCriterion.QA] = 25,
    };
}
