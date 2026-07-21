using FSH.Framework.Core.Domain;

namespace FSH.Modules.Sourcing.Domain;

/// <summary>
/// A request for quotation — the aggregate root for the invitation/bid-window lifecycle.
/// Owns its lines, form items, per-vendor invitations, and the append-only event log.
/// </summary>
public sealed class Rfq : AggregateRoot<Guid>
{
    private readonly List<RfqLine> _lines = [];
    private readonly List<FormItem> _formItems = [];
    private readonly List<RfqInvitation> _invitations = [];
    private readonly List<RfqEvent> _events = [];

    public string Code { get; private set; } = default!;
    public string Title { get; private set; } = string.Empty;
    public RfqEnvelope Envelope { get; private set; } = RfqEnvelope.Dual;
    public RfqStatus Status { get; private set; } = RfqStatus.Draft;
    public string Currency { get; private set; } = "MYR";
    public string? OwnerUserId { get; private set; }
    public DateTime? OpensUtc { get; private set; }
    public DateTime? ClosesUtc { get; private set; }

    /// <summary>Immutable baseline set once at release; extension never touches it.</summary>
    public DateTime? OriginalClosesUtc { get; private set; }

    public DateTime? ReleasedUtc { get; private set; }
    public DateTime? ClosedUtc { get; private set; }
    public int ExtensionCount { get; private set; }

    /// <summary>Reserved for a future multi-round flow — always 1 today.</summary>
    public int RoundNumber { get; private set; } = 1;

    public List<string> PrRefs { get; private set; } = [];
    public List<string> TechnicalSections { get; private set; } = [];
    public List<string> CommercialSections { get; private set; } = [];

    /// <summary>FSH Identity user ids assigned to score the sealed technical envelope.</summary>
    public List<string> TechnicalEvaluatorIds { get; private set; } = [];

    /// <summary>FSH Identity user ids permitted to open the sealed commercial envelope.</summary>
    public List<string> CommercialEvaluatorIds { get; private set; } = [];

    public bool TechnicalOpened { get; private set; }
    public bool TechFinalized { get; private set; }
    public bool CommercialOpened { get; private set; }

    public DateTime CreatedUtc { get; private set; }
    public DateTime UpdatedUtc { get; private set; }

    public IReadOnlyList<RfqLine> Lines => _lines;
    public IReadOnlyList<FormItem> FormItems => _formItems;
    public IReadOnlyList<RfqInvitation> Invitations => _invitations;
    public IReadOnlyList<RfqEvent> Events => _events;

    /// <summary>Single envelopes were never sealed on price; Dual only reveals pricing once the commercial envelope opens.</summary>
    public bool CommercialRevealed => Envelope == RfqEnvelope.Single || CommercialOpened;

    /// <summary>Vendors still meaningfully part of this RFQ — everyone invited except a Rescinded invitation.</summary>
    public IReadOnlyList<Guid> LiveInvitedVendorIds =>
        [.. _invitations.Where(i => i.Status != RfqInvitationStatus.Rescinded).Select(i => i.VendorId)];

    private Rfq() { }

    public static Rfq CreateDraft(
        string code,
        string? title,
        RfqEnvelope envelope,
        string currency,
        string? ownerUserId,
        IReadOnlyList<string> prRefs,
        IReadOnlyList<RfqLine> lines)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentNullException.ThrowIfNull(lines);

