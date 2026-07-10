namespace eProcure.Domain.Sourcing;

public enum RfqEnvelope { Single, Dual }

/// <summary>
/// What a <see cref="FormTemplate"/> is for (VENDOR-ONBOARDING-SPEC §5). Lets the Forms page filter
/// onboarding question packs and the invite step offer only those. Stored as a stable string token.
/// Defaults to <see cref="Rfq"/> so every existing template keeps its current behaviour (additive).
/// </summary>
public enum FormPurpose { Rfq, Onboarding, Both }

/// <summary>RFQ lifecycle. Closing is a server timestamp (ClosesUtc), never a button.</summary>
public enum RfqStatus { Draft, Open, Closed, Evaluation, Awarded, Cancelled }

/// <summary>
/// Vendor-invitation lifecycle (RFQ-LIFECYCLE-ADDENDUM §1.2). Distinct from <see cref="RfqStatus"/> —
/// invitation state (decline / intend / rescind) lives here, never on the RFQ. Stored as a stable
/// string token. Explicit numeric values pin the persisted contract (Rescinded rows stay visible).
/// </summary>
public enum RfqInvitationStatus
{
    Invited = 0,
    Viewed = 1,
    IntendToBid = 2,
    Declined = 3,
    BidSubmitted = 4,
    Rescinded = 5,
}

/// <summary>
/// Typed, queryable RFQ business-fact log (RFQ-LIFECYCLE-ADDENDUM §2.2). Append-only; complements
/// (does not replace) the generic <c>AuditEntry</c>. Values 9/10 reserved for future
/// Amended/Reopened. Stored as a stable string token.
/// </summary>
public enum RfqEventType
{
    Released = 0,
    Extended = 1,
    VendorInvited = 2,
    InvitationRescinded = 3,
    VendorDeclined = 4,
    DeclineReversed = 5,
    BidWithdrawn = 6,
    Closed = 7,
    Cancelled = 8,
    // 9 = Amended, 10 = Reopened — reserved, not yet emitted.
}

/// <summary>
/// PR line lifecycle (PR-module). Stored as a stable string token (analytics §9) — the six
/// values are the complete persisted set. "No quotes / returned" is NOT a stored value: it is
/// DERIVED from the line being <see cref="Open"/> with its most recent
/// <see cref="PrLineSourcing"/> link in <see cref="LinkStatus.Returned"/>.
/// </summary>
public enum PrLineStatus { Open, InDraftRfq, InRfq, Awarded, Cancelled, Closed }

/// <summary>
/// Provenance-link lifecycle for <see cref="PrLineSourcing"/>. Append-only: a link is never
/// deleted — Returned/Cancelled set the status plus ClosedUtc + Reason (analytics §1).
/// </summary>
public enum LinkStatus { Active, Returned, Cancelled }

/// <summary>
/// PR header status — DERIVED from its lines via <see cref="PurchaseRequisition.RecomputeHeaderStatus"/>
/// (PR-MODULE-SPEC §2.5). Stored as a stable token so it round-trips for analytics.
/// </summary>
public enum PrHeaderStatus { Draft, Submitted, PartiallySourced, Sourced, Cancelled }

/// <summary>
/// Allowed form-item tokens, matching the prototype's QTYPES + block kinds so the
/// builder and bid form share the same vocabulary.
/// </summary>
public static class FormItemVocab
{
    // The 12 question field types.
    public static readonly IReadOnlyList<string> Types =
    [
        "short_text", "long_text", "number", "money", "percent", "list",
        "multi", "yesno", "date", "attachment", "table", "group",
    ];

    // Item kinds: a scored question, or a non-answer block (terms/instruction).
    public static readonly IReadOnlyList<string> Kinds = ["question", "terms", "instruction"];

    // The two sealed-envelope groups.
    public static readonly IReadOnlyList<string> Groups = ["technical", "commercial"];

    public static bool IsValidType(string t) => Types.Contains(t);
    public static bool IsValidKind(string k) => Kinds.Contains(k);
    public static bool IsValidGroup(string g) => Groups.Contains(g);
}
