namespace eProcure.Domain.Sourcing;

/// <summary>
/// Durable provenance link from a PR line to the RFQ line it fed — the SOURCE OF TRUTH for
/// sourcing lineage (PR-MODULE-SPEC §2.3), and the spine of future spend analytics
/// (DATA-MODEL-ANALYTICS §2).
///
/// GRAIN: one (PR line → RFQ line) sourcing event. A merged RFQ line produces MULTIPLE rows
/// (one per source PR line), so consolidation never collapses provenance.
///
/// Append-only: rows are NEVER deleted. Returning/cancelling sets <see cref="LinkStatus"/> +
/// <see cref="ClosedUtc"/> + <see cref="Reason"/> (analytics §1). It crosses the PR and RFQ
/// aggregates, so it is its own entity referenced by id, not nested in either
/// (ENGINEERING-STANDARDS — aggregate boundaries).
/// </summary>
public class PrLineSourcing
{
    // HARDENING: new write surface — RowVersion concurrency token candidate (PR-MODULE-SPEC §2.6).
    public Guid Id { get; private set; } = Guid.NewGuid();      // surrogate PK (analytics §3)
    public Guid PrLineId { get; private set; }                  // -> PrLine.Id (grain key)
    public Guid RfqId { get; private set; }                     // -> Rfq.Id
    public string RfqLineCode { get; private set; } = default!; // stable code of the fed RFQ line
    public decimal QtySourced { get; private set; }             // full PR line qty in v1 (partial-ready)
    public LinkStatus LinkStatus { get; private set; } = LinkStatus.Active;
    public string? Reason { get; private set; }                 // captured on Returned/Cancelled
    public DateTime CreatedUtc { get; private set; }
    public DateTime? ClosedUtc { get; private set; }

    // EF Core
    private PrLineSourcing() { }

    /// <summary>Opens an Active sourcing link (written when an RFQ is released from the basket).</summary>
    public PrLineSourcing(Guid prLineId, Guid rfqId, string rfqLineCode, decimal qtySourced, DateTime nowUtc)
    {
        if (prLineId == Guid.Empty) throw new DomainRuleException("PrLineSourcing requires a PR line id.");
        if (rfqId == Guid.Empty) throw new DomainRuleException("PrLineSourcing requires an RFQ id.");
        if (string.IsNullOrWhiteSpace(rfqLineCode)) throw new DomainRuleException("PrLineSourcing requires an RFQ line code.");
        if (qtySourced <= 0) throw new DomainRuleException("PrLineSourcing quantity must be positive.");

        Id = Guid.NewGuid();
        PrLineId = prLineId;
        RfqId = rfqId;
        RfqLineCode = rfqLineCode;
        QtySourced = qtySourced;
        LinkStatus = LinkStatus.Active;
        CreatedUtc = nowUtc;
    }

    /// <summary>Line came back unsourced (lost / not awarded / no quotes). Append-only close.</summary>
    public void MarkReturned(string? reason, DateTime nowUtc) => Close(LinkStatus.Returned, reason, nowUtc);

    /// <summary>The whole RFQ was cancelled. Append-only close.</summary>
    public void MarkCancelled(string? reason, DateTime nowUtc) => Close(LinkStatus.Cancelled, reason, nowUtc);

    private void Close(LinkStatus to, string? reason, DateTime nowUtc)
    {
        if (LinkStatus != LinkStatus.Active)
            throw new DomainRuleException($"Sourcing link is already {LinkStatus}; it cannot be {to}.");
        LinkStatus = to;
        Reason = reason;
        ClosedUtc = nowUtc;
    }
}
