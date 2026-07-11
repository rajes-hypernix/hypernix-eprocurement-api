namespace eProcure.Domain.Sourcing;

/// <summary>
/// A purchase requisition — the aggregate root that owns its <see cref="PrLine"/>s. In
/// production these originate from NetSuite; for now they are dummy/local seed data
/// (CLAUDE.md). Lines are sourced into RFQ lines, with provenance kept in
/// <see cref="PrLineSourcing"/>.
///
/// Analytics foundation (DATA-MODEL-ANALYTICS): business dates are typed <see cref="DateOnly"/>
/// (not strings); dimensions carry a controlled code beside the display label; the header
/// <see cref="Value"/> is derived from lines; the header <see cref="HeaderStatus"/> is derived
/// from line states and recomputed on every line change.
/// </summary>
public class PurchaseRequisition
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Code { get; set; } = default!;          // PR-2026-0412 (business key)
    public string Requestor { get; set; } = "";

    // --- Dimensions: display label + controlled analytics code (conformed dimension, §4) ---
    public string Department { get; set; } = "";
    public string DepartmentCode { get; set; } = "";
    public string Location { get; set; } = "";
    public string LocationCode { get; set; } = "";
    public string Category { get; set; } = "";
    public string CategoryCode { get; set; } = "";
    public string Job { get; set; } = "";
    public string JobCode { get; set; } = "";

    public string Memo { get; set; } = "";
    public string CostCentre { get; set; } = "";
    public string? Project { get; set; }

    // --- Business dates: typed source of truth (§5) + the legacy display strings kept intact ---
    public DateOnly? RaisedOn { get; set; }               // typed source of truth (Slice H T4 retired the display strings)
    public DateOnly? RequiredOn { get; set; }
    public DateTime? SubmittedUtc { get; private set; }   // actual transition instant (Slice H T5)

    /// <summary>Legacy free-text status ("Approved") — preserved for existing readers. The
    /// analytics-grade lifecycle is <see cref="HeaderStatus"/>. Setter is private (T3); the string→enum
    /// convergence is deferred to a later slice as it fans out to RequisitionDto/web (see BACKLOG).</summary>
    public string Status { get; private set; } = "Approved";

    /// <summary>Whether the PR has been submitted (vs a portal Draft). Seeded PRs are submitted.</summary>
    public bool Submitted { get; set; } = true;

    /// <summary>Derived header lifecycle (PR-MODULE-SPEC §2.5). Stored so it round-trips for
    /// analytics; set only via <see cref="RecomputeHeaderStatus"/>.</summary>
    public PrHeaderStatus HeaderStatus { get; private set; } = PrHeaderStatus.Submitted;

    public string Currency { get; set; } = "MYR";

    /// <summary>Legacy stored aggregate — no longer the source of truth (§6). Read
    /// <see cref="DerivedValue"/> instead; this column is retained for backward compatibility.</summary>
    public decimal Value { get; set; }

    public List<PrLine> Lines { get; set; } = [];

    public DateTime CreatedUtc { get; set; }
    // HARDENING: PR header/line edits are a RowVersion concurrency-token candidate (§2.6).
    public DateTime UpdatedUtc { get; set; }

    /// <summary>Header value derived deterministically from lines (§6). Not persisted.</summary>
    public decimal DerivedValue => Lines.Sum(l => l.Qty * l.EstUnitPrice);

    /// <summary>
    /// Draft → Submitted transition. A portal-created PR must be submitted before its lines
    /// become source-eligible (PR-MODULE-SPEC §4.2). Requires at least one Open line — an
    /// empty or all-cancelled PR cannot be submitted. Rich-domain method (not a raw setter);
    /// the caller records the audit entry.
    /// </summary>
    public void Submit(DateTime nowUtc)
    {
        if (HeaderStatus != PrHeaderStatus.Draft)
            throw new DomainRuleException("Only a draft PR can be submitted.");
        if (!Lines.Any(l => l.LifecycleStatus == PrLineStatus.Open))
            throw new DomainRuleException("A PR needs at least one open line before it can be submitted.");
        Submitted = true;
        Status = "Submitted";          // keep the legacy display field in step
        UpdatedUtc = nowUtc;
        SubmittedUtc = nowUtc;
        RecomputeHeaderStatus();
    }

    /// <summary>Cancels the PR — keeps the legacy display field in step with HeaderStatus (which the
    /// caller drives to Cancelled via RecomputeHeaderStatus after cancelling the open lines).</summary>
    public void Cancel() => Status = "Cancelled";

    /// <summary>Aligns the legacy display string with the derived <see cref="HeaderStatus"/>. Used at
    /// creation, where "Submitted"/"Draft" is exactly HeaderStatus — until the deferred string→enum
    /// convergence lands (see BACKLOG).</summary>
    public void SyncLegacyStatus() => Status = HeaderStatus.ToString();

    /// <summary>TEST/SEED ONLY — sets the legacy display status directly. Never call from production
    /// service code (enforced by the ArchitectureTests source-scan).</summary>
    public PurchaseRequisition SeededAs(string status) { Status = status; return this; }

    /// <summary>
    /// Recomputes the derived header status from line states (mirrors the mockup's
    /// <c>recomputePr</c>). Call after every line transition. Idempotent.
    /// </summary>
    public void RecomputeHeaderStatus()
    {
        if (Lines.Count > 0 && Lines.All(l => l.LifecycleStatus == PrLineStatus.Cancelled))
        {
            HeaderStatus = PrHeaderStatus.Cancelled;
            return;
        }

        var hasOpenDemand = Lines.Any(l => l.LifecycleStatus is PrLineStatus.Open or PrLineStatus.InDraftRfq);
        var hasSourced = Lines.Any(l => l.LifecycleStatus is PrLineStatus.InRfq or PrLineStatus.Awarded);

        HeaderStatus = (Submitted, hasSourced, hasOpenDemand) switch
        {
            (false, false, _) => PrHeaderStatus.Draft,          // portal-created, not yet submitted
            (_, true, true) => PrHeaderStatus.PartiallySourced, // some sourced, some demand left
            (_, true, false) => PrHeaderStatus.Sourced,         // everything sourced
            _ => PrHeaderStatus.Submitted,                      // has demand, none sourced yet
        };
    }
}

