using FSH.Framework.Core.Domain;

namespace FSH.Modules.Sourcing.Domain;

/// <summary>
/// One evaluator's score for one criterion on one vendor's bid. A top-level aggregate (not owned
/// by <see cref="Rfq"/>) so concurrent evaluators scoring different vendors don't contend on the
/// same aggregate row. Unique per (RfqId, VendorId, EvaluatorId, Criterion).
/// </summary>
public sealed class TechnicalScore : AggregateRoot<Guid>
{
    public Guid RfqId { get; private set; }

    /// <summary>Bare reference into Modules.Suppliers — no cross-schema FK, matching RfqInvitation.VendorId.</summary>
    public Guid VendorId { get; private set; }

    /// <summary>The scoring FSH Identity user's id (string — matches AspNetUsers.Id).</summary>
    public string EvaluatorId { get; private set; } = default!;

    public TechnicalCriterion Criterion { get; private set; }
    public int Score { get; private set; }
    public DateTime CreatedUtc { get; private set; }
    public DateTime UpdatedUtc { get; private set; }

    private TechnicalScore() { }

    public static TechnicalScore Create(Guid rfqId, Guid vendorId, string evaluatorId, TechnicalCriterion criterion, int score)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(evaluatorId);

        var now = DateTime.UtcNow;
        return new TechnicalScore
        {
            Id = Guid.CreateVersion7(),
            RfqId = rfqId,
            VendorId = vendorId,
            EvaluatorId = evaluatorId,
            Criterion = criterion,
            Score = Math.Clamp(score, 0, 100),
            CreatedUtc = now,
            UpdatedUtc = now,
        };
    }

    public void SetScore(int score, bool techFinalized)
    {
        if (techFinalized)
        {
            throw new SourcingRuleException("Scores cannot change after the technical evaluation is finalized.");
        }

        Score = Math.Clamp(score, 0, 100);
        UpdatedUtc = DateTime.UtcNow;
    }
}
