namespace FSH.Framework.Core.Common;

/// <summary>
/// The one per-line tax compute, shared verbatim by every record that carries a tax code
/// (Procurement's PurchaseOrder/PoLine, Sourcing's PurchaseRequisition/PrLine estimate) so there is
/// never a second implementation to drift out of sync. Line SST = round(amount * ratePct / 100, 2,
/// half-up). The <c>amount</c> argument is the line's ex-tax base, which differs by
/// record type and is the caller's business decision, not this function's — e.g. a PO line uses its
/// committed <c>Qty * UnitPrice</c>, a PR line uses its estimate-grade <c>Qty * EstUnitPrice</c>.
/// A record's total SST is always the SUM of these already-rounded line amounts, never a re-round of
/// the total, so a mixed-line record's tax equals exactly what's shown summed line-by-line.
/// </summary>
public static class TaxMath
{
    public static decimal LineSst(decimal amount, decimal ratePct) =>
        Math.Round(amount * ratePct / 100m, 2, MidpointRounding.AwayFromZero);
}
