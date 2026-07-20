namespace FSH.Modules.Sourcing.Domain;

/// <summary>
/// One PR line's demand-lifecycle state machine. A child of <see cref="PurchaseRequisition"/> but
/// its own row (not owned) since <see cref="PrLineSourcing"/> references it by id across the
/// aggregate boundary.
/// </summary>
public sealed class PrLine
{
    public Guid Id { get; private set; }
    public Guid PurchaseRequisitionId { get; private set; }
    public string ItemCode { get; private set; } = default!;
    public string Description { get; private set; } = string.Empty;
    public decimal Qty { get; private set; }
    public string Uom { get; private set; } = string.Empty;
    public decimal EstUnitPrice { get; private set; }
    public PrLineStatus LifecycleStatus { get; private set; } = PrLineStatus.Open;

    /// <summary>Most recent RFQ code this line was released to.</summary>
    public string? Ref { get; private set; }

    public DateTime? UpdatedUtc { get; private set; }

    private PrLine() { }

    internal static PrLine Create(Guid purchaseRequisitionId, string itemCode, string description, decimal qty, string uom, decimal estUnitPrice)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemCode);

        return new PrLine
        {
            Id = Guid.CreateVersion7(),
            PurchaseRequisitionId = purchaseRequisitionId,
            ItemCode = itemCode.Trim(),
            Description = description,
            Qty = qty,
            Uom = uom,
            EstUnitPrice = estUnitPrice,
        };
    }

    public PrLineTransition Cancel(string? reason, DateTime nowUtc)
    {
        Require(PrLineStatus.Open);
        return To(PrLineStatus.Cancelled, "Line cancelled", reason, nowUtc);
    }

    /// <summary>Open -&gt; InDraftRfq — basket reservation into an RFQ draft.</summary>
    public PrLineTransition AddToDraft(DateTime nowUtc)
    {
        Require(PrLineStatus.Open);
        return To(PrLineStatus.InDraftRfq, "Added to RFQ draft", null, nowUtc);
    }

    /// <summary>InDraftRfq -&gt; InRfq — on RFQ release.</summary>
    public PrLineTransition ReleaseToRfq(string rfqCode, DateTime nowUtc)
    {
        Require(PrLineStatus.InDraftRfq);
        Ref = rfqCode;
        return To(PrLineStatus.InRfq, "Released to RFQ", null, nowUtc);
    }

    /// <summary>InDraftRfq -&gt; Open — draft RFQ abandoned/cancelled before release.</summary>
    public PrLineTransition AbandonDraft(DateTime nowUtc)
    {
        Require(PrLineStatus.InDraftRfq);
        return To(PrLineStatus.Open, "Draft RFQ abandoned", null, nowUtc);
    }

    /// <summary>InRfq -&gt; Awarded (terminal).</summary>
    public PrLineTransition MarkAwarded(DateTime nowUtc)
    {
        Require(PrLineStatus.InRfq);
        return To(PrLineStatus.Awarded, "Awarded", null, nowUtc);
    }

    /// <summary>InRfq -&gt; Open — lost / not awarded / RFQ cancelled.</summary>
    public PrLineTransition ReturnFromRfq(string? reason, DateTime nowUtc)
    {
        Require(PrLineStatus.InRfq);
        return To(PrLineStatus.Open, "Returned from RFQ", reason, nowUtc);
    }

    /// <summary>Open -&gt; Open — no-op state change, records the action for a "no quotes" release.</summary>
    public PrLineTransition ReleaseForResourcing(string? reason, DateTime nowUtc)
    {
        Require(PrLineStatus.Open);
        return To(PrLineStatus.Open, "Released for re-sourcing", reason, nowUtc);
    }

    public PrLineTransition Reopen(DateTime nowUtc)
    {
        Require(PrLineStatus.Cancelled);
        return To(PrLineStatus.Open, "Line reopened", null, nowUtc);
    }

    private void Require(PrLineStatus expected)
    {
        if (LifecycleStatus != expected)
        {
            throw new SourcingRuleException($"Cannot perform this action on a line that is {LifecycleStatus} (expected {expected}).");
        }
    }

    private PrLineTransition To(PrLineStatus to, string action, string? reason, DateTime nowUtc)
    {
        var from = LifecycleStatus;
        LifecycleStatus = to;
        UpdatedUtc = nowUtc;
        return new PrLineTransition(from, to, action, reason);
    }
}

public sealed record PrLineTransition(PrLineStatus From, PrLineStatus To, string Action, string? Reason);
