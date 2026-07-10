using eProcure.Domain;
using eProcure.Domain.Onboarding;
using eProcure.Domain.Suppliers;
using FluentAssertions;
using Xunit;

namespace eProcure.Tests;

/// <summary>
/// Slice A — the onboarding state machine + magic-link invitation (EDGE-CASES A1–A11, F1, F4).
/// Pure-domain tests: every legal transition, the illegal-transition rejections, append-only
/// clarification, financial snapshot on submit, and token hashing.
/// </summary>
public class OnboardingLifecycleTests
{
    private static readonly DateTime T = OnboardingTestData.T;

    // ---------- Invitation (A1/A2/A9/A10, F1) ----------

    [Fact]
    public void Invitation_StoresTokenHashed_NeverRaw()   // F1
    {
        const string raw = "raw-token-abc123";
        var inv = OnboardingTestData.Invitation(rawToken: raw);

        inv.TokenHash.Should().NotBe(raw);
        inv.TokenHash.Should().HaveLength(64).And.MatchRegex("^[0-9a-f]+$");   // SHA-256 hex
        inv.Matches(raw).Should().BeTrue();
        inv.Matches("wrong-token").Should().BeFalse();
        inv.Status.Should().Be(OnboardingInvitationStatus.Sent);
        inv.ExpiresUtc.Should().Be(T.AddDays(14));
    }

    [Fact]
    public void Invitation_Open_WithValidToken_GoesOpened_AndLinksApplication()   // A2
    {
        var inv = OnboardingTestData.Invitation(rawToken: "tok");
        var appId = Guid.NewGuid();

        var t = inv.Open("tok", appId, T);

        inv.Status.Should().Be(OnboardingInvitationStatus.Opened);
        inv.ApplicationId.Should().Be(appId);
        t.From.Should().Be(OnboardingInvitationStatus.Sent);
        t.To.Should().Be(OnboardingInvitationStatus.Opened);
    }

    [Fact]
    public void Invitation_Open_WithWrongToken_IsRejected()
    {
        var inv = OnboardingTestData.Invitation(rawToken: "tok");
        var act = () => inv.Open("nope", Guid.NewGuid(), T);
        act.Should().Throw<DomainRuleException>();
        inv.Status.Should().Be(OnboardingInvitationStatus.Sent);   // no state change
    }

    [Fact]
    public void Invitation_Expired_DoesNotResolve()   // A9
    {
        var inv = OnboardingTestData.Invitation(rawToken: "tok");
        var afterExpiry = T.AddDays(15);

        inv.IsExpired(afterExpiry).Should().BeTrue();
        var act = () => inv.Open("tok", Guid.NewGuid(), afterExpiry);
        act.Should().Throw<DomainRuleException>().WithMessage("*expired*");
    }

    [Fact]
    public void Invitation_Revoked_TokenNoLongerResolves()   // A10
    {
        var inv = OnboardingTestData.Invitation(rawToken: "tok");
        inv.Revoke(T);

        inv.Status.Should().Be(OnboardingInvitationStatus.Revoked);
        var act = () => inv.Open("tok", Guid.NewGuid(), T);
        act.Should().Throw<DomainRuleException>();
    }

    // ---------- Application happy path (A2–A8) ----------

    [Fact]
    public void Application_FromInvitation_IsInvited_WithOneReviewStep()   // A1
    {
        var app = OnboardingTestData.Application(type: VendorType.NonSwec);

        app.Status.Should().Be(OnboardingStatus.Invited);
        app.Source.Should().Be(ApplicationSource.SelfService);
        app.Steps.Should().ContainSingle();                                  // workflow seam, length 1
        app.Steps[0].RequiresFinance.Should().BeTrue();                      // Non-SWEC finance sub-step
    }

    [Fact]
    public void Application_Submit_CapturesFinancialSnapshot()   // A3
    {
        var app = OnboardingTestData.Application(withFinancials: true);
        app.MarkInProgress(T);

        app.Submit(T);

        app.Status.Should().Be(OnboardingStatus.Submitted);
        app.SubmittedUtc.Should().Be(T);
        app.Financial!.Snapshots.Should().ContainSingle(s => s.Stage == FinancialSnapshotStage.AtSubmit);
        app.Financial.Snapshots[0].Band.Should().Be(FinancialBand.C);
    }

    [Fact]
    public void Application_FullReviewToApprove_CapturesDecisionSnapshot_AndRecordsStep()   // A4/A5
    {
        var app = OnboardingTestData.UnderReview(withFinancials: true);
        var vendorId = Guid.NewGuid();

        var t = app.Approve(vendorId, "u_faridah", "Faridah Yusof", T);

        app.Status.Should().Be(OnboardingStatus.Approved);
        app.PromotedVendorId.Should().Be(vendorId);
        app.DecisionUtc.Should().Be(T);
        app.Financial!.Snapshots.Should().HaveCount(2);                       // submit + decision
        app.Financial.Snapshots.Should().Contain(s => s.Stage == FinancialSnapshotStage.AtDecision);
        app.Steps[0].Outcome.Should().Be("Approved");
        app.Steps[0].DecidedByName.Should().Be("Faridah Yusof");
        t.From.Should().Be(OnboardingStatus.UnderReview);
    }

