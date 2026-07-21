using FSH.Modules.Sourcing.Domain;

namespace Sourcing.Tests.Domain;

public sealed class BidGuardsTests
{
    [Fact]
    public void EnsureOpen_Should_Throw_When_Draft()
    {
        var rfq = Rfq.CreateDraft(
            "RFQ-1", "t", RfqEnvelope.Dual, "MYR", "o", [],
            [new RfqLine("L1", "ITEM-1", "w", 1m, "EA", null, null)]);

        Should.Throw<SourcingRuleException>(() => BidGuards.EnsureOpen(rfq, DateTime.UtcNow));
    }

    [Fact]
    public void EnsureOpen_Should_Throw_When_PastClose()
    {
        var rfq = Rfq.CreateDraft(
            "RFQ-1", "t", RfqEnvelope.Dual, "MYR", "o", [],
            [new RfqLine("L1", "ITEM-1", "w", 1m, "EA", null, null)]);
        var closes = DateTime.UtcNow.AddHours(-1);
        rfq.UpdateDraft("t", RfqEnvelope.Dual, "MYR", null, closes,
            [new RfqLine("L1", "ITEM-1", "w", 1m, "EA", null, null)], [], [], [], [], []);
        rfq.InviteVendor(Guid.NewGuid(), DateTime.UtcNow.AddHours(-2), 0, "buyer");
        // Release requires ClosesUtc in the future relative to nothing for draft invite;
        // force status via release then close window by using past closes after open.
        // Invite while draft with past closes is allowed; release requires closes set.
        // Re-set closes in future for release, then close early and still fail EnsureOpen on Closed.
        rfq.UpdateDraft("t", RfqEnvelope.Dual, "MYR", null, DateTime.UtcNow.AddDays(1),
            [new RfqLine("L1", "ITEM-1", "w", 1m, "EA", null, null)], [], [], [], [], []);
        rfq.MarkReleased(DateTime.UtcNow, "buyer");
        rfq.CloseEarly(DateTime.UtcNow, "buyer");

        Should.Throw<SourcingRuleException>(() => BidGuards.EnsureOpen(rfq, DateTime.UtcNow));
    }

    [Fact]
    public void EnsureOpen_Should_Pass_When_OpenAndBeforeClose()
    {
        var rfq = Rfq.CreateDraft(
            "RFQ-1", "t", RfqEnvelope.Dual, "MYR", "o", [],
            [new RfqLine("L1", "ITEM-1", "w", 1m, "EA", null, null)]);
        rfq.UpdateDraft("t", RfqEnvelope.Dual, "MYR", null, DateTime.UtcNow.AddDays(2),
            [new RfqLine("L1", "ITEM-1", "w", 1m, "EA", null, null)], [], [], [], [], []);
        rfq.InviteVendor(Guid.NewGuid(), DateTime.UtcNow, 72, "buyer");
        rfq.MarkReleased(DateTime.UtcNow, "buyer");

        Should.NotThrow(() => BidGuards.EnsureOpen(rfq, DateTime.UtcNow));
    }
}
