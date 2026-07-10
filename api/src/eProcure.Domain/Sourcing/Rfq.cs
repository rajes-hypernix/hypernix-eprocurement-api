namespace eProcure.Domain.Sourcing;

/// <summary>
/// A request for quotation. Built from PR lines, carries the question form, invited
/// vendors and (dual envelope) the assigned evaluators. <see cref="ClosesUtc"/> is the
/// real server-side bid deadline captured at release (BUSINESS-RULES [G]).
/// </summary>
public class Rfq
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Code { get; set; } = default!;          // RFQ-2026-0001
    public string Title { get; set; } = "";
    public RfqEnvelope Envelope { get; set; } = RfqEnvelope.Dual;
    public RfqStatus Status { get; private set; } = RfqStatus.Draft;
    public string Currency { get; set; } = "MYR";
    public string? OwnerUserId { get; set; }              // buyer in charge of this RFQ

    public DateTime? OpensUtc { get; set; }
    public DateTime? ClosesUtc { get; set; }

    /// <summary>The deadline captured at release, immutable thereafter (RFQ-LIFECYCLE-ADDENDUM §1.3).
    /// <see cref="ClosesUtc"/> is the effective (possibly extended) deadline; this is the baseline for
    /// "days extended" analytics. Set once when the RFQ is released; never modified by an extension.</summary>
    public DateTime? OriginalClosesUtc { get; set; }

    /// <summary>Reserved for multi-round bidding (SOW, deferred). Always 1 in this slice.</summary>
    public int RoundNumber { get; set; } = 1;

    public List<string> PrRefs { get; set; } = [];
    // InvitedVendorIds (delimited string) retired — invited vendors are now RfqInvitation rows
    // (see Invitations below). Backfilled + dropped in migration RfqInvitationsAndEvents.
    public List<string> TechnicalEvaluatorIds { get; set; } = [];
    public List<string> CommercialEvaluatorIds { get; set; } = [];

    public bool TechnicalOpened { get; set; }
    public bool TechFinalized { get; set; }
    public bool CommercialOpened { get; set; }

    public List<RfqLine> Lines { get; set; } = [];
    public List<FormItem> FormItems { get; set; } = [];
    public List<string> TechnicalSections { get; set; } = [];
    public List<string> CommercialSections { get; set; } = [];

    /// <summary>Vendor invitations for this RFQ (RFQ-LIFECYCLE-ADDENDUM §2.1) — the source of truth that
    /// replaced <c>InvitedVendorIds</c>. Owned by this aggregate; mutated only through the governance
    /// methods below so every guard is enforced in the domain.</summary>
    public List<RfqInvitation> Invitations { get; set; } = [];

    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }

    // ===== Invitation & extension governance (RFQ-LIFECYCLE-ADDENDUM §1–§3) =====
    // Rfq is the aggregate root: these methods own the RFQ-level guards (status, deadline, submitted
    // bid) and delegate the invitation-status transition to the RfqInvitation. Rfq.Status is never
    // changed here — invitation state is not RFQ state (README rule 2).

    private RfqInvitation? FindInvitation(Guid vendorId) =>
        Invitations.FirstOrDefault(i => i.VendorId == vendorId && i.RoundNumber == RoundNumber);

    private RfqInvitation RequireInvitation(Guid vendorId) =>
        FindInvitation(vendorId) ?? throw new DomainRuleException("No invitation exists for this vendor on this RFQ.");

    private void RequireOpenForBidding()
    {
        if (Status is not RfqStatus.Open)
            throw new DomainRuleException("This action is only available while the RFQ is open for bids.");
    }

    private void RequireBeforeClose(DateTime nowUtc)
    {
        // The RFQ row may still be Open past its deadline (close is a timestamp, not a button) — use IClock.
        if (ClosesUtc is not null && nowUtc >= ClosesUtc.Value)
            throw new DomainRuleException("The RFQ bid window has closed.");
    }

    /// <summary>G1/G2 + T8. Buyer invites a vendor (Draft or Open); re-invites a rescinded row in place.</summary>
    public RfqInvitation InviteVendor(Guid vendorId, DateTime nowUtc, int minRemainingHoursForLateInvite)
    {
        if (Status is not (RfqStatus.Draft or RfqStatus.Open))
            throw new DomainRuleException("Vendors can only be added while the RFQ is Draft or Open.");

        var existing = FindInvitation(vendorId);
        if (existing is not null && existing.Status != RfqInvitationStatus.Rescinded)
            throw new DomainRuleException("This vendor is already invited to the RFQ.");

        if (Status is RfqStatus.Open)   // G2 — no late add inside the min-remaining window
        {
            if (ClosesUtc is null)
                throw new DomainRuleException("The RFQ has no close date.");
            var remaining = ClosesUtc.Value - nowUtc;
            if (remaining <= TimeSpan.Zero)
                throw new DomainRuleException("The RFQ bid window has closed.");
            if (remaining < TimeSpan.FromHours(minRemainingHoursForLateInvite))
                throw new DomainRuleException(
                    $"Less than {minRemainingHoursForLateInvite}h remain before close — extend the deadline before inviting more vendors.");
        }

        if (existing is not null)   // T8 re-invite of a rescinded vendor
        {
            existing.ReInvite(nowUtc);
            return existing;
        }

        var invitation = RfqInvitation.Create(Id, vendorId, RoundNumber, nowUtc);
        Invitations.Add(invitation);
        return invitation;
    }

    /// <summary>Draft-only de-selection (approved boundary): a not-yet-acted invitation on a Draft RFQ is
    /// a workspace selection, not a business fact, so it may be physically removed. Once Open, the only
    /// removal path is <see cref="RescindInvitation"/>.</summary>
    public void RemoveDraftInvitation(Guid vendorId)
    {
        if (Status is not RfqStatus.Draft)
            throw new DomainRuleException("Vendors can only be removed by rescinding once the RFQ is released.");
        var invitation = FindInvitation(vendorId);
        if (invitation is null) return;
        if (invitation.Status is not RfqInvitationStatus.Invited)
            throw new DomainRuleException("Only a freshly invited vendor can be removed from a draft.");
        Invitations.Remove(invitation);
    }

    /// <summary>T7 + G3/G4. <paramref name="vendorHasSubmittedBid"/> is supplied by the service (Bid is a
    /// separate aggregate).</summary>
    public RfqInvitation RescindInvitation(Guid vendorId, string reasonCode, string? note, DateTime nowUtc, bool vendorHasSubmittedBid)
    {
        if (Status is not (RfqStatus.Draft or RfqStatus.Open))
            throw new DomainRuleException("Invitations can only be rescinded while the RFQ is Draft or Open.");
        var invitation = RequireInvitation(vendorId);
        if (vendorHasSubmittedBid)
            throw new DomainRuleException("An invitation with a submitted bid cannot be rescinded.");   // G3
        invitation.ToRescinded(reasonCode, note, nowUtc);
        return invitation;
    }

    /// <summary>G5. Forward-only, Open-only, capped. <paramref name="currentExtensionCount"/> is COUNT of
    /// prior Extended events (never stored). <see cref="OriginalClosesUtc"/> is untouched.</summary>
    public void Extend(DateTime newClosesUtc, int currentExtensionCount, int maxExtensions, DateTime nowUtc)
    {
        if (Status is not RfqStatus.Open)
            throw new DomainRuleException("Only an open RFQ can be extended.");
        if (ClosesUtc is null)
            throw new DomainRuleException("The RFQ has no close date to extend.");
        if (nowUtc >= ClosesUtc.Value)
            throw new DomainRuleException("The RFQ bid window has already closed; it cannot be extended.");
        if (newClosesUtc <= ClosesUtc.Value)
            throw new DomainRuleException("A new close date must be later than the current one — extensions are forward-only.");
        if (newClosesUtc <= nowUtc)
            throw new DomainRuleException("The new close date must be in the future.");
        if (currentExtensionCount >= maxExtensions)
            throw new DomainRuleException($"This RFQ has reached the maximum of {maxExtensions} extension(s).");
        ClosesUtc = newClosesUtc;
    }

    /// <summary>T1 — vendor viewed the RFQ. Passive and idempotent; legal in any RFQ state.</summary>
    public RfqInvitation MarkInvitationViewed(Guid vendorId, DateTime nowUtc)
    {
        var invitation = RequireInvitation(vendorId);
        invitation.MarkViewed(nowUtc);
        return invitation;
    }

    /// <summary>T2 / T4 — vendor declares intent to bid (also the decline-reversal path).</summary>
    public RfqInvitation DeclareIntendToBid(Guid vendorId, DateTime nowUtc)
    {
        RequireOpenForBidding();
        var invitation = RequireInvitation(vendorId);
        invitation.ToIntendToBid(nowUtc);
        return invitation;
    }

    /// <summary>T3 — vendor declines (Open + before close; reason code required).</summary>
    public RfqInvitation DeclineInvitation(Guid vendorId, string reasonCode, string? note, DateTime nowUtc)
    {
        RequireOpenForBidding();
        RequireBeforeClose(nowUtc);   // E2
        var invitation = RequireInvitation(vendorId);
        invitation.ToDeclined(reasonCode, note, nowUtc);
        return invitation;
    }

    /// <summary>T5 — bid submitted. Bid-deadline guards live in the bid flow; this handles the invitation
    /// transition (E14: a declined vendor must reverse first).</summary>
    public RfqInvitation RecordBidSubmitted(Guid vendorId, DateTime nowUtc)
    {
        var invitation = RequireInvitation(vendorId);
        invitation.ToBidSubmitted(nowUtc);
        return invitation;
    }

    /// <summary>T6 — bid withdrawn (Open + before close), invitation returns to IntendToBid.</summary>
    public RfqInvitation WithdrawInvitationBid(Guid vendorId, DateTime nowUtc)
    {
        RequireOpenForBidding();
        RequireBeforeClose(nowUtc);   // E10
        var invitation = RequireInvitation(vendorId);
        invitation.ToIntendFromBid(nowUtc);
        return invitation;
    }

    // ===== RFQ lifecycle transitions (T3 — golden constraint: status changes only through guarded
    // domain methods). Guards + messages preserve the exact behaviour the services enforced before. =====

    /// <summary>Draft → Open. The service still checks its own release preconditions (invited vendors,
    /// close date, provenance) before calling this; the guard here is the invariant.</summary>
    public void MarkReleased()
    {
        if (Status != RfqStatus.Draft) throw new DomainRuleException($"RFQ {Code} is already released.");
        Status = RfqStatus.Open;
    }

    /// <summary>Open → Closed (early close).</summary>
    public void CloseEarly()
    {
        if (Status != RfqStatus.Open)
            throw new DomainRuleException($"Only an open RFQ can be closed early (RFQ {Code} is {Status}).");
        Status = RfqStatus.Closed;
    }

    /// <summary>Any non-terminal state → Cancelled.</summary>
    public void CancelRfq()
    {
        if (Status is RfqStatus.Awarded or RfqStatus.Cancelled)
            throw new DomainRuleException($"RFQ {Code} cannot be cancelled ({Status}).");
        Status = RfqStatus.Cancelled;
    }

    /// <summary>→ Awarded, on award approval. NOTE: the pre-Slice-G code set this with no precondition;
    /// preserved as-is (behaviour-preserving). A tighter guard (require Closed/Evaluation) is a candidate
    /// for a later slice — see the Slice G report.</summary>
    public void MarkAwarded() => Status = RfqStatus.Awarded;

    /// <summary>Closed → Evaluation when an envelope is opened; a no-op in any other state (this exactly
    /// mirrors EvaluationService's `if (Status == Closed) Status = Evaluation`).</summary>
    public void MoveToEvaluationIfClosed()
    {
        if (Status == RfqStatus.Closed) Status = RfqStatus.Evaluation;
    }

    /// <summary>TEST/SEED ONLY — sets the lifecycle status directly, bypassing the transition guards, so
    /// fixtures can start in any state. Never call from production service code (enforced by the
    /// ArchitectureTests source-scan).</summary>
    public Rfq SeededAs(RfqStatus status) { Status = status; return this; }
}

