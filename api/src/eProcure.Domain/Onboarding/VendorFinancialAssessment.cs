namespace eProcure.Domain.Onboarding;

/// <summary>
/// Financial pre-qualification for one Non-SWEC application (grain: one assessment per
/// application; DATA-MODEL-ANALYTICS §7). Holds the 11 line items × 3 years as typed decimals
/// (RM'000) plus append-only <see cref="FinancialSnapshot"/>s of the computed Z/score/band/
/// risk/statement captured at submit and at decision (§6). Live values are DERIVED via
/// <see cref="AltmanZModel"/>; the snapshot is the record of decision, so a later change to
/// SPSB Finance's weights/thresholds never rewrites history.
/// </summary>
public class VendorFinancialAssessment
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    /// <summary>Owning application (FK; indexed for lineage joins).</summary>
    public Guid ApplicationId { get; private set; }

    /// <summary>The master vendor this assessment was copied onto at approval (SPEC §8); null pre-approval.</summary>
    public Guid? VendorId { get; set; }

    /// <summary>The 11 line items for each of the 3 years (YearIndex 0=FY-2, 1=FY-1, 2=current).</summary>
    public List<FinancialYearFigures> Years { get; set; } = [];

    /// <summary>As-of snapshots (append-only): captured at submit and at decision (§6).</summary>
    public List<FinancialSnapshot> Snapshots { get; set; } = [];

    /// <summary>Free-text finance reviewer remarks (SPEC §3).</summary>
    public string Remarks { get; set; } = "";

    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }

    // EF Core
    private VendorFinancialAssessment() { }

    public VendorFinancialAssessment(Guid applicationId, IEnumerable<FinancialYearFigures> years, DateTime nowUtc)
    {
        ApplicationId = applicationId;
        Years = [.. years];
        CreatedUtc = nowUtc;
        UpdatedUtc = nowUtc;
    }

    /// <summary>Live weighted Altman Z′ over the current figures (derived; not stored).</summary>
    public double LiveWeightedZ => AltmanZModel.Weighted(Years.OrderBy(y => y.YearIndex));

    /// <summary>
    /// Captures an as-of snapshot of the computed model output at a decision point. Append-only —
    /// each capture is a new immutable row (§6). Returns the snapshot written.
    /// </summary>
    public FinancialSnapshot CaptureSnapshot(FinancialSnapshotStage stage, DateTime nowUtc)
    {
        var z = LiveWeightedZ;
        var band = AltmanZModel.BandFor(z);
        var snap = new FinancialSnapshot(
            stage, z,
            AltmanZModel.ScoreFor(z),
            band,
            AltmanZModel.RiskFor(band),
            AltmanZModel.StatementFor(band),
            nowUtc);
        Snapshots.Add(snap);
        UpdatedUtc = nowUtc;
        return snap;
    }
}

/// <summary>
/// One year's 11 financial line items (RM'000), typed decimals (DATA-MODEL-ANALYTICS §6).
/// Owned by <see cref="VendorFinancialAssessment"/>. <see cref="YearIndex"/> is 0 (FY-2), 1 (FY-1)
/// or 2 (current) and selects the weight in <see cref="AltmanZModel.YearWeights"/>.
/// </summary>
public class FinancialYearFigures
{
    public int YearIndex { get; set; }

    public decimal Revenue { get; set; }
    public decimal NetProfit { get; set; }
    public decimal Ebit { get; set; }
    public decimal TotalAssets { get; set; }
    public decimal CurrentAssets { get; set; }
    public decimal Inventory { get; set; }
    public decimal CurrentLiabilities { get; set; }
    public decimal TotalLiabilities { get; set; }
    public decimal Equity { get; set; }
    public decimal RetainedEarnings { get; set; }
    public decimal FixedAssets { get; set; }

    public FinancialYearFigures() { }

    public FinancialYearFigures(int yearIndex) => YearIndex = yearIndex;
}

/// <summary>
/// An immutable as-of capture of the Altman Z′ model output (grain: one snapshot; §6/§7). Written
/// at submit and at decision; never updated. The weighted Z is stored as <see cref="double"/>
/// (a derived ratio, not money) so the global money precision rule does not truncate it.
/// </summary>
public class FinancialSnapshot
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public FinancialSnapshotStage Stage { get; private set; }
    public double WeightedZ { get; private set; }
    public int Score { get; private set; }
    public FinancialBand Band { get; private set; }
    public RiskCategory Risk { get; private set; }
    public string Statement { get; private set; } = "";
    public DateTime AsOfUtc { get; private set; }

    // EF Core
    private FinancialSnapshot() { }

    public FinancialSnapshot(FinancialSnapshotStage stage, double weightedZ, int score,
        FinancialBand band, RiskCategory risk, string statement, DateTime asOfUtc)
    {
        Stage = stage;
        WeightedZ = weightedZ;
        Score = score;
        Band = band;
        Risk = risk;
        Statement = statement;
        AsOfUtc = asOfUtc;
    }
}
