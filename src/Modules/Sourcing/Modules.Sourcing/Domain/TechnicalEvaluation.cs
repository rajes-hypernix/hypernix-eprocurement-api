namespace FSH.Modules.Sourcing.Domain;

/// <summary>Pure scoring maths shared by the evaluation slices — no persistence, no side effects.</summary>
public static class TechnicalEvaluation
{
    /// <summary>One evaluator's weighted 0-100 score across all 4 criteria — null if any criterion is unscored.</summary>
    public static decimal? WeightedForEvaluator(IReadOnlyList<TechnicalScore> evaluatorScores)
    {
        ArgumentNullException.ThrowIfNull(evaluatorScores);

        var byCriterion = evaluatorScores.ToDictionary(s => s.Criterion);
        if (!TechnicalCriteria.Weights.Keys.All(byCriterion.ContainsKey))
        {
            return null;
        }

        decimal total = 0;
        foreach (var (criterion, weight) in TechnicalCriteria.Weights)
        {
            total += byCriterion[criterion].Score * weight / 100m;
        }

        return total;
    }

    /// <summary>The committee score for one vendor — the average of evaluators' complete weighted scores; null if none are complete.</summary>
    public static decimal? Committee(IReadOnlyList<TechnicalScore> allScoresForVendor)
    {
        ArgumentNullException.ThrowIfNull(allScoresForVendor);

        var weighted = allScoresForVendor
            .GroupBy(s => s.EvaluatorId)
            .Select(g => WeightedForEvaluator([.. g]))
            .Where(w => w is not null)
            .Select(w => w!.Value)
            .ToList();

        return weighted.Count == 0 ? null : weighted.Average();
    }

    public static bool Pass(decimal? committeeScore) => committeeScore is { } score && score >= TechnicalCriteria.Threshold;

    /// <summary>"Bidder A/B/C" by invited order position — the evaluator-facing alias until the commercial envelope opens.</summary>
    public static string Alias(IReadOnlyList<Guid> invitedVendorIdsInOrder, Guid vendorId)
    {
        ArgumentNullException.ThrowIfNull(invitedVendorIdsInOrder);

        int index = -1;
        for (int i = 0; i < invitedVendorIdsInOrder.Count; i++)
        {
            if (invitedVendorIdsInOrder[i] == vendorId)
            {
                index = i;
                break;
            }
        }

        return index < 0 ? "Bidder ?" : $"Bidder {(char)('A' + index)}";
    }
}