public class RfqLine
{
    /// <summary>Stable per-RFQ line code — the lineage target referenced by
    /// <see cref="PrLineSourcing.RfqLineCode"/> (PR-MODULE-SPEC §2.4). One line per item in v1,
    /// so it defaults to <see cref="ItemCode"/>; kept distinct so merged/aliased lines can carry
    /// their own stable code later without disturbing provenance.</summary>
    public string LineCode { get; set; } = "";
    public string ItemCode { get; set; } = default!;
    public string Description { get; set; } = "";
    public decimal Qty { get; set; }
    public string Uom { get; set; } = "Unit";
    public string? PrRef { get; set; }

    /// <summary>Source PR line ids this RFQ line was consolidated from (Slice C). One id for a
    /// straight source, several for a merged line. Carried from the Consolidate basket through
    /// the RFQ draft so release can write one <see cref="PrLineSourcing"/> link per source.</summary>
    public List<string> SourcePrLineIds { get; set; } = [];
}

/// <summary>A reusable question form in the form library (versioned).</summary>
public class FormTemplate
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Code { get; set; } = default!;          // FORM-2026-0001
    public string Name { get; set; } = "New form";

    /// <summary>What the template is for (SPEC §5). Defaults to <see cref="FormPurpose.Rfq"/> so existing
    /// forms are unchanged; onboarding question packs are <see cref="FormPurpose.Onboarding"/>.</summary>
    public FormPurpose Purpose { get; set; } = FormPurpose.Rfq;
    public int Version { get; set; } = 1;
    public List<FormItem> Items { get; set; } = [];
    public List<string> TechnicalSections { get; set; } = [];
    public List<string> CommercialSections { get; set; } = [];
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
