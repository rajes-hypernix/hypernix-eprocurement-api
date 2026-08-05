using FSH.Framework.Core.Domain;

namespace FSH.Modules.Suppliers.Domain.Onboarding;

/// <summary>
/// Financial pre-qualification for one Non-SWEC application (grain: one assessment per
/// application). Holds the 11 line items x 3 years plus append-only <see cref="FinancialSnapshot"/>s
/// of the computed Z/score/band/risk/statement captured at submit and at decision. Live values are
/// derived via <see cref="AltmanZModel"/>; the snapshot is the record of decision.
/// </summary>
public sealed class VendorFinancialAssessment : AggregateRoot<Guid>, IAuditableEntity
{
    private readonly List<FinancialYearFigures> _years = [];
    private readonly List<FinancialSnapshot> _snapshots = [];

    public Guid ApplicationId { get; private set; }

    /// <summary>The master vendor this assessment was copied onto at approval; null pre-approval.</summary>
    public Guid? VendorId { get; private set; }

    public string Remarks { get; private set; } = string.Empty;
    public DateTimeOffset CreatedOnUtc { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? LastModifiedOnUtc { get; private set; }
    public string? LastModifiedBy { get; private set; }

    public IReadOnlyList<FinancialYearFigures> Years => _years;
    public IReadOnlyList<FinancialSnapshot> Snapshots => _snapshots;

    private VendorFinancialAssessment() { }

    public static VendorFinancialAssessment Create(Guid applicationId, IEnumerable<FinancialYearFigures> years, DateTime nowUtc)
    {
        var assessment = new VendorFinancialAssessment
        {
            Id = Guid.CreateVersion7(),
            ApplicationId = applicationId,
            CreatedOnUtc = AuditTime.FromUtc(nowUtc),
            CreatedBy = null,
        };
        assessment._years.AddRange(years);
        return assessment;
    }

    /// <summary>Live weighted Altman Z′ over the current figures (derived; not stored).</summary>
    public double LiveWeightedZ => AltmanZModel.Weighted(_years.OrderBy(y => y.YearIndex));

    public void LinkToVendor(Guid vendorId) => VendorId = vendorId;

    public void ReplaceYears(IEnumerable<FinancialYearFigures> years)
    {
        ArgumentNullException.ThrowIfNull(years);
        _years.Clear();
        _years.AddRange(years);
        LastModifiedOnUtc = AuditTime.UtcNow;
        LastModifiedBy = null;
    }

    public void SetRemarks(string remarks)
    {
        Remarks = remarks ?? string.Empty;
        LastModifiedOnUtc = AuditTime.UtcNow;
        LastModifiedBy = null;
    }

    /// <summary>Captures an as-of snapshot of the computed model output at a decision point. Append-only.</summary>
    public FinancialSnapshot CaptureSnapshot(FinancialSnapshotStage stage, DateTime nowUtc)
    {
        double z = LiveWeightedZ;
        var band = AltmanZModel.BandFor(z);
        var snapshot = new FinancialSnapshot(
            stage,
            z,
            AltmanZModel.ScoreFor(z),
            band,
            AltmanZModel.RiskFor(band),
            AltmanZModel.StatementFor(band),
            nowUtc);
        _snapshots.Add(snapshot);
        LastModifiedOnUtc = AuditTime.FromUtc(nowUtc);
        LastModifiedBy = null;
        return snapshot;
    }
}
