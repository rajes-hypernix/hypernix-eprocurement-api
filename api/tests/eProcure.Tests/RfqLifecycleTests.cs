using eProcure.Domain;
using eProcure.Domain.Sourcing;
using FluentAssertions;
using Xunit;

namespace eProcure.Tests;

/// <summary>
/// Slice I — RFQ invitation & extension state machine (RFQ-LIFECYCLE-ADDENDUM §1.2 T1–T8, governance
/// rules G1–G5, edge cases E1–E14). Pure-domain tests through the Rfq aggregate root: every legal
/// transition, every illegal transition, and the guard rails. Time is explicit (IClock stand-in).
/// </summary>
public class RfqLifecycleTests
{
    private static readonly Guid V1 = Guid.NewGuid();
    private static readonly Guid V2 = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 6, 30, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Close = new(2026, 7, 10, 9, 0, 0, DateTimeKind.Utc);
    private const int MinHrs = 72;
    private const int MaxExt = 2;

    private static Rfq Rfq(RfqStatus status = RfqStatus.Open, DateTime? closes = null) =>
        new() { Code = "RFQ-2026-0001", Status = status, ClosesUtc = closes ?? Close, OriginalClosesUtc = closes ?? Close };

    private static RfqInvitation Invite(Rfq r, Guid v) => r.InviteVendor(v, Now, MinHrs);

    // Seed an invited vendor onto an Open RFQ whose deadline is too close for a live invite (G2) —
    // invite while Draft (G2 does not apply), then open. Used by the deadline-edge tests.
    private static Rfq OpenRfqClosingAt(DateTime closes, Guid v)
    {
        var r = Rfq(RfqStatus.Draft, closes);
        r.InviteVendor(v, Now.AddDays(-1), MinHrs);
        r.Status = RfqStatus.Open;
        return r;
    }

    // ---------- Invitations (G1/G2) & T8 re-invite ----------

    [Fact]
    public void InviteVendor_OnOpenRfq_CreatesInvitedRow()
    {
        var r = Rfq();
        var inv = Invite(r, V1);
        inv.Status.Should().Be(RfqInvitationStatus.Invited);
        inv.VendorId.Should().Be(V1);
        inv.InvitedUtc.Should().Be(Now);
        r.Invitations.Should().ContainSingle();
    }

    [Fact]
    public void InviteVendor_WhenAwarded_Throws_G1()   // G1: never between close and award
    {
        var r = Rfq(RfqStatus.Awarded);
        var act = () => Invite(r, V1);
        act.Should().Throw<DomainRuleException>().WithMessage("*Draft or Open*");
    }

    [Fact]
    public void InviteVendor_Duplicate_Throws_E5()
    {
        var r = Rfq();
        Invite(r, V1);
        var act = () => Invite(r, V1);
        act.Should().Throw<DomainRuleException>().WithMessage("*already invited*");
        r.Invitations.Should().ContainSingle();   // no duplicate row
    }

    [Fact]
    public void InviteVendor_LateAddInside72h_Throws_G2_E9()
    {
        var closeSoon = Now.AddHours(48);   // < 72h remaining
        var r = Rfq(RfqStatus.Open, closeSoon);
        var act = () => r.InviteVendor(V1, Now, MinHrs);
        act.Should().Throw<DomainRuleException>().WithMessage("*extend the deadline*");
    }

    [Fact]
    public void InviteVendor_LateAddOnDraft_IsAllowed()   // G2 only applies while Open
    {
        var r = Rfq(RfqStatus.Draft, Now.AddHours(1));
        var inv = r.InviteVendor(V1, Now, MinHrs);
        inv.Status.Should().Be(RfqInvitationStatus.Invited);
    }

