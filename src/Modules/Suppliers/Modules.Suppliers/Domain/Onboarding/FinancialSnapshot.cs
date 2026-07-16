namespace FSH.Modules.Suppliers.Domain.Onboarding;

/// <summary>
/// An immutable as-of capture of the Altman Z′ model output. Written at submit and at decision;
/// never updated. The weighted Z is stored as <see cref="double"/> (a derived ratio, not money).
/// </summary>
public sealed class FinancialSnapshot
{
    public Guid Id { get; private set; }
    public FinancialSnapshotStage Stage { get; private set; }
    public double WeightedZ { get; private set; }
    public int Score { get; private set; }
    public FinancialBand Band { get; private set; }
    public RiskCategory Risk { get; private set; }
    public string Statement { get; private set; }
    public DateTime AsOfUtc { get; private set; }

    public FinancialSnapshot(
        FinancialSnapshotStage stage,
        double weightedZ,
        int score,
        FinancialBand band,
        RiskCategory risk,
        string statement,
        DateTime asOfUtc)
    {
        Id = Guid.CreateVersion7();
        Stage = stage;
        WeightedZ = weightedZ;
        Score = score;
        Band = band;
        Risk = risk;
        Statement = statement;
        AsOfUtc = asOfUtc;
    }
}
