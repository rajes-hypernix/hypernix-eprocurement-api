using FSH.Modules.Procurement.Domain;

namespace Procurement.Tests.Domain;

public sealed class AsnAndGrnDomainTests
{
    [Fact]
    public void MarkReceived_Should_Throw_When_NotInTransit()
    {
        var asn = Asn.Create("ASN-1", Guid.NewGuid(), "Carrier", "TRK", null, null);
        asn.AddLine("L1", 5m, null);
        asn.MarkReceived();

        Should.Throw<ProcurementRuleException>(() => asn.MarkReceived());
    }

    [Fact]
    public void GrnLine_Should_FlagShort_When_UnderReceived()
    {
        var grn = Grn.Create("GRN-1", Guid.NewGuid(), Guid.NewGuid());
        var line = grn.AddLine("L1", expectedQty: 10m, receivedQty: 7m);

        line.Condition.ShouldBe("Short");
        line.ExpectedQty.ShouldBe(10m);
        line.ReceivedQty.ShouldBe(7m);
    }
}