    [Fact]
    public void ReInvite_RescindedVendor_SameRowReturnsToInvited_T8_E4()
    {
        var r = Rfq();
        var inv = Invite(r, V1);
        r.RescindInvitation(V1, "DUPLICATE", null, Now, vendorHasSubmittedBid: false);
        inv.Status.Should().Be(RfqInvitationStatus.Rescinded);

        var again = r.InviteVendor(V1, Now.AddHours(1), MinHrs);
        again.Should().BeSameAs(inv);                       // same row, not a duplicate
        again.Status.Should().Be(RfqInvitationStatus.Invited);
        again.RescindReasonCode.Should().BeNull();
        again.RescindedUtc.Should().BeNull();
        r.Invitations.Should().ContainSingle();
    }

    // ---------- Draft de-selection (approved boundary) ----------

    [Fact]
    public void RemoveDraftInvitation_OnDraft_RemovesInvitedRow()
    {
        var r = Rfq(RfqStatus.Draft);
        r.InviteVendor(V1, Now, MinHrs);
        r.RemoveDraftInvitation(V1);
        r.Invitations.Should().BeEmpty();
    }

    [Fact]
    public void RemoveDraftInvitation_WhenOpen_Throws()
    {
        var r = Rfq(RfqStatus.Open);
        Invite(r, V1);
        var act = () => r.RemoveDraftInvitation(V1);
        act.Should().Throw<DomainRuleException>().WithMessage("*rescinding*");
    }

    // ---------- T1 view ----------

    [Fact]
    public void MarkViewed_SetsViewedOnce_T1_E13()
    {
        var r = Rfq();
        var inv = Invite(r, V1);
        r.MarkInvitationViewed(V1, Now);
        inv.Status.Should().Be(RfqInvitationStatus.Viewed);
        inv.ViewedUtc.Should().Be(Now);

        r.MarkInvitationViewed(V1, Now.AddHours(2));         // second view
        inv.ViewedUtc.Should().Be(Now);                     // unchanged (first view only)
    }

    [Fact]
    public void MarkViewed_IsPassive_AfterIntend_DoesNotRegress()
    {
        var r = Rfq();
        Invite(r, V1);
        r.DeclareIntendToBid(V1, Now);
        r.MarkInvitationViewed(V1, Now.AddMinutes(1));
        r.Invitations[0].Status.Should().Be(RfqInvitationStatus.IntendToBid);   // not knocked back to Viewed
    }

    // ---------- T2 / T3 / T4 ----------

    [Fact]
    public void IntendToBid_FromInvited_T2()
    {
        var r = Rfq();
        Invite(r, V1);
        var inv = r.DeclareIntendToBid(V1, Now);
        inv.Status.Should().Be(RfqInvitationStatus.IntendToBid);
        inv.RespondedUtc.Should().Be(Now);
    }

    [Fact]
    public void Decline_RequiresReasonCode_G4()
    {
        var r = Rfq();
        Invite(r, V1);
        var act = () => r.DeclineInvitation(V1, "", null, Now);
        act.Should().Throw<DomainRuleException>().WithMessage("*reason code is required*");
    }

    [Fact]
    public void Decline_RecordsReasonAndNote_T3()
    {
        var r = Rfq();
        Invite(r, V1);
        var inv = r.DeclineInvitation(V1, "CAPACITY", "No slots until Q4", Now);
        inv.Status.Should().Be(RfqInvitationStatus.Declined);
        inv.DeclineReasonCode.Should().Be("CAPACITY");
        inv.DeclineNote.Should().Be("No slots until Q4");
    }

    [Fact]
    public void ReverseDecline_ClearsDecline_T4()
    {
        var r = Rfq();
        Invite(r, V1);
        r.DeclineInvitation(V1, "CAPACITY", "note", Now);
        var inv = r.DeclareIntendToBid(V1, Now.AddHours(1));   // T4 reversal
        inv.Status.Should().Be(RfqInvitationStatus.IntendToBid);
        inv.DeclineReasonCode.Should().BeNull();
    }

