using eProcure.Domain;
using eProcure.Domain.Sourcing;
using FluentAssertions;
using Xunit;

namespace eProcure.Tests;

/// <summary>
/// Slice A — PR line state machine + derived PR header status (PR-MODULE-SPEC §2.5/§3,
/// EDGE-CASES A & B). Pure-domain tests: every legal transition and every illegal-transition
/// rejection, plus header derivation.
/// </summary>
public class PrLineLifecycleTests
{
    private static readonly DateTime T = new(2026, 6, 30, 9, 0, 0, DateTimeKind.Utc);

    private static PrLine Line(PrLineStatus status = PrLineStatus.Open) =>
        PrLine.Create("ITEM-1", "Widget", 10, "Unit", 100m, status);

    private static PurchaseRequisition Pr(bool submitted, params PrLineStatus[] lineStates)
    {
        var pr = new PurchaseRequisition { Code = "PR-2026-9001", Submitted = submitted };
        pr.Lines = [.. lineStates.Select(s => PrLine.Create("X", "x", 1, "Unit", 1m, s))];
        pr.RecomputeHeaderStatus();
        return pr;
    }

    // ---- A1: cancel an Open line ----
    [Fact]
    public void Cancel_OpenLine_GoesCancelled_AndCapturesReason()
    {
        var l = Line(PrLineStatus.Open);
        var t = l.Cancel("Duplicate of PR-2026-0423", T);

        l.LifecycleStatus.Should().Be(PrLineStatus.Cancelled);
        l.UpdatedUtc.Should().Be(T);
        t.From.Should().Be(PrLineStatus.Open);
        t.To.Should().Be(PrLineStatus.Cancelled);
        t.Reason.Should().Be("Duplicate of PR-2026-0423");
    }

    // ---- A11 / A12: cancel an InRfq / Awarded line is blocked ----
    [Theory]
    [InlineData(PrLineStatus.InRfq)]
    [InlineData(PrLineStatus.Awarded)]
    [InlineData(PrLineStatus.Cancelled)]
    public void Cancel_NonOpenLine_IsRejected(PrLineStatus status)
    {
        var act = () => Line(status).Cancel("x", T);
        act.Should().Throw<DomainRuleException>();
    }

    // ---- A3: add open line to basket (soft reservation) ----
    [Fact]
    public void AddToDraft_OpenLine_GoesInDraftRfq()
    {
        var l = Line(PrLineStatus.Open);
        l.AddToDraft(T);
        l.LifecycleStatus.Should().Be(PrLineStatus.InDraftRfq);
    }

    [Fact]
    public void AddToDraft_NonOpenLine_IsRejected() =>
        ((Action)(() => Line(PrLineStatus.InRfq).AddToDraft(T))).Should().Throw<DomainRuleException>();

    // ---- A5: release the draft -> InRfq ----
    [Fact]
    public void ReleaseToRfq_FromDraft_GoesInRfq()
    {
        var l = Line(PrLineStatus.InDraftRfq);
        l.ReleaseToRfq(T);
        l.LifecycleStatus.Should().Be(PrLineStatus.InRfq);
    }

    [Fact]
    public void ReleaseToRfq_FromOpen_IsRejected() =>
        ((Action)(() => Line(PrLineStatus.Open).ReleaseToRfq(T))).Should().Throw<DomainRuleException>();

    // ---- A4: abandon draft -> Open ----
    [Fact]
    public void AbandonDraft_FromDraft_GoesOpen()
    {
        var l = Line(PrLineStatus.InDraftRfq);
        l.AbandonDraft(T);
        l.LifecycleStatus.Should().Be(PrLineStatus.Open);
    }

    // ---- A6: award win -> Awarded ----
    [Fact]
    public void MarkAwarded_FromInRfq_GoesAwarded()
    {
        var l = Line(PrLineStatus.InRfq);
        l.MarkAwarded(T);
        l.LifecycleStatus.Should().Be(PrLineStatus.Awarded);
    }

    [Fact]
    public void MarkAwarded_FromOpen_IsRejected() =>
        ((Action)(() => Line(PrLineStatus.Open).MarkAwarded(T))).Should().Throw<DomainRuleException>();

    // ---- A7 / A9: line lost / RFQ cancelled -> returns to Open with reason ----
    [Fact]
    public void ReturnFromRfq_FromInRfq_GoesOpen_WithReason()
    {
        var l = Line(PrLineStatus.InRfq);
        var t = l.ReturnFromRfq("not awarded / residual", T);
        l.LifecycleStatus.Should().Be(PrLineStatus.Open);
        t.Reason.Should().Be("not awarded / residual");
    }

    [Fact]
    public void ReturnFromRfq_FromOpen_IsRejected() =>
        ((Action)(() => Line(PrLineStatus.Open).ReturnFromRfq("x", T))).Should().Throw<DomainRuleException>();

    // ---- A10: release a returned (Open) line for re-sourcing — stays Open, reason captured ----
    [Fact]
    public void ReleaseForResourcing_OpenLine_StaysOpen_AndRecordsReason()
    {
        var l = Line(PrLineStatus.Open);
        var t = l.ReleaseForResourcing("No vendor quoted this line", T);
        l.LifecycleStatus.Should().Be(PrLineStatus.Open);
        t.From.Should().Be(PrLineStatus.Open);
        t.To.Should().Be(PrLineStatus.Open);
        t.Reason.Should().Be("No vendor quoted this line");
    }

