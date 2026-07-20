namespace FSH.Modules.Sourcing.Domain;

/// <summary>
/// One vendor's invitation to an RFQ — its own lifecycle, independent of <see cref="Rfq.Status"/>.
/// Transitions are internal: only <see cref="Rfq"/> may call them, since several require
/// cross-invitation/cross-aggregate guards (e.g. late-invite window, submitted-bid check) that
/// only the aggregate root (or the handler, via a passed-in flag) can evaluate.
/// </summary>
public sealed class RfqInvitation
{
    public Guid Id { get; private set; }
    public Guid RfqId { get; private set; }

    /// <summary>Bare reference into Modules.Suppliers — no cross-schema FK; validated via GetVendorByIdQuery at write time.</summary>
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

    private RfqInvitation() { }

    internal static RfqInvitation Create(Guid rfqId, Guid vendorId, DateTime nowUtc) => new()
    {
        Id = Guid.CreateVersion7(),
        RfqId = rfqId,
        VendorId = vendorId,
        InvitedUtc = nowUtc,
    };

    /// <summary>T1 — idempotent.</summary>
    internal void MarkViewed(DateTime nowUtc)
    {
        ViewedUtc ??= nowUtc;
        if (Status == RfqInvitationStatus.Invited)
        {
            Status = RfqInvitationStatus.Viewed;
        }
    }

    /// <summary>T2/T4 — from Invited/Viewed/Declined. Clears decline fields if reversing a decline.</summary>
    internal void ToIntendToBid()
    {
        if (Status is not (RfqInvitationStatus.Invited or RfqInvitationStatus.Viewed or RfqInvitationStatus.Declined))
        {
            throw new SourcingRuleException($"Cannot declare intent to bid from status {Status}.");
        }

        Status = RfqInvitationStatus.IntendToBid;
        DeclineReasonCode = null;
        DeclineNote = null;
    }

    /// <summary>T3 — from Invited/Viewed/IntendToBid only; reason code required.</summary>
    internal void ToDeclined(string reasonCode, string? note, DateTime nowUtc)
    {
        if (Status is not (RfqInvitationStatus.Invited or RfqInvitationStatus.Viewed or RfqInvitationStatus.IntendToBid))
        {
            throw new SourcingRuleException($"Cannot decline from status {Status}.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);
        Status = RfqInvitationStatus.Declined;
        DeclineReasonCode = reasonCode;
        DeclineNote = note;
        RespondedUtc = nowUtc;
    }

    /// <summary>T5.</summary>
    internal void ToBidSubmitted()
    {
        if (Status == RfqInvitationStatus.Declined)
        {
            throw new SourcingRuleException("Reverse your decline before submitting a bid.");
        }

        if (Status == RfqInvitationStatus.Rescinded)
        {
            throw new SourcingRuleException("This invitation was rescinded — you can no longer bid.");
        }

        Status = RfqInvitationStatus.BidSubmitted;
    }

    /// <summary>T6 — only from BidSubmitted.</summary>
    internal void ToIntendFromBid()
    {
        if (Status != RfqInvitationStatus.BidSubmitted)
        {
            throw new SourcingRuleException($"Cannot withdraw a bid from status {Status}.");
        }

        Status = RfqInvitationStatus.IntendToBid;
    }

    /// <summary>T7 — reason code required. Callers must ensure the vendor has no submitted bid before calling this.</summary>
    internal void ToRescinded(string reasonCode, string? note, DateTime nowUtc)
    {
        if (Status == RfqInvitationStatus.Rescinded)
        {
            throw new SourcingRuleException("This invitation has already been rescinded.");
        }

        if (Status == RfqInvitationStatus.BidSubmitted)
        {
            throw new SourcingRuleException("An invitation with a submitted bid cannot be rescinded.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);
        Status = RfqInvitationStatus.Rescinded;
        RescindReasonCode = reasonCode;
        RescindNote = note;
        RescindedUtc = nowUtc;
    }

    /// <summary>T8 — only from Rescinded; resets all fields and returns to Invited with a fresh InvitedUtc.</summary>
    internal void ReInvite(DateTime nowUtc)
    {
        if (Status != RfqInvitationStatus.Rescinded)
        {
            throw new SourcingRuleException($"Cannot re-invite from status {Status}.");
        }

        Status = RfqInvitationStatus.Invited;
        InvitedUtc = nowUtc;
        ViewedUtc = null;
        RespondedUtc = null;
        RescindedUtc = null;
        DeclineReasonCode = null;
        DeclineNote = null;
        RescindReasonCode = null;
        RescindNote = null;
    }
}