/// <summary>
/// One requisition line — the demand grain (DATA-MODEL-ANALYTICS §8). Owned by
/// <see cref="PurchaseRequisition"/>; identified by a stable surrogate <see cref="Id"/> that
/// <see cref="PrLineSourcing"/> references for lineage.
///
/// State transitions are methods on this entity (rich domain): they enforce the line state
/// machine and throw <see cref="DomainRuleException"/> on an illegal transition. Each returns a
/// <see cref="PrLineTransition"/> so the calling service can write the audit entry from one
/// chokepoint. No setter lets a caller skip the state machine.
/// </summary>
public class PrLine
{
    public Guid Id { get; private set; } = Guid.NewGuid();   // grain key for facts/lineage (§7)
    public string ItemCode { get; set; } = default!;
    public string Description { get; set; } = "";
    public decimal Qty { get; set; }
    public string Uom { get; set; } = "Unit";
    public decimal EstUnitPrice { get; set; }

    /// <summary>Typed lifecycle — the stored source of truth (analytics §9, stable token).</summary>
    public PrLineStatus LifecycleStatus { get; private set; } = PrLineStatus.Open;

    /// <summary>Most recent RFQ this line was sourced into (display hint; provenance lives in
    /// <see cref="PrLineSourcing"/>).</summary>
    public string? Ref { get; set; }

    public DateTime? UpdatedUtc { get; private set; }

    // EF Core
    private PrLine() { }

    /// <summary>Factory used by seed/import. Status defaults to Open.</summary>
    public static PrLine Create(string itemCode, string description, decimal qty, string uom,
        decimal estUnitPrice, PrLineStatus status = PrLineStatus.Open, string? @ref = null) => new()
    {
        ItemCode = itemCode,
        Description = description,
        Qty = qty,
        Uom = uom,
        EstUnitPrice = estUnitPrice,
        LifecycleStatus = status,
        Ref = @ref,
    };