    [Fact]
    public void Decline_AtOneMinuteBeforeClose_IsAllowed_E1()
    {
        var closes = Now.AddMinutes(1);
        var r = OpenRfqClosingAt(closes, V1);
        var inv = r.DeclineInvitation(V1, "COMMERCIAL", null, Now);   // now < closes
        inv.Status.Should().Be(RfqInvitationStatus.Declined);
    }

    [Fact]
    public void Decline_AfterClose_WhileRowStillOpen_Throws_E2()
    {
        var closes = Now.AddHours(-1);   // deadline already passed, RFQ row still Open
        var r = Rfq(RfqStatus.Open, closes);
        // invite while there was still time (use an earlier now via a fresh rfq is overkill — invite ignores past close in Draft;
        // here we seed the invitation directly through InviteVendor on a Draft then open):
        var draft = Rfq(RfqStatus.Draft, closes);
        var inv = draft.InviteVendor(V1, Now.AddHours(-2), MinHrs);
        // move that invitation onto the open rfq
        r.Invitations.Add(inv);
        var act = () => r.DeclineInvitation(V1, "OTHER", null, Now);
        act.Should().Throw<DomainRuleException>().WithMessage("*bid window has closed*");
    }

    [Fact]
    public void Decline_WhenClosed_Throws_ProbityWindow()
    {
        var r = Rfq(RfqStatus.Closed);
        var draft = Rfq(RfqStatus.Draft);
        r.Invitations.Add(draft.InviteVendor(V1, Now, MinHrs));
        var act = () => r.DeclineInvitation(V1, "OTHER", null, Now);
        act.Should().Throw<DomainRuleException>().WithMessage("*open for bids*");
    }

    // ---------- T5 submit / E14 ----------

    [Fact]
    public void RecordBidSubmitted_FromIntend_T5()
    {
        var r = Rfq();
        Invite(r, V1);
        r.DeclareIntendToBid(V1, Now);
        var inv = r.RecordBidSubmitted(V1, Now.AddHours(1));
        inv.Status.Should().Be(RfqInvitationStatus.BidSubmitted);
    }

    [Fact]
    public void RecordBidSubmitted_WhenDeclined_Throws_E14()
    {
        var r = Rfq();
        Invite(r, V1);
        r.DeclineInvitation(V1, "CAPACITY", null, Now);
        var act = () => r.RecordBidSubmitted(V1, Now.AddHours(1));
        act.Should().Throw<DomainRuleException>().WithMessage("*Reverse your decline*");
    }

    [Fact]
    public void RecordBidSubmitted_WhenRescinded_Throws()
    {
        var r = Rfq();
        Invite(r, V1);
        r.RescindInvitation(V1, "COMPLIANCE", null, Now, false);
        var act = () => r.RecordBidSubmitted(V1, Now.AddHours(1));
        act.Should().Throw<DomainRuleException>().WithMessage("*rescinded*");
    }

    // ---------- T6 withdraw / E10 ----------

    [Fact]
    public void WithdrawBid_BackToIntend_T6()
    {
        var r = Rfq();
        Invite(r, V1);
        r.DeclareIntendToBid(V1, Now);
        r.RecordBidSubmitted(V1, Now.AddHours(1));
        var inv = r.WithdrawInvitationBid(V1, Now.AddHours(2));
        inv.Status.Should().Be(RfqInvitationStatus.IntendToBid);
    }

    [Fact]
    public void WithdrawBid_AfterClose_Throws_E10()
    {
        var closes = Now.AddHours(1);
        var r = OpenRfqClosingAt(closes, V1);
        r.DeclareIntendToBid(V1, Now);
        r.RecordBidSubmitted(V1, Now);
        var act = () => r.WithdrawInvitationBid(V1, closes.AddMinutes(1));   // after close
        act.Should().Throw<DomainRuleException>().WithMessage("*bid window has closed*");
    }

