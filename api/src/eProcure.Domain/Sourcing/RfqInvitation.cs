namespace eProcure.Domain.Sourcing;

/// <summary>
/// A vendor's invitation to one RFQ round (RFQ-LIFECYCLE-ADDENDUM §2.1). Replaces the old
/// <c>Rfq.InvitedVendorIds</c> delimited string. Part of the <see cref="Rfq"/> aggregate: every
/// transition goes through an <see cref="Rfq"/> root method (which owns the cross-entity guards —
/// RFQ status, deadline, submitted-bid checks), so the status setter is private and the transition
/// methods are <c>internal</c>. Append-only: once the RFQ is Open a row is never deleted; rescind is
/// a status transition, not a delete. Legal transitions are table §1.2 (T1–T8).
/// </summary>
public class RfqInvitation
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid RfqId { get; private set; }
    public Guid VendorId { get; private set; }
    public int RoundNumber { get; private set; } = 1;
    public RfqInvitationStatus Status { get; private set; } = RfqInvitationStatus.Invited;

    public string? DeclineReasonCode { get; private set; }
    public string? DeclineNote { get; private set; }
    public string? RescindReasonCode { get; private set; }
    public string? RescindNote { get; private set; }

    public DateTime InvitedUtc { get; private set; }
    public DateTime? ViewedUtc { get; private set; }
    public DateTime? RespondedUtc { get; private set; }
    public DateTime? RescindedUtc { get; private set; }

    private RfqInvitation() { }   // EF

    internal static RfqInvitation Create(Guid rfqId, Guid vendorId, int roundNumber, DateTime nowUtc) => new()
    {
        RfqId = rfqId,
        VendorId = vendorId,
        RoundNumber = roundNumber,
        Status = RfqInvitationStatus.Invited,
        InvitedUtc = nowUtc,
    };

    /// <summary>Seed/backfill only — reconstructs an invitation at a known-good status directly, bypassing
    /// the transition guards (used by the data seeder and the migration is raw-SQL). Not for request paths.</summary>
    public static RfqInvitation Seed(
        Guid rfqId, Guid vendorId, RfqInvitationStatus status, DateTime invitedUtc,
        string? declineReasonCode = null, string? rescindReasonCode = null, int roundNumber = 1) => new()
    {
        RfqId = rfqId,
        VendorId = vendorId,
        RoundNumber = roundNumber,
        Status = status,
        InvitedUtc = invitedUtc,
        DeclineReasonCode = status == RfqInvitationStatus.Declined ? declineReasonCode : null,
        RescindReasonCode = status == RfqInvitationStatus.Rescinded ? rescindReasonCode : null,
        RescindedUtc = status == RfqInvitationStatus.Rescinded ? invitedUtc : null,
        RespondedUtc = status is RfqInvitationStatus.Declined or RfqInvitationStatus.IntendToBid or RfqInvitationStatus.BidSubmitted ? invitedUtc : null,
    };

    // ---- internal transitions — the Rfq aggregate root calls these AFTER its own RFQ-level guards ----

    /// <summary>T1 — passive first view. Idempotent; legal in any RFQ state.</summary>
    internal void MarkViewed(DateTime nowUtc)
    {
        if (Status == RfqInvitationStatus.Invited) Status = RfqInvitationStatus.Viewed;
        ViewedUtc ??= nowUtc;
    }

    /// <summary>T2 (Invited/Viewed → IntendToBid) and T4 (Declined → IntendToBid, decline reversed).</summary>
    internal void ToIntendToBid(DateTime nowUtc)
    {
        if (Status is not (RfqInvitationStatus.Invited or RfqInvitationStatus.Viewed or RfqInvitationStatus.Declined))
            throw new DomainRuleException($"Cannot declare intent to bid from status {Status}.");
        var reversedDecline = Status == RfqInvitationStatus.Declined;
        Status = RfqInvitationStatus.IntendToBid;
        if (reversedDecline) { DeclineReasonCode = null; DeclineNote = null; }   // reversal clears the active decline
        RespondedUtc = nowUtc;
    }

    /// <summary>T3 — vendor declines. Reason code required (G4); free note is optional colour.</summary>
    internal void ToDeclined(string reasonCode, string? note, DateTime nowUtc)
    {
        if (Status is not (RfqInvitationStatus.Invited or RfqInvitationStatus.Viewed or RfqInvitationStatus.IntendToBid))
            throw new DomainRuleException($"Cannot decline from status {Status}.");
        if (string.IsNullOrWhiteSpace(reasonCode))
            throw new DomainRuleException("A decline reason code is required.");
        Status = RfqInvitationStatus.Declined;
        DeclineReasonCode = reasonCode;
        DeclineNote = note;
        RespondedUtc = nowUtc;
    }

    /// <summary>T5 — bid submitted. E14: a declined vendor must reverse first; a rescinded one cannot bid.</summary>
    internal void ToBidSubmitted(DateTime nowUtc)
    {
        if (Status == RfqInvitationStatus.Declined)
            throw new DomainRuleException("Reverse your decline before submitting a bid.");
        if (Status == RfqInvitationStatus.Rescinded)
            throw new DomainRuleException("This invitation has been rescinded; you can no longer bid.");
        Status = RfqInvitationStatus.BidSubmitted;
        RespondedUtc = nowUtc;
    }

    /// <summary>T6 — bid withdrawn, back to IntendToBid.</summary>
    internal void ToIntendFromBid(DateTime nowUtc)
    {
        if (Status is not RfqInvitationStatus.BidSubmitted)
            throw new DomainRuleException($"Cannot withdraw a bid from status {Status}.");
        Status = RfqInvitationStatus.IntendToBid;
        RespondedUtc = nowUtc;
    }

    /// <summary>T7 — buyer rescinds. Submitted-bid check (G3) is enforced by the Rfq root before this call.</summary>
    internal void ToRescinded(string reasonCode, string? note, DateTime nowUtc)
    {
        if (Status == RfqInvitationStatus.Rescinded)
            throw new DomainRuleException("This invitation is already rescinded.");
        if (Status == RfqInvitationStatus.BidSubmitted)
            throw new DomainRuleException("An invitation with a submitted bid cannot be rescinded.");
        if (string.IsNullOrWhiteSpace(reasonCode))
            throw new DomainRuleException("A rescind reason code is required.");
        Status = RfqInvitationStatus.Rescinded;
        RescindReasonCode = reasonCode;
        RescindNote = note;
        RescindedUtc = nowUtc;
    }

    /// <summary>T8 — buyer re-invites a rescinded vendor. Same row returns to Invited with a fresh timeline.</summary>
    internal void ReInvite(DateTime nowUtc)
    {
        if (Status is not RfqInvitationStatus.Rescinded)
            throw new DomainRuleException("Only a rescinded invitation can be re-invited.");
        Status = RfqInvitationStatus.Invited;
        RescindReasonCode = null;
        RescindNote = null;
        RescindedUtc = null;
        DeclineReasonCode = null;
        DeclineNote = null;
        ViewedUtc = null;
        RespondedUtc = null;
        InvitedUtc = nowUtc;
    }
}
