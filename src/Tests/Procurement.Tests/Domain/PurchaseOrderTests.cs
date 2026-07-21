using FSH.Modules.Procurement.Domain;

namespace Procurement.Tests.Domain;

public sealed class PurchaseOrderTests
{
    [Fact]
    public void Issue_Then_Acknowledge_Should_AdvanceStatus()
    {
        var po = CreateDraftPo();

        po.Issue();
        po.Status.ShouldBe(PoStatus.Issued);

        po.Acknowledge();
        po.Status.ShouldBe(PoStatus.Acknowledged);
    }

    [Fact]
    public void Issue_Should_Throw_When_NotDraft()
    {
        var po = CreateDraftPo();
        po.Issue();

        Should.Throw<ProcurementRuleException>(() => po.Issue());
    }

    [Fact]
    public void RecordReceipt_Should_BePartial_Then_Received()
    {
        var po = CreateDraftPo(qty: 10m);
        po.Issue();
        po.Acknowledge();

        po.ApplyReceivedQty("L1", 4m);
        po.Status.ShouldBe(PoStatus.PartiallyReceived);

        po.ApplyReceivedQty("L1", 6m);
        po.Status.ShouldBe(PoStatus.Received);
    }

    private static PurchaseOrder CreateDraftPo(decimal qty = 10m)
    {
        var po = PurchaseOrder.Create("PO-1", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "MYR");
        po.AddLine("L1", "Widget", "EA", qty, 95m, "L1");
        return po;
    }
}
