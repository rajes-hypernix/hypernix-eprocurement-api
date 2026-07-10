namespace eProcure.Domain.Sourcing;

/// <summary>
/// One evaluator's score (0–100) for one vendor on one criterion of an RFQ's
/// technical envelope. Per-evaluator weighted score and committee average are
/// derived (see TechnicalEvaluation). Unique per (Rfq, Vendor, Evaluator, Criterion).
/// </summary>
public class TechnicalScore
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid RfqId { get; set; }
    public Guid VendorId { get; set; }
    public string EvaluatorId { get; set; } = default!;   // internal user code, e.g. u_hafiz
    public string Criterion { get; set; } = default!;     // compliance | experience | delivery | qa
    public int Score { get; set; }                        // 0..100
    public DateTime CreatedUtc { get; set; }              // stamped once on first score (IClock)
    public DateTime UpdatedUtc { get; set; }

    private TechnicalScore() { }

    public TechnicalScore(Guid rfqId, Guid vendorId, string evaluatorId, string criterion, int score)
    {
        Id = Guid.NewGuid();
        RfqId = rfqId;
        VendorId = vendorId;
        EvaluatorId = evaluatorId;
        Criterion = criterion;
        Score = score;
    }
}

/// <summary>The weighted technical criteria + threshold (matches the prototype).</summary>
public static class TechnicalCriteria
{
    public sealed record Criterion(string Key, string Label, int Weight);

    public static readonly IReadOnlyList<Criterion> All =
    [
        new("compliance", "Technical specification compliance", 35),
        new("experience", "Track record & similar supply", 20),
        new("delivery", "Delivery capability & capacity", 20),
        new("qa", "QA, certifications & warranty", 25),
    ];

    public const int Threshold = 70;   // pass mark
    public const int TechWeight = 70;  // combined-eval technical weight %
    public const int CommWeight = 30;  // combined-eval commercial weight %

    public static bool IsValidKey(string key) => All.Any(c => c.Key == key);
}
