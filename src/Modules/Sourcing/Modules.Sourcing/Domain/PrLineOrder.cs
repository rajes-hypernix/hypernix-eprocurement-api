namespace FSH.Modules.Sourcing.Domain;

/// <summary>
/// Reservation ledger for direct-from-requisition ordering (IC14/IC15) — the arithmetic source of
/// truth for "quantity already ordered" against a PR line. Deliberately never derived by
/// cross-schema-querying Procurement's PoLine table: this ledger lives entirely in Sourcing so the
/// row-lock + cap-check + insert can happen inside one local transaction. Append-only, mirrors
/// <see cref="PrLineSourcing"/>: a released/cancelled reservation is closed, never deleted (grain:
/// one row per PR-line-to-PO commitment).
/// </summary>
public sealed class PrLineOrder
{
    public Guid Id { get; private set; }
    public Guid PrLineId { get; private set; }
    public Guid PoId { get; private set; }
    public string? PoLineRef { get; private set; }
    public decimal QtyOrdered { get; private set; }
    public LinkStatus LinkStatus { get; private set; } = LinkStatus.Active;
    public string? Reason { get; private set; }
    public DateTime CreatedUtc { get; private set; }
    public DateTime? ClosedUtc { get; private set; }

    private PrLineOrder() { }

    public static PrLineOrder Create(Guid prLineId, Guid poId, string? poLineRef, decimal qtyOrdered, DateTime nowUtc)
    {
        if (prLineId == Guid.Empty || poId == Guid.Empty)
        {
            throw new SourcingRuleException("An order reservation requires both a PR line and a PO.");
        }

        if (qtyOrdered <= 0)
        {
            throw new SourcingRuleException("Ordered quantity must be greater than zero.");
        }

        return new PrLineOrder
        {
            Id = Guid.CreateVersion7(),
            PrLineId = prLineId,
            PoId = poId,
            PoLineRef = poLineRef,
            QtyOrdered = qtyOrdered,
            CreatedUtc = nowUtc,
        };
    }

    /// <summary>The sourcing PO was cancelled or reduced — quantity returns to the PR line's remaining balance.</summary>
    public void MarkReleased(string? reason, DateTime nowUtc) => Close(LinkStatus.Returned, reason, nowUtc);

    public void MarkCancelled(string? reason, DateTime nowUtc) => Close(LinkStatus.Cancelled, reason, nowUtc);

    private void Close(LinkStatus status, string? reason, DateTime nowUtc)
    {
        if (LinkStatus != LinkStatus.Active)
        {
            throw new SourcingRuleException("This order reservation is already closed.");
        }

        LinkStatus = status;
        Reason = reason;
        ClosedUtc = nowUtc;
    }
}
