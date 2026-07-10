namespace eProcure.Domain.Procurement;

public enum PoStatus { Draft, Issued, Acknowledged, PartiallyReceived, Received, Matched, Closed, Discrepancy }

/// <summary>
/// A purchase order. Generated (Draft) when an award is approved — one PO per
/// awarded vendor. NetSuite push is a stub (NsId is cosmetic). Full lifecycle
/// (issue/acknowledge/receive/match) is built in later slices.
/// </summary>
public class PurchaseOrder
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Code { get; set; } = default!;          // PO-2026-0001
    public Guid VendorId { get; set; }
    public Guid? RfqId { get; set; }
    public string? AwardCode { get; set; }
    public PoStatus Status { get; set; } = PoStatus.Draft;
    public string Currency { get; set; } = "MYR";
    public string Incoterm { get; set; } = "DDP Bintulu";
    public string? NsId { get; set; }
    public bool Acknowledged { get; set; }
    public List<PoLine> Lines { get; set; } = [];
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }

    public decimal Total => Lines.Sum(l => l.Qty * l.UnitPrice);
}

public class PoLine
{
    public string ItemCode { get; set; } = default!;
    public string Description { get; set; } = "";
    public decimal Qty { get; set; }
    public string Uom { get; set; } = "Unit";
    public decimal UnitPrice { get; set; }
    public decimal ReceivedQty { get; set; }
    public decimal InvoicedQty { get; set; }
}
