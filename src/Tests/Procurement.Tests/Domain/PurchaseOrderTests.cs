using FSH.Modules.Procurement.Domain;

namespace Procurement.Tests.Domain;

public sealed class PurchaseOrderTests
{
    [Fact]
    public void UpdateLinePrice_ChangingPrice_Should_Confirm()
    {
        var po = PurchaseOrder.Create("PO-3", Guid.NewGuid(), "MYR", PoSourceKind.Standalone);
        var line = po.AddLine("L1", "Widget", "EA", 10m, 95m);

        po.UpdateLinePrice(line.Id, 90m, priceConfirmed: false);

        line.UnitPrice.ShouldBe(90m);
        line.PriceConfirmed.ShouldBeTrue();
    }

    [Fact]
    public void UpdateLinePrice_ExplicitFlag_Should_Confirm_WithoutChangingPrice()
    {
        var po = PurchaseOrder.Create("PO-3", Guid.NewGuid(), "MYR", PoSourceKind.Standalone);
        var line = po.AddLine("L1", "Widget", "EA", 10m, 95m);

        po.UpdateLinePrice(line.Id, unitPrice: null, priceConfirmed: true);

        line.UnitPrice.ShouldBe(95m);
        line.PriceConfirmed.ShouldBeTrue();
    }

    [Fact]
    public void UpdateLinePrice_SamePriceNoFlag_Should_NotConfirm()
    {
        var po = PurchaseOrder.Create("PO-3", Guid.NewGuid(), "MYR", PoSourceKind.Standalone);
        var line = po.AddLine("L1", "Widget", "EA", 10m, 95m);

        po.UpdateLinePrice(line.Id, 95m, priceConfirmed: false);

        line.PriceConfirmed.ShouldBeFalse();
    }

    [Fact]
    public void UpdateLinePrice_Should_Throw_When_NotDraft()
    {
        var po = CreateVerifiedPo();
        var line = po.Lines[0]!;

        Should.Throw<ProcurementRuleException>(() => po.UpdateLinePrice(line.Id, 80m, priceConfirmed: false));
    }

    [Fact]
    public void Issue_Then_Acknowledge_Should_AdvanceStatus()
    {
        var po = CreateVerifiedPo();

        po.Issue();
        po.Status.ShouldBe(PoStatus.Issued);

        po.Acknowledge();
        po.Status.ShouldBe(PoStatus.Acknowledged);
    }

    [Fact]
    public void Issue_Should_Throw_When_Draft()
    {
        var po = CreateDraftPo();

        Should.Throw<ProcurementRuleException>(() => po.Issue());
    }

    [Fact]
    public void Issue_Should_Throw_When_AlreadyIssued()
    {
        var po = CreateVerifiedPo();
        po.Issue();

        Should.Throw<ProcurementRuleException>(() => po.Issue());
    }

    [Fact]
    public void Verify_Should_Throw_When_ShipToMissing()
    {
        var po = PurchaseOrder.Create("PO-2", Guid.NewGuid(), "MYR", PoSourceKind.Standalone);
        po.AddLine("L1", "Widget", "EA", 10m, 95m, priceConfirmed: true);

        Should.Throw<ProcurementRuleException>(() => po.Verify(DateTime.UtcNow));
    }

    [Fact]
    public void Verify_Should_Throw_When_PriceUnconfirmed()
    {
        var po = PurchaseOrder.Create("PO-2", Guid.NewGuid(), "MYR", PoSourceKind.Standalone);
        po.AddLine("L1", "Widget", "EA", 10m, 95m);
        po.SetShipToAdhoc("123 Test Street");

        Should.Throw<ProcurementRuleException>(() => po.Verify(DateTime.UtcNow));
    }

    [Fact]
    public void Verify_Should_Succeed_When_AllGapsResolved()
    {
        var po = CreateVerifiedPo();
        po.Status.ShouldBe(PoStatus.Verified);
    }

    [Fact]
    public void ReopenDraft_Should_ReturnToDraft()
    {
        var po = CreateVerifiedPo();

        po.ReopenDraft();
        po.Status.ShouldBe(PoStatus.Draft);
    }

    [Fact]
    public void Cancel_Should_Succeed_FromDraftOrVerified()
    {
        var draft = CreateDraftPo();
        draft.Cancel(DateTime.UtcNow);
        draft.Status.ShouldBe(PoStatus.Cancelled);

        var verified = CreateVerifiedPo();
        verified.Cancel(DateTime.UtcNow);
        verified.Status.ShouldBe(PoStatus.Cancelled);
    }

    [Fact]
    public void Cancel_Should_Throw_When_AlreadyIssued()
    {
        var po = CreateVerifiedPo();
        po.Issue();

        Should.Throw<ProcurementRuleException>(() => po.Cancel(DateTime.UtcNow));
    }

    [Fact]
    public void Close_Should_Succeed_From_Matched()
    {
        var po = CreateVerifiedPo();
        po.Issue();
        po.Acknowledge();
        po.MarkMatched();

        po.Close(DateTime.UtcNow);
        po.Status.ShouldBe(PoStatus.Closed);
    }

    [Fact]
    public void Close_Should_Throw_When_Draft()
    {
        var po = CreateDraftPo();

        Should.Throw<ProcurementRuleException>(() => po.Close(DateTime.UtcNow));
    }

    [Fact]
    public void RecordReceipt_Should_BePartial_Then_Received()
    {
        var po = CreateVerifiedPo(qty: 10m);
        po.Issue();
        po.Acknowledge();

        po.ApplyReceivedQty("L1", 4m);
        po.Status.ShouldBe(PoStatus.PartiallyReceived);

        po.ApplyReceivedQty("L1", 6m);
        po.Status.ShouldBe(PoStatus.Received);
    }

    private static PurchaseOrder CreateDraftPo(decimal qty = 10m)
    {
        var po = PurchaseOrder.Create(
            "PO-1", Guid.NewGuid(), "MYR", PoSourceKind.FromAward,
            awardId: Guid.NewGuid(), rfqId: Guid.NewGuid());
        po.AddLine("L1", "Widget", "EA", qty, 95m, rfqLineCode: "L1", priceConfirmed: true);
        po.SetShipToAdhoc("123 Test Street");
        return po;
    }

    /// <summary>A Draft PO with every VerificationGaps() requirement satisfied, then Verified.</summary>
    private static PurchaseOrder CreateVerifiedPo(decimal qty = 10m)
    {
        var po = CreateDraftPo(qty);
        po.Verify(DateTime.UtcNow);
        return po;
    }
}
