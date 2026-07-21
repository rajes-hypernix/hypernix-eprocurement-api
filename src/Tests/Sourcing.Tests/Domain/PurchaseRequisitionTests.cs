using FSH.Modules.Sourcing.Domain;

namespace Sourcing.Tests.Domain;

public sealed class PurchaseRequisitionTests
{
    [Fact]
    public void Cancel_Should_Throw_When_LineIsInRfq()
    {
        var pr = CreateSubmittedPr();
        var line = pr.Lines[0];
        pr.ReserveLine(line.Id, DateTime.UtcNow);
        pr.ReleaseLineToRfq(line.Id, "RFQ-1", DateTime.UtcNow);

        var ex = Should.Throw<SourcingRuleException>(() => pr.Cancel(DateTime.UtcNow));
        ex.Message.ShouldContain("Cannot cancel a PR while a line is in an RFQ");
    }

    [Fact]
    public void Cancel_Should_Throw_When_LineIsAwarded()
    {
        var pr = CreateSubmittedPr();
        var line = pr.Lines[0];
        pr.ReserveLine(line.Id, DateTime.UtcNow);
        pr.ReleaseLineToRfq(line.Id, "RFQ-1", DateTime.UtcNow);
        pr.MarkLineAwarded(line.Id, DateTime.UtcNow);

        Should.Throw<SourcingRuleException>(() => pr.Cancel(DateTime.UtcNow));
    }

    [Fact]
    public void Cancel_Should_Succeed_When_LinesAreOpenOrInDraftRfq()
    {
        var pr = CreateSubmittedPr();
        pr.ReserveLine(pr.Lines[0].Id, DateTime.UtcNow);

        pr.Cancel(DateTime.UtcNow);

        pr.HeaderStatus.ShouldBe(PrHeaderStatus.Cancelled);
        pr.Lines[0].LifecycleStatus.ShouldBe(PrLineStatus.Cancelled);
    }

    [Fact]
    public void Submit_Should_Throw_When_NoOpenLines()
    {
        var pr = PurchaseRequisition.Create(
            "PR-1", "Alice", "Ops", null, "HQ", null, "IT", null, "J1", null,
            "memo", "CC1", null, null, null, null, "MYR");

        Should.Throw<SourcingRuleException>(() => pr.Submit(DateTime.UtcNow));
    }

    [Fact]
    public void MarkLineAwarded_Should_SettleLine_And_DeriveHeader()
    {
        var pr = CreateSubmittedPr();
        var line = pr.Lines[0];
        pr.ReserveLine(line.Id, DateTime.UtcNow);
        pr.ReleaseLineToRfq(line.Id, "RFQ-1", DateTime.UtcNow);

        pr.MarkLineAwarded(line.Id, DateTime.UtcNow);

        line.LifecycleStatus.ShouldBe(PrLineStatus.Awarded);
        pr.HeaderStatus.ShouldBe(PrHeaderStatus.Sourced);
    }

    private static PurchaseRequisition CreateSubmittedPr()
    {
        var pr = PurchaseRequisition.Create(
            "PR-1", "Alice", "Ops", null, "HQ", null, "IT", null, "J1", null,
            "memo", "CC1", null, null, null, null, "MYR");
        pr.AddLine("ITEM-1", "Widget", 10m, "EA", 100m);
        pr.Submit(DateTime.UtcNow);
        return pr;
    }
}
