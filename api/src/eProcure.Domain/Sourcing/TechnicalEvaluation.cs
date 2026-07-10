namespace eProcure.Domain.Sourcing;

/// <summary>
/// Pure technical-scoring maths (mirrors the prototype): a per-evaluator weighted
/// score (Σ score×weight / 100, null until all criteria scored), the committee
/// average across evaluators, and pass = committee ≥ threshold.
/// </summary>
public static class TechnicalEvaluation
{
    public static double? WeightedForEvaluator(IReadOnlyCollection<TechnicalScore> vendorEvaluatorScores)
    {
        double sum = 0;
        foreach (var c in TechnicalCriteria.All)
        {
            var s = vendorEvaluatorScores.FirstOrDefault(x => x.Criterion == c.Key);
            if (s is null) return null;        // incomplete — not yet weightable
            sum += s.Score * c.Weight;
        }
        return sum / 100.0;
    }

    public static double? Committee(
        IReadOnlyCollection<TechnicalScore> rfqScores, Guid vendorId, IReadOnlyCollection<string> evaluators)
    {
        var weights = evaluators
            .Select(ev => WeightedForEvaluator(
                rfqScores.Where(s => s.VendorId == vendorId && s.EvaluatorId == ev).ToList()))
            .Where(w => w is not null)
            .Select(w => w!.Value)
            .ToList();
        if (weights.Count == 0) return null;
        return Math.Round(weights.Average(), 1);
    }

    public static bool Pass(double? committee) =>
        committee is not null && committee.Value >= TechnicalCriteria.Threshold;

    /// <summary>Masked alias "Bidder A/B/C…" by the vendor's position in the invited list.</summary>
    public static string Alias(IReadOnlyList<string> invitedVendorIds, Guid vendorId)
    {
        var target = vendorId.ToString();
        for (var i = 0; i < invitedVendorIds.Count; i++)
            if (invitedVendorIds[i] == target)
                return $"Bidder {(char)('A' + i)}";
        return "Bidder ?";
    }
}