    [Fact]
    public void Bid_Withdraw_SetsWithdrawnAndDraft()
    {
        var bid = new Bid { Code = "BID-1", Submitted = true, SubmittedUtc = Now };
        bid.Withdraw(Now.AddHours(1));
        bid.Submitted.Should().BeFalse();
        bid.SavedDraft.Should().BeTrue();
        bid.WithdrawnUtc.Should().Be(Now.AddHours(1));
    }

    [Fact]
    public void Bid_Withdraw_WhenNotSubmitted_Throws()
    {
        var bid = new Bid { Code = "BID-1", Submitted = false };
        var act = () => bid.Withdraw(Now);
        act.Should().Throw<DomainRuleException>().WithMessage("*submitted bid*");
    }

    // ---------- T7 rescind / G3 / E3 ----------

    [Fact]
    public void Rescind_RequiresReasonCode_G4()
    {
        var r = Rfq();
        Invite(r, V1);
        var act = () => r.RescindInvitation(V1, "", null, Now, false);
        act.Should().Throw<DomainRuleException>().WithMessage("*rescind reason code is required*");
    }

    [Fact]
    public void Rescind_WhenVendorHasSubmittedBid_Throws_G3()
    {
        var r = Rfq();
        Invite(r, V1);
        r.DeclareIntendToBid(V1, Now);
        r.RecordBidSubmitted(V1, Now);
        var act = () => r.RescindInvitation(V1, "SCOPE_CHANGE", null, Now, vendorHasSubmittedBid: true);
        act.Should().Throw<DomainRuleException>().WithMessage("*submitted bid cannot be rescinded*");
    }

    [Fact]
    public void Rescind_AfterWithdraw_IsAllowed_E3()
    {
        // vendor submitted then withdrew → no submitted bid at this moment → rescind allowed
        var r = Rfq();
        Invite(r, V1);
        r.DeclareIntendToBid(V1, Now);
        r.RecordBidSubmitted(V1, Now);
        r.WithdrawInvitationBid(V1, Now.AddHours(1));
        var inv = r.RescindInvitation(V1, "SCOPE_CHANGE", "requirement dropped", Now.AddHours(2), vendorHasSubmittedBid: false);
        inv.Status.Should().Be(RfqInvitationStatus.Rescinded);
        inv.RescindReasonCode.Should().Be("SCOPE_CHANGE");
    }

    // ---------- Extension (G5 / E6 / E7 / E8) ----------

    [Fact]
    public void Extend_ForwardOnly_MovesCloses()
    {
        var r = Rfq();
        var newClose = Close.AddDays(3);
        r.Extend(newClose, currentExtensionCount: 0, MaxExt, Now);
        r.ClosesUtc.Should().Be(newClose);
        r.OriginalClosesUtc.Should().Be(Close);   // immutable
    }

    [Fact]
    public void Extend_Backwards_Throws_E6()
    {
        var r = Rfq();
        var act = () => r.Extend(Close.AddDays(-1), 0, MaxExt, Now);
        act.Should().Throw<DomainRuleException>().WithMessage("*forward-only*");
    }

    [Fact]
    public void Extend_ThirdWhenMaxTwo_Throws_E7()
    {
        var r = Rfq();
        var act = () => r.Extend(Close.AddDays(3), currentExtensionCount: 2, MaxExt, Now);
        act.Should().Throw<DomainRuleException>().WithMessage("*maximum of 2*");
    }

    [Fact]
    public void Extend_WhenClosed_Throws_E8()
    {
        var r = Rfq(RfqStatus.Closed);
        var act = () => r.Extend(Close.AddDays(3), 0, MaxExt, Now);
        act.Should().Throw<DomainRuleException>().WithMessage("*open RFQ can be extended*");
    }

    [Fact]
    public void Extend_AfterCloseTimePassed_Throws()
    {
        var r = Rfq(RfqStatus.Open, Now.AddHours(-1));
        var act = () => r.Extend(Now.AddDays(3), 0, MaxExt, Now);
        act.Should().Throw<DomainRuleException>().WithMessage("*already closed*");
    }
}