        var now = DateTime.UtcNow;
        var rfq = new Rfq
        {
            Id = Guid.CreateVersion7(),
            Code = code.Trim(),
            Title = title ?? string.Empty,
            Envelope = envelope,
            Currency = string.IsNullOrWhiteSpace(currency) ? "MYR" : currency,
            OwnerUserId = ownerUserId,
            CreatedUtc = now,
            UpdatedUtc = now,
        };
        rfq.PrRefs.AddRange(prRefs);
        rfq._lines.AddRange(lines);
        return rfq;
    }

    public void UpdateDraft(
        string title,
        RfqEnvelope envelope,
        string currency,
        DateTime? opensUtc,
        DateTime? closesUtc,
        IReadOnlyList<RfqLine> lines,
        IReadOnlyList<FormItem> formItems,
        IReadOnlyList<string> technicalSections,
        IReadOnlyList<string> commercialSections,
        IReadOnlyList<string> technicalEvaluatorIds,
        IReadOnlyList<string> commercialEvaluatorIds)
    {
        RequireStatus(RfqStatus.Draft, "update");
        Title = title;
        Envelope = envelope;
        Currency = currency;
        OpensUtc = opensUtc;
        ClosesUtc = closesUtc;
        _lines.Clear();
        _lines.AddRange(lines);
        _formItems.Clear();
        _formItems.AddRange(formItems);
        TechnicalSections = [.. technicalSections];
        CommercialSections = [.. commercialSections];
        TechnicalEvaluatorIds = [.. technicalEvaluatorIds];
        CommercialEvaluatorIds = [.. commercialEvaluatorIds];
        UpdatedUtc = DateTime.UtcNow;
    }

    /// <summary>Dual-only, requires Closed/Evaluation (not Draft/Open) — one-time gate.</summary>
    public void OpenTechnicalEnvelope()
    {
        if (Envelope != RfqEnvelope.Dual)
        {
            throw new SourcingRuleException("Only Dual-envelope RFQs have a separate technical envelope.");
        }

        if (Status is not (RfqStatus.Closed or RfqStatus.Evaluation))
        {
            throw new SourcingRuleException($"Cannot open the technical envelope while the RFQ is {Status}.");
        }

        if (TechnicalOpened)
        {
            throw new SourcingRuleException("The technical envelope has already been opened.");
        }

        TechnicalOpened = true;
        UpdatedUtc = DateTime.UtcNow;
    }

    /// <summary>Single: no gate beyond not Draft/Open. Dual: requires <see cref="TechFinalized"/> first (sealed-bid sequencing).</summary>
    public void OpenCommercialEnvelope()
    {
        if (Status is not (RfqStatus.Closed or RfqStatus.Evaluation))
        {
            throw new SourcingRuleException($"Cannot open the commercial envelope while the RFQ is {Status}.");
        }

        if (Envelope == RfqEnvelope.Dual && !TechFinalized)
        {
            throw new SourcingRuleException("Finalize the technical evaluation before opening the commercial envelope.");
        }

        if (CommercialOpened)
        {
            throw new SourcingRuleException("The commercial envelope has already been opened.");
        }

        CommercialOpened = true;
        UpdatedUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Requires the technical envelope open, at least one submitted bid, and every submitted
    /// vendor fully scored. <paramref name="hasSubmittedBids"/>/<paramref name="allScored"/> are
    /// supplied by the caller — Bid/TechnicalScore live in separate aggregates this domain can't
    /// query directly.
    /// </summary>
    public void FinalizeTechnical(bool hasSubmittedBids, bool allScored)
    {
        if (Envelope != RfqEnvelope.Dual)
        {
            throw new SourcingRuleException("Only Dual-envelope RFQs have a technical finalization step.");
        }

        if (!TechnicalOpened)
        {
            throw new SourcingRuleException("Open the technical envelope before finalizing.");
        }

        if (TechFinalized)
        {
            throw new SourcingRuleException("The technical evaluation has already been finalized.");
        }

        if (!hasSubmittedBids)
        {
            throw new SourcingRuleException("No submitted bids to finalize.");
        }

        if (!allScored)
        {
            throw new SourcingRuleException("Every submitted bid must be fully scored before finalizing.");
        }

        TechFinalized = true;
        UpdatedUtc = DateTime.UtcNow;
    }

    /// <summary>Draft -&gt; Open. Requires at least one invited vendor and a close date.</summary>
    public RfqEvent MarkReleased(DateTime nowUtc, string? actorUserId)
    {
        RequireStatus(RfqStatus.Draft, "release");
        if (_invitations.Count == 0)
        {
            throw new SourcingRuleException("Invite at least one vendor before releasing the RFQ.");
        }

        if (ClosesUtc is null)
        {
            throw new SourcingRuleException("Set a close date before releasing the RFQ.");
        }

        OpensUtc ??= nowUtc;
        OriginalClosesUtc ??= ClosesUtc;
        ReleasedUtc = nowUtc;
        Status = RfqStatus.Open;
        UpdatedUtc = nowUtc;
        return AppendEvent(RfqEventType.Released, nowUtc, actorUserId: actorUserId);
    }

    /// <summary>Open -&gt; Closed (early close). Does not touch the planned ClosesUtc.</summary>
    public RfqEvent CloseEarly(DateTime nowUtc, string? actorUserId)
    {
        RequireStatus(RfqStatus.Open, "close");
        Status = RfqStatus.Closed;
        ClosedUtc = nowUtc;
        UpdatedUtc = nowUtc;
        return AppendEvent(RfqEventType.Closed, nowUtc, actorUserId: actorUserId);
    }

    /// <summary>Closed -&gt; Evaluation (no-op transition mirroring the old system).</summary>
    public void MoveToEvaluationIfClosed()
    {
        if (Status == RfqStatus.Closed)
        {
            Status = RfqStatus.Evaluation;
            UpdatedUtc = DateTime.UtcNow;
        }
    }

    private static readonly RfqStatus[] Terminal = [RfqStatus.Awarded, RfqStatus.Cancelled];

    /// <summary>Any non-terminal state -&gt; Cancelled.</summary>
    public RfqEvent CancelRfq(DateTime nowUtc, string? actorUserId)
    {
        if (Terminal.Contains(Status))
        {
            throw new SourcingRuleException($"Cannot cancel an RFQ that is already {Status}.");
        }

        Status = RfqStatus.Cancelled;
        UpdatedUtc = nowUtc;
        return AppendEvent(RfqEventType.Cancelled, nowUtc, actorUserId: actorUserId);
    }

    /// <summary>
    /// G1/G2/T8: invites a vendor. Only Draft/Open. Rejects a duplicate live invite. If Open,
    /// enforces the late-invite window (remaining time before close must be &gt;=
    /// <paramref name="minRemainingHoursForLateInvite"/>). Re-invites an existing Rescinded row
    /// in place rather than creating a new one.
    /// </summary>
    public (RfqInvitation Invitation, RfqEvent Event) InviteVendor(Guid vendorId, DateTime nowUtc, int minRemainingHoursForLateInvite, string? actorUserId)
    {
        if (Status is not (RfqStatus.Draft or RfqStatus.Open))
        {
            throw new SourcingRuleException($"Cannot invite a vendor to an RFQ that is {Status}.");
        }

        var existing = _invitations.FirstOrDefault(i => i.VendorId == vendorId);
        if (existing is not null && existing.Status != RfqInvitationStatus.Rescinded)
        {
            throw new SourcingRuleException("This vendor already has a live invitation to this RFQ.");
        }

        if (Status == RfqStatus.Open && (ClosesUtc is null || (ClosesUtc.Value - nowUtc).TotalHours < minRemainingHoursForLateInvite))
        {
            throw new SourcingRuleException(
                $"A vendor can only be invited to a live RFQ at least {minRemainingHoursForLateInvite} hours before it closes — extend the deadline first.");
        }

        RfqInvitation invitation;
        if (existing is not null)
        {
            existing.ReInvite(nowUtc);
            invitation = existing;
        }
        else
        {
            invitation = RfqInvitation.Create(Id, vendorId, nowUtc);
            _invitations.Add(invitation);
        }

        UpdatedUtc = nowUtc;
        var evt = AppendEvent(RfqEventType.VendorInvited, nowUtc, vendorId: vendorId, actorUserId: actorUserId);
        return (invitation, evt);
    }

    /// <summary>Draft-only physical delete of a freshly-Invited row (workspace selection, not a fact — no event).</summary>
    public void RemoveDraftInvitation(Guid vendorId)
    {
        RequireStatus(RfqStatus.Draft, "remove an invitation from");
        _invitations.RemoveAll(i => i.VendorId == vendorId);
        UpdatedUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// G3: an invitation with a submitted bid cannot be rescinded — <paramref name="vendorHasSubmittedBid"/>
    /// is supplied by the caller since Bid is a separate aggregate this domain can't query directly.
    /// </summary>
    public RfqEvent RescindInvitation(Guid vendorId, string reasonCode, string? note, DateTime nowUtc, bool vendorHasSubmittedBid, string? actorUserId)
    {
        if (Status is not (RfqStatus.Draft or RfqStatus.Open))
        {
            throw new SourcingRuleException($"Cannot rescind an invitation on an RFQ that is {Status}.");
        }

        if (vendorHasSubmittedBid)
        {
            throw new SourcingRuleException("An invitation with a submitted bid cannot be rescinded.");
        }

        var invitation = FindInvitation(vendorId);
        invitation.ToRescinded(reasonCode, note, nowUtc);
        UpdatedUtc = nowUtc;
        return AppendEvent(RfqEventType.InvitationRescinded, nowUtc, vendorId: vendorId, actorUserId: actorUserId, reasonCode: reasonCode, reasonNote: note);
    }

    /// <summary>G5: Open-only, forward-only, future-only, capped at maxExtensions. OriginalClosesUtc untouched.</summary>
    public RfqEvent Extend(DateTime newClosesUtc, int maxExtensions, string? reasonCode, string? note, DateTime nowUtc, string? actorUserId)
    {
        RequireStatus(RfqStatus.Open, "extend");

        if (ClosesUtc is { } current && newClosesUtc <= current)
        {
            throw new SourcingRuleException("The new close date must be later than the current one.");
        }

        if (newClosesUtc <= nowUtc)
        {
            throw new SourcingRuleException("The new close date must be in the future.");
        }

        if (ExtensionCount >= maxExtensions)
        {
            throw new SourcingRuleException($"This RFQ has already been extended the maximum of {maxExtensions} time(s).");
        }

        var oldCloses = ClosesUtc;
        ClosesUtc = newClosesUtc;
        ExtensionCount++;
        UpdatedUtc = nowUtc;
        return AppendEvent(RfqEventType.Extended, nowUtc, actorUserId: actorUserId, reasonCode: reasonCode, reasonNote: note, oldClosesUtc: oldCloses, newClosesUtc: newClosesUtc);
    }

    /// <summary>T1 — passive/idempotent, any RFQ state.</summary>
    public void MarkInvitationViewed(Guid vendorId, DateTime nowUtc) => FindInvitation(vendorId).MarkViewed(nowUtc);

    /// <summary>T2/T4 — requires Open. Logs a DeclineReversed event only when reversing a prior decline.</summary>
    public RfqEvent? DeclareIntendToBid(Guid vendorId, DateTime nowUtc)
    {
        RequireStatus(RfqStatus.Open, "declare intent to bid on");
        var invitation = FindInvitation(vendorId);
        bool wasDeclined = invitation.Status == RfqInvitationStatus.Declined;
        invitation.ToIntendToBid();
        UpdatedUtc = nowUtc;
        return wasDeclined ? AppendEvent(RfqEventType.DeclineReversed, nowUtc, vendorId: vendorId) : null;
    }

    /// <summary>T3 — requires Open + before close, reason code required.</summary>
    public RfqEvent DeclineInvitation(Guid vendorId, string reasonCode, string? note, DateTime nowUtc)
    {
        RequireOpenBeforeClose(nowUtc, "decline");
        FindInvitation(vendorId).ToDeclined(reasonCode, note, nowUtc);
        UpdatedUtc = nowUtc;
        return AppendEvent(RfqEventType.VendorDeclined, nowUtc, vendorId: vendorId, reasonCode: reasonCode, reasonNote: note);
    }

    /// <summary>Any non-terminal state -&gt; Awarded, once the award is approved.</summary>
    public RfqEvent MarkAwarded(DateTime nowUtc, string? actorUserId)
    {
        if (Terminal.Contains(Status))
        {
            throw new SourcingRuleException($"Cannot award an RFQ that is already {Status}.");
        }

        Status = RfqStatus.Awarded;
        UpdatedUtc = nowUtc;
        return AppendEvent(RfqEventType.Awarded, nowUtc, actorUserId: actorUserId);
    }

    /// <summary>T5 — driven by <c>Bid.Submit</c> in the same transaction. Requires Open + before close.</summary>
    public RfqEvent RecordBidSubmitted(Guid vendorId, DateTime nowUtc, string? actorVendorUserId)
    {
        RequireOpenBeforeClose(nowUtc, "submit a bid on");
        FindInvitation(vendorId).ToBidSubmitted();
        UpdatedUtc = nowUtc;
        return AppendEvent(RfqEventType.BidSubmitted, nowUtc, vendorId: vendorId, actorVendorUserId: actorVendorUserId);
    }

    /// <summary>
    /// T6 — driven by <c>Bid.Withdraw</c> in the same transaction. No open-window guard: a vendor
    /// may formally withdraw even after close, independent of the RFQ's own status.
    /// </summary>
    public RfqEvent WithdrawInvitationBid(Guid vendorId, DateTime nowUtc, string? actorVendorUserId)
    {
        FindInvitation(vendorId).ToIntendFromBid();
        UpdatedUtc = nowUtc;
        return AppendEvent(RfqEventType.BidWithdrawn, nowUtc, vendorId: vendorId, actorVendorUserId: actorVendorUserId);
    }

    private RfqInvitation FindInvitation(Guid vendorId) =>
        _invitations.FirstOrDefault(i => i.VendorId == vendorId)
            ?? throw new SourcingRuleException("This vendor does not have an invitation to this RFQ.");

    private void RequireStatus(RfqStatus expected, string action)
    {
        if (Status != expected)
        {
            throw new SourcingRuleException($"Cannot {action} an RFQ that is {Status} (expected {expected}).");
        }
    }

    private void RequireOpenBeforeClose(DateTime nowUtc, string action)
    {
        if (Status != RfqStatus.Open || (ClosesUtc is { } close && nowUtc > close))
        {
            throw new SourcingRuleException($"Cannot {action} — bids have closed for {Code}.");
        }
    }

    private RfqEvent AppendEvent(
        RfqEventType type,
        DateTime nowUtc,
        Guid? vendorId = null,
        string? actorUserId = null,
        string? actorVendorUserId = null,
        string? reasonCode = null,
        string? reasonNote = null,
        DateTime? oldClosesUtc = null,
        DateTime? newClosesUtc = null)
    {
        var evt = RfqEvent.Create(Id, type, nowUtc, vendorId, actorUserId, actorVendorUserId, reasonCode, reasonNote, oldClosesUtc, newClosesUtc);
        _events.Add(evt);
        return evt;
    }
}
