using System.Reflection;
using FSH.Modules.Procurement.Domain;
using FSH.Modules.Procurement.Features.v1.Statements.Internal;

namespace Procurement.Tests.Domain;

public sealed class SoaCalculatorTests
{
    [Fact]
    public void Ledger_Omits_Draft_And_Verified_PoIssued_Rows()
    {
        var vendorId = Guid.NewGuid();
        var names = new Dictionary<Guid, string> { [vendorId] = "Acme Supplies" };

        var draft = PurchaseOrder.Create("PO-D", vendorId, "MYR", PoSourceKind.Standalone);
        draft.AddLine("X", "Widget", "EA", 1m, 10m);

        var verified = PurchaseOrder.Create("PO-V", vendorId, "MYR", PoSourceKind.Standalone);
        verified.AddLine("X", "Widget", "EA", 1m, 10m);
        SetStatus(verified, PoStatus.Verified);

        var issued = PurchaseOrder.Create("PO-I", vendorId, "MYR", PoSourceKind.Standalone);
        issued.AddLine("X", "Widget", "EA", 2m, 50m);
        SetStatus(issued, PoStatus.Issued);

        var calc = new SoaCalculator([draft, verified, issued], [], [], DateTime.UtcNow, names);
        var detail = calc.ToDetail(vendorId);

        detail.VendorName.ShouldBe("Acme Supplies");
        detail.Ledger.Count.ShouldBe(1);
        detail.Ledger[0].Type.ShouldBe("PO issued");
        detail.Ledger[0].Reference.ShouldBe("PO-I");
    }

    private static void SetStatus(PurchaseOrder po, PoStatus status)
    {
        typeof(PurchaseOrder)
            .GetProperty(nameof(PurchaseOrder.Status), BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(po, status);
    }
}
