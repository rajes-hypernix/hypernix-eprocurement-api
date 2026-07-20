namespace FSH.Modules.Sourcing.Domain;

/// <summary>One line on an RFQ. <see cref="LineCode"/> is stable per-RFQ and is the lineage target for <see cref="PrLineSourcing"/>.</summary>
public sealed class RfqLine
{
    public string LineCode { get; private set; }
    public string ItemCode { get; private set; }
    public string Description { get; private set; }
    public decimal Qty { get; private set; }
    public string Uom { get; private set; }
    public string? PrRef { get; private set; }

    /// <summary>Source PR-line ids for a consolidated/merged line.</summary>
    public List<string> SourcePrLineIds { get; private set; } = [];

    public RfqLine(string lineCode, string itemCode, string description, decimal qty, string uom, string? prRef, List<string>? sourcePrLineIds)
    {
        LineCode = lineCode;
        ItemCode = itemCode;
        Description = description;
        Qty = qty;
        Uom = uom;
        PrRef = prRef;
        SourcePrLineIds = sourcePrLineIds is { Count: > 0 } ? sourcePrLineIds : [];
    }
}
