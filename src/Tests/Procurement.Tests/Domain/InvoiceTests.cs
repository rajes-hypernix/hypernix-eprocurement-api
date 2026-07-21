using FSH.Modules.Procurement.Domain;

namespace Procurement.Tests.Domain;

public sealed class InvoiceTests
{
    [Fact]
    public void Totals_Should_ApplySstAndWhtServerSide()
    {
        var inv = Invoice.Create("INV-1", Guid.NewGuid(), null, "SUP-1", DateOnly.FromDateTime(DateTime.UtcNow), whtRate: 5m);
        inv.AddLine("L1", 10m, 600m); // subtotal 6000 → SST 480, WHT 300

        inv.Subtotal.ShouldBe(6000m);
        inv.SstAmount.ShouldBe(480m);
        inv.WhtAmount.ShouldBe(300m);
        inv.Total.ShouldBe(6180m);
    }

    [Fact]
    public void Approve_Should_Throw_When_Exception()
    {
        var inv = Invoice.Create("INV-1", Guid.NewGuid(), null, "SUP-1", null, 0m);
        inv.AddLine("L1", 1m, 10m);
        inv.MarkException("price variance");

        Should.Throw<ProcurementRuleException>(() => inv.Approve());
    }

    [Fact]
    public void ResolveException_Should_ReturnToSubmitted()
    {
        var inv = Invoice.Create("INV-1", Guid.NewGuid(), null, "SUP-1", null, 0m);
        inv.AddLine("L1", 1m, 10m);
        inv.MarkException("price variance");

        inv.ResolveException();

        inv.Status.ShouldBe(InvoiceStatus.Submitted);
        inv.ExceptionReason.ShouldBeNull();
    }

    [Fact]
    public void PriceTolerance_Should_BeTwoPercent()
    {
        Invoice.PriceTolerance.ShouldBe(0.02m);
        Invoice.SstRate.ShouldBe(0.08m);
    }
}
