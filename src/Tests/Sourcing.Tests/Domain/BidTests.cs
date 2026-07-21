using FSH.Modules.Sourcing.Domain;

namespace Sourcing.Tests.Domain;

public sealed class BidTests
{
    [Fact]
    public void Submit_Should_Throw_When_NoPricedBiddingLine()
    {
        var bid = Bid.CreateDraft("BID-1", Guid.NewGuid(), Guid.NewGuid());
        bid.SaveDraft("lead", "12m", [new BidLine("ITEM-1", bidding: false, price: 0m, qty: 1m, partial: false, altItem: null)], [], [], DateTime.UtcNow);

        Should.Throw<SourcingRuleException>(() => bid.Submit(DateTime.UtcNow));
    }

    [Fact]
    public void Submit_Should_Succeed_When_AtLeastOnePricedLine()
    {
        var bid = Bid.CreateDraft("BID-1", Guid.NewGuid(), Guid.NewGuid());
        bid.SaveDraft("lead", "12m", [new BidLine("ITEM-1", bidding: true, price: 95m, qty: 10m, partial: false, altItem: null)], [], [], DateTime.UtcNow);

        bid.Submit(DateTime.UtcNow);

        bid.Submitted.ShouldBeTrue();
        bid.SubmittedUtc.ShouldNotBeNull();
    }

    [Fact]
    public void SaveDraft_Should_Throw_After_Submit_Until_Withdrawn()
    {
        var bid = Bid.CreateDraft("BID-1", Guid.NewGuid(), Guid.NewGuid());
        bid.SaveDraft("lead", null, [new BidLine("ITEM-1", true, 10m, 1m, false, null)], [], [], DateTime.UtcNow);
        bid.Submit(DateTime.UtcNow);

        Should.Throw<SourcingRuleException>(() =>
            bid.SaveDraft("x", null, [new BidLine("ITEM-1", true, 11m, 1m, false, null)], [], [], DateTime.UtcNow));
    }

    [Fact]
    public void Withdraw_Should_Allow_Edit_Again()
    {
        var bid = Bid.CreateDraft("BID-1", Guid.NewGuid(), Guid.NewGuid());
        bid.SaveDraft("lead", null, [new BidLine("ITEM-1", true, 10m, 1m, false, null)], [], [], DateTime.UtcNow);
        bid.Submit(DateTime.UtcNow);

        bid.Withdraw(DateTime.UtcNow);
        bid.SaveDraft("lead2", null, [new BidLine("ITEM-1", true, 12m, 1m, false, null)], [], [], DateTime.UtcNow);

        bid.Submitted.ShouldBeFalse();
        bid.Lead.ShouldBe("lead2");
    }

    [Fact]
    public void Withdraw_Should_Throw_When_NotSubmitted()
    {
        var bid = Bid.CreateDraft("BID-1", Guid.NewGuid(), Guid.NewGuid());

        Should.Throw<SourcingRuleException>(() => bid.Withdraw(DateTime.UtcNow));
    }
}