    // ---- A2: re-open a cancelled line ----
    [Fact]
    public void Reopen_CancelledLine_GoesOpen()
    {
        var l = Line(PrLineStatus.Cancelled);
        l.Reopen(T);
        l.LifecycleStatus.Should().Be(PrLineStatus.Open);
    }

    [Fact]
    public void Reopen_NonCancelledLine_IsRejected() =>
        ((Action)(() => Line(PrLineStatus.Open).Reopen(T))).Should().Throw<DomainRuleException>();

    // ============ B. Derived PR header status ============

    [Fact] // B1
    public void Header_AllCancelled_IsCancelled() =>
        Pr(true, PrLineStatus.Cancelled, PrLineStatus.Cancelled).HeaderStatus.Should().Be(PrHeaderStatus.Cancelled);

    [Fact] // B2
    public void Header_SomeOpenSomeSourced_IsPartiallySourced() =>
        Pr(true, PrLineStatus.Open, PrLineStatus.InRfq).HeaderStatus.Should().Be(PrHeaderStatus.PartiallySourced);

    [Fact] // B3
    public void Header_NoOpenSomeSourced_IsSourced() =>
        Pr(true, PrLineStatus.InRfq, PrLineStatus.Awarded).HeaderStatus.Should().Be(PrHeaderStatus.Sourced);

    [Fact] // B4
    public void Header_HasOpenNoneSourced_Submitted_IsSubmitted() =>
        Pr(true, PrLineStatus.Open, PrLineStatus.Open).HeaderStatus.Should().Be(PrHeaderStatus.Submitted);

    [Fact] // B5
    public void Header_PortalCreatedNotSubmitted_IsDraft() =>
        Pr(false, PrLineStatus.Open).HeaderStatus.Should().Be(PrHeaderStatus.Draft);

    [Fact] // B6
    public void Header_ReopeningLineInCancelledPr_FlipsOffCancelled()
    {
        var pr = Pr(true, PrLineStatus.Cancelled, PrLineStatus.Cancelled);
        pr.HeaderStatus.Should().Be(PrHeaderStatus.Cancelled);

        pr.Lines[0].Reopen(T);
        pr.RecomputeHeaderStatus();

        pr.HeaderStatus.Should().NotBe(PrHeaderStatus.Cancelled);
        pr.HeaderStatus.Should().Be(PrHeaderStatus.Submitted);
    }

    // ---- Bug 3: Draft → Submitted transition ----
    [Fact]
    public void Submit_DraftWithOpenLine_GoesSubmitted()
    {
        var pr = new PurchaseRequisition { Code = "PR-1", Submitted = false, Lines = [PrLine.Create("A", "a", 1, "Unit", 1m)] };
        pr.RecomputeHeaderStatus();
        pr.HeaderStatus.Should().Be(PrHeaderStatus.Draft);

        pr.Submit(T);

        pr.HeaderStatus.Should().Be(PrHeaderStatus.Submitted);
        pr.Submitted.Should().BeTrue();
    }

    [Fact]
    public void Submit_NonDraftPr_IsRejected()
    {
        var pr = new PurchaseRequisition { Code = "PR-1", Submitted = true, Lines = [PrLine.Create("A", "a", 1, "Unit", 1m)] };
        pr.RecomputeHeaderStatus();   // Submitted
        ((Action)(() => pr.Submit(T))).Should().Throw<DomainRuleException>();
    }

    [Fact]
    public void Submit_DraftWithNoOpenLine_IsRejected()
    {
        var pr = new PurchaseRequisition { Code = "PR-1", Submitted = false, Lines = [] };
        pr.RecomputeHeaderStatus();   // Draft (no lines)
        ((Action)(() => pr.Submit(T))).Should().Throw<DomainRuleException>();
    }

    [Fact]
    public void DerivedValue_SumsLines()
    {
        var pr = new PurchaseRequisition
        {
            Code = "PR-2026-9002",
            Lines = [PrLine.Create("A", "a", 4, "Unit", 38500m), PrLine.Create("B", "b", 4, "Unit", 7500m)],
        };
        pr.DerivedValue.Should().Be(184000m);
    }

    // ============ E3. Provenance links are append-only ============

    [Fact]
    public void SourcingLink_MarkReturned_RetainsRowWithStatusClosedUtcAndReason()
    {
        var link = new PrLineSourcing(Guid.NewGuid(), Guid.NewGuid(), "ITEM-1", 10m, T);
        link.LinkStatus.Should().Be(LinkStatus.Active);

        link.MarkReturned("not awarded / residual", T.AddDays(1));

        link.LinkStatus.Should().Be(LinkStatus.Returned);
        link.ClosedUtc.Should().Be(T.AddDays(1));
        link.Reason.Should().Be("not awarded / residual");
    }

    [Fact]
    public void SourcingLink_CannotBeClosedTwice()
    {
        var link = new PrLineSourcing(Guid.NewGuid(), Guid.NewGuid(), "ITEM-1", 10m, T);
        link.MarkReturned("first", T);
        var act = () => link.MarkCancelled("second", T);
        act.Should().Throw<DomainRuleException>();
    }
}
