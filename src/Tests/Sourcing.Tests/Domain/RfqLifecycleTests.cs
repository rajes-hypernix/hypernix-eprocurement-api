using FSH.Modules.Sourcing.Domain;

namespace Sourcing.Tests.Domain;

public sealed class RfqLifecycleTests
{
    [Fact]
    public void MarkReleased_Should_Throw_When_NoInvitations()
    {
        var rfq = CreateDraft(closesUtc: DateTime.UtcNow.AddDays(7));

        Should.Throw<SourcingRuleException>(() => rfq.MarkReleased(DateTime.UtcNow, "buyer"));
    }

    [Fact]
    public void MarkReleased_Should_Throw_When_OnlyRescindedInvitations()
    {
        var vendorId = Guid.NewGuid();
        var rfq = CreateDraft(closesUtc: DateTime.UtcNow.AddDays(7));
        rfq.InviteVendor(vendorId, DateTime.UtcNow, 72, "buyer");
        rfq.RescindInvitation(vendorId, "OTHER", null, DateTime.UtcNow, vendorHasSubmittedBid: false, "buyer");

        Should.Throw<SourcingRuleException>(() => rfq.MarkReleased(DateTime.UtcNow, "buyer"))
            .Message.ShouldContain("Invite at least one vendor");
    }

    [Fact]
    public void MarkReleased_Should_Throw_When_NoCloseDate()
    {
        var rfq = CreateDraft(closesUtc: null);
        rfq.InviteVendor(Guid.NewGuid(), DateTime.UtcNow, minRemainingHoursForLateInvite: 72, "buyer");

        Should.Throw<SourcingRuleException>(() => rfq.MarkReleased(DateTime.UtcNow, "buyer"));
    }

    [Fact]
    public void MarkReleased_Should_Open_When_InviteAndClosePresent()
    {
        var rfq = CreateDraft(closesUtc: DateTime.UtcNow.AddDays(7));
        rfq.InviteVendor(Guid.NewGuid(), DateTime.UtcNow, 72, "buyer");

        rfq.MarkReleased(DateTime.UtcNow, "buyer");

        rfq.Status.ShouldBe(RfqStatus.Open);
        rfq.OriginalClosesUtc.ShouldNotBeNull();
        rfq.Events.ShouldContain(e => e.EventType == RfqEventType.Released);
    }

    [Fact]
    public void InviteVendor_Should_Throw_On_DuplicateLiveInvite()
    {
        var vendorId = Guid.NewGuid();
        var rfq = CreateDraft(closesUtc: DateTime.UtcNow.AddDays(7));
        rfq.InviteVendor(vendorId, DateTime.UtcNow, 72, "buyer");

        Should.Throw<SourcingRuleException>(() => rfq.InviteVendor(vendorId, DateTime.UtcNow, 72, "buyer"));
    }

    [Fact]
    public void OpenTechnicalEnvelope_Should_Require_ClosedOrEvaluation()
    {
        var rfq = CreateReleased();

        Should.Throw<SourcingRuleException>(() => rfq.OpenTechnicalEnvelope());
    }

    [Fact]
    public void OpenCommercialEnvelope_Dual_Should_Require_TechFinalized()
    {
        var rfq = CreateReleased();
        rfq.CloseEarly(DateTime.UtcNow, "buyer");
        rfq.OpenTechnicalEnvelope();

        Should.Throw<SourcingRuleException>(() => rfq.OpenCommercialEnvelope())
            .Message.ShouldContain("Finalize the technical evaluation");
    }

    [Fact]
    public void FinalizeTechnical_Should_Require_AllScored()
    {
        var rfq = CreateReleased();
        rfq.CloseEarly(DateTime.UtcNow, "buyer");
        rfq.OpenTechnicalEnvelope();

        Should.Throw<SourcingRuleException>(() => rfq.FinalizeTechnical(hasSubmittedBids: true, allScored: false));
    }

    [Fact]
    public void DualEnvelope_HappyPath_Flags_Should_Sequence()
    {
        var rfq = CreateReleased();
        rfq.CloseEarly(DateTime.UtcNow, "buyer");
        rfq.OpenTechnicalEnvelope();
        rfq.FinalizeTechnical(hasSubmittedBids: true, allScored: true);
        rfq.OpenCommercialEnvelope();

        rfq.TechnicalOpened.ShouldBeTrue();
        rfq.TechFinalized.ShouldBeTrue();
        rfq.CommercialOpened.ShouldBeTrue();
        rfq.CommercialRevealed.ShouldBeTrue();
    }

    private static Rfq CreateDraft(DateTime? closesUtc)
    {
        var rfq = Rfq.CreateDraft(
            "RFQ-1",
            "Test",
            RfqEnvelope.Dual,
            "MYR",
            "owner",
            ["PR-1"],
            [new RfqLine("L1", "ITEM-1", "Widget", 10m, "EA", "PR-1", null)]);

        if (closesUtc is not null)
        {
            rfq.UpdateDraft(
                "Test",
                RfqEnvelope.Dual,
                "MYR",
                opensUtc: null,
                closesUtc,
                [new RfqLine("L1", "ITEM-1", "Widget", 10m, "EA", "PR-1", null)],
                [],
                [],
                [],
                [],
                []);
        }

        return rfq;
    }

    private static Rfq CreateReleased()
    {
        var rfq = CreateDraft(closesUtc: DateTime.UtcNow.AddDays(7));
        rfq.InviteVendor(Guid.NewGuid(), DateTime.UtcNow, 72, "buyer");
        rfq.MarkReleased(DateTime.UtcNow, "buyer");
        return rfq;
    }
}