    // ---- transitions (the state machine; see PR-MODULE-SPEC §3) ----

    /// <summary>Open → Cancelled (terminal). Withdraws the demand. Reason captured for audit.</summary>
    public PrLineTransition Cancel(string reason, DateTime nowUtc)
    {
        Require(PrLineStatus.Open, "cancel");
        return To(PrLineStatus.Cancelled, "Line cancelled", reason, nowUtc);
    }

    /// <summary>Open → InDraftRfq. Soft reservation into a draft basket; no link written yet.</summary>
    public PrLineTransition AddToDraft(DateTime nowUtc)
    {
        Require(PrLineStatus.Open, "add to basket");
        return To(PrLineStatus.InDraftRfq, "Reserved to draft RFQ", null, nowUtc);
    }

    /// <summary>InDraftRfq → InRfq. The RFQ was released; the service writes the Active link.</summary>
    public PrLineTransition ReleaseToRfq(DateTime nowUtc)
    {
        Require(PrLineStatus.InDraftRfq, "release to RFQ");
        return To(PrLineStatus.InRfq, "Sourced into RFQ", null, nowUtc);
    }

    /// <summary>InDraftRfq → Open. The draft RFQ was abandoned; reservation released (no link).</summary>
    public PrLineTransition AbandonDraft(DateTime nowUtc)
    {
        Require(PrLineStatus.InDraftRfq, "abandon draft");
        return To(PrLineStatus.Open, "Draft reservation released", null, nowUtc);
    }

    /// <summary>InRfq → Awarded (terminal in v1). The Active link stays Active.</summary>
    public PrLineTransition MarkAwarded(DateTime nowUtc)
    {
        Require(PrLineStatus.InRfq, "award");
        return To(PrLineStatus.Awarded, "Line awarded", null, nowUtc);
    }

    /// <summary>InRfq → Open. Line lost / not awarded / RFQ cancelled; the service closes the link
    /// (Returned/Cancelled). Re-sourceable.</summary>
    public PrLineTransition ReturnFromRfq(string reason, DateTime nowUtc)
    {
        Require(PrLineStatus.InRfq, "return from RFQ");
        return To(PrLineStatus.Open, "Returned from RFQ", reason, nowUtc);
    }

    /// <summary>Open → Open. Explicitly release a returned ("no quotes") line for re-sourcing.
    /// State is unchanged; the action + reason are recorded for audit/analytics.</summary>
    public PrLineTransition ReleaseForResourcing(string reason, DateTime nowUtc)
    {
        Require(PrLineStatus.Open, "release for re-sourcing");
        UpdatedUtc = nowUtc;
        return new PrLineTransition(PrLineStatus.Open, PrLineStatus.Open, "Released for re-sourcing", reason);
    }

    /// <summary>Cancelled → Open. Re-open a cancelled line.</summary>
    public PrLineTransition Reopen(DateTime nowUtc)
    {
        Require(PrLineStatus.Cancelled, "re-open");
        return To(PrLineStatus.Open, "Line re-opened", null, nowUtc);
    }

    private void Require(PrLineStatus expected, string action)
    {
        if (LifecycleStatus != expected)
            throw new DomainRuleException(
                $"Cannot {action} a line that is {LifecycleStatus}. {Describe()}");
    }

    private string Describe() => LifecycleStatus switch
    {
        PrLineStatus.InRfq => "Locked to RFQ (live).",
        PrLineStatus.Awarded => "Awarded — PO issued.",
        PrLineStatus.Cancelled => "The line is cancelled.",
        _ => "Illegal transition.",
    };

    private PrLineTransition To(PrLineStatus to, string action, string? reason, DateTime nowUtc)
    {
        var from = LifecycleStatus;
        LifecycleStatus = to;
        UpdatedUtc = nowUtc;
        return new PrLineTransition(from, to, action, reason);
    }
}

/// <summary>The recorded result of a line transition — the single signal a service turns into an
/// <c>AuditEntry</c> (with from/to/reason typed columns) for cycle-time analytics.</summary>
public sealed record PrLineTransition(PrLineStatus From, PrLineStatus To, string Action, string? Reason);