    [Fact]
    public void Application_Reject_IsTerminal_WithReason()   // A6
    {
        var app = OnboardingTestData.UnderReview();

        app.Reject("Incomplete audited accounts", "u_faridah", "Faridah Yusof", T);

        app.Status.Should().Be(OnboardingStatus.Rejected);
        app.RejectReason.Should().Be("Incomplete audited accounts");
        app.PromotedVendorId.Should().BeNull();                               // master untouched
        app.Steps[0].Outcome.Should().Be("Rejected");
    }

    [Fact]
    public void Application_Reject_WithoutReason_IsRejected()
    {
        var app = OnboardingTestData.UnderReview();
        var act = () => app.Reject("  ", "u", "U", T);
        act.Should().Throw<DomainRuleException>();
    }

    [Fact]
    public void Application_ClarificationRoundTrip_IsBatched_AppendOnly()   // A7/A8, F4
    {
        var app = OnboardingTestData.UnderReview();
        var items = new[]
        {
            new OnboardingClarificationItem("ISO 9001 certificate", "Not attached — please upload a valid certificate."),
            new OnboardingClarificationItem("Audited accounts (FY2025)", "Only FY2023–24 provided; add FY2025."),
        };

        app.RequestClarification(ClarificationDirection.BuyerToVendor, "A few items before we proceed.",
            "u_faridah", "Faridah Yusof", items, T);

        app.Status.Should().Be(OnboardingStatus.ClarificationRequested);
        app.Rounds.Should().ContainSingle();
        var round = app.Rounds[0];
        round.RoundNo.Should().Be(1);
        round.Items.Should().HaveCount(2);
        round.Status.Should().Be(ClarificationRoundStatus.Open);

        // Vendor answers the whole round at once → Resubmitted; then buyer re-reviews.
        app.Resubmit(["Certificate uploaded.", "FY2025 accounts added."], T);

        app.Status.Should().Be(OnboardingStatus.Resubmitted);
        round.Status.Should().Be(ClarificationRoundStatus.Responded);
        round.RespondedUtc.Should().Be(T);
        round.Items[0].Request.Should().Be("Not attached — please upload a valid certificate.");   // request immutable
        round.Items[0].Response.Should().Be("Certificate uploaded.");                               // response filled once

        // A second round appends (append-only) — the first is preserved.
        app.StartReview(T);
        app.RequestClarification(ClarificationDirection.BuyerToVendor, "One more.",
            "u_faridah", "Faridah Yusof", [new OnboardingClarificationItem("Bank letter", "Provide a dated letter.")], T);
        app.Rounds.Should().HaveCount(2);
        app.Rounds[0].Items[0].Response.Should().Be("Certificate uploaded.");                       // round 1 intact
    }

    [Fact]
    public void Clarification_Respond_Twice_IsRejected()   // F4 — a responded round is immutable
    {
        var app = OnboardingTestData.UnderReview();
        app.RequestClarification(ClarificationDirection.BuyerToVendor, "x", "u", "U",
            [new OnboardingClarificationItem("t", "r")], T);
        app.Resubmit(["done"], T);

        var act = () => app.Rounds[0].Respond(["again"], T);
        act.Should().Throw<DomainRuleException>();
    }

    // ---------- Illegal transitions (A11) ----------

    [Fact]
    public void Approve_AnInvitedApplication_IsRejected()   // A11
    {
        var app = OnboardingTestData.Application();
        var act = () => app.Approve(Guid.NewGuid(), "u", "U", T);
        act.Should().Throw<DomainRuleException>();
        app.Status.Should().Be(OnboardingStatus.Invited);   // unchanged
    }

    [Theory]
    [InlineData("submit")]      // Submit before InProgress
    [InlineData("startreview")] // StartReview before Submitted
    [InlineData("resubmit")]    // Resubmit with no open round
    [InlineData("reject")]      // Reject before UnderReview
    public void IllegalTransitions_FromInvited_AreRejected(string action)
    {
        var app = OnboardingTestData.Application();
        Action act = action switch
        {
            "submit" => () => app.Submit(T),
            "startreview" => () => app.StartReview(T),
            "resubmit" => () => app.Resubmit(["x"], T),
            _ => () => app.Reject("r", "u", "U", T),
        };
        act.Should().Throw<DomainRuleException>();
        app.Status.Should().Be(OnboardingStatus.Invited);
    }

    [Fact]
    public void Expire_AnInvitedApplication_GoesExpired()   // A9 (application side)
    {
        var app = OnboardingTestData.Application();
        app.Expire(T);
        app.Status.Should().Be(OnboardingStatus.Expired);

        var act = () => app.Submit(T);   // terminal — nothing further
        act.Should().Throw<DomainRuleException>();
    }
}
