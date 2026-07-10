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
    public PoStatus Status { get; private set; } = PoStatus.Draft;
    public string Currency { get; set; } = "MYR";
    public string Incoterm { get; set; } = "DDP Bintulu";
    public string? NsId { get; set; }
    public bool Acknowledged { get; set; }
    public List<PoLine> Lines { get; set; } = [];
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }

    public decimal Total => Lines.Sum(l => l.Qty * l.UnitPrice);

    // ===== Lifecycle transitions (T3). Guards + messages preserve prior service behaviour. =====

    /// <summary>Draft → Issued.</summary>
    public void Issue()
    {
        if (Status != PoStatus.Draft) throw new DomainRuleException($"PO {Code} has already been issued.");
        Status = PoStatus.Issued;
    }

    /// <summary>Issued → Acknowledged.</summary>
    public void Acknowledge()
    {
        if (Status != PoStatus.Issued) throw new DomainRuleException($"PO {Code} must be Issued before it can be acknowledged.");
        Status = PoStatus.Acknowledged;
    }

    /// <summary>Records a goods receipt: Received when everything has arrived, else PartiallyReceived.
    /// The delivery service decides applicability (only Issued/Acknowledged/PartiallyReceived POs).</summary>
    public void RecordReceipt(bool allReceived) => Status = allReceived ? PoStatus.Received : PoStatus.PartiallyReceived;

    /// <summary>→ Discrepancy, set by the invoice flow on a price variance against a live PO.</summary>
    public void MarkDiscrepancy() => Status = PoStatus.Discrepancy;

    /// <summary>→ Matched, set by the invoice flow once the 3-way match is fully satisfied.</summary>
    public void MarkMatched() => Status = PoStatus.Matched;

    /// <summary>TEST/SEED ONLY — sets the lifecycle status directly, bypassing the transition guards.
    /// Never call from production service code (enforced by the ArchitectureTests source-scan).</summary>
    public PurchaseOrder SeededAs(PoStatus status) { Status = status; return this; }
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
