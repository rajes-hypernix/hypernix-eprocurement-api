namespace FSH.Modules.Sourcing.Domain;

/// <summary>
/// Append-only provenance link between a PR line and the RFQ line it was sourced through.
/// Never deleted — a merged/consolidated RFQ line produces multiple rows (grain: one
/// PR-line-to-RFQ-line link).
/// </summary>
public sealed class PrLineSourcing
{
    public Guid Id { get; private set; }
    public Guid PrLineId { get; private set; }
    public Guid RfqId { get; private set; }
    public string RfqLineCode { get; private set; } = default!;
    public decimal QtySourced { get; private set; }
    public LinkStatus LinkStatus { get; private set; } = LinkStatus.Active;
    public string? Reason { get; private set; }
    public DateTime CreatedUtc { get; private set; }
    public DateTime? ClosedUtc { get; private set; }

    private PrLineSourcing() { }

    public static PrLineSourcing Create(Guid prLineId, Guid rfqId, string rfqLineCode, decimal qtySourced, DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rfqLineCode);
        if (prLineId == Guid.Empty || rfqId == Guid.Empty)
        {
            throw new SourcingRuleException("A sourcing link requires both a PR line and an RFQ.");
        }

        if (qtySourced <= 0)
        {
            throw new SourcingRuleException("Sourced quantity must be greater than zero.");
        }

        return new PrLineSourcing
        {
            Id = Guid.CreateVersion7(),
            PrLineId = prLineId,
            RfqId = rfqId,
            RfqLineCode = rfqLineCode,
            QtySourced = qtySourced,
            CreatedUtc = nowUtc,
        };
    }

    public void MarkReturned(string? reason, DateTime nowUtc) => Close(LinkStatus.Returned, reason, nowUtc);

    public void MarkCancelled(string? reason, DateTime nowUtc) => Close(LinkStatus.Cancelled, reason, nowUtc);

    private void Close(LinkStatus status, string? reason, DateTime nowUtc)
    {
        if (LinkStatus != LinkStatus.Active)
        {
            throw new SourcingRuleException("This sourcing link is already closed.");
        }

        LinkStatus = status;
        Reason = reason;
        ClosedUtc = nowUtc;
    }
}
