namespace eProcure.Domain.Procurement;

public enum InvoiceStatus { Draft, Submitted, Approved, Exception, Paid }

/// <summary>
/// A supplier invoice 3-way matched against the PO and goods receipt. Invoice qty is
/// capped to billable (received − already invoiced); a price variance beyond tolerance
/// flips the invoice to Exception, which blocks payment until resolved (BUSINESS-RULES
/// [Q]/[G]). SST/WHT are computed server-side ([$]).
/// </summary>
public class Invoice
{
    public const decimal SstRate = 0.08m;        // 8% SST
    public const decimal PriceTolerance = 0.02m; // 2% match tolerance

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Code { get; set; } = default!;          // INV-2026-0001
    public Guid PoId { get; set; }
    public Guid VendorId { get; set; }
    public string InvoiceNo { get; set; } = "";           // supplier's own ref
    public string Date { get; set; } = "";
    public string Currency { get; set; } = "MYR";
    public decimal WhtRate { get; set; }                  // withholding tax %
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public string? ExceptionReason { get; set; }
    public string? NsId { get; set; }
    public List<InvoiceLine> Lines { get; set; } = [];
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }

    public decimal Subtotal => Lines.Sum(l => l.Qty * l.UnitPrice);
    public decimal Sst => Math.Round(Subtotal * SstRate, 0, MidpointRounding.AwayFromZero);
    public decimal Wht => Math.Round(Subtotal * (WhtRate / 100m), 0, MidpointRounding.AwayFromZero);
    public decimal Total => Subtotal + Sst - Wht;
}

public class InvoiceLine
{
    public string ItemCode { get; set; } = default!;
    public string Description { get; set; } = "";
    public decimal Qty { get; set; }
    public string Uom { get; set; } = "Unit";
    public decimal UnitPrice { get; set; }
}
