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
    public Guid? GrnId { get; set; }                      // the receipt this invoice matches (3-way-match lineage, Slice H T3)
    public Guid VendorId { get; set; }
    public string InvoiceNo { get; set; } = "";           // supplier's own ref
    public string Date { get; set; } = "";
    public string Currency { get; set; } = "MYR";
    public decimal WhtRate { get; set; }                  // withholding tax %
    public InvoiceStatus Status { get; private set; } = InvoiceStatus.Draft;
    public string? ExceptionReason { get; set; }
    public string? NsId { get; set; }
    public List<InvoiceLine> Lines { get; set; } = [];
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }

    public decimal Subtotal => Lines.Sum(l => l.Qty * l.UnitPrice);
    public decimal Sst => Math.Round(Subtotal * SstRate, 0, MidpointRounding.AwayFromZero);
    public decimal Wht => Math.Round(Subtotal * (WhtRate / 100m), 0, MidpointRounding.AwayFromZero);
    public decimal Total => Subtotal + Sst - Wht;

    // ===== Lifecycle transitions (T3). The submit flow constructs a Draft invoice, then flips it to
    // Submitted or Exception; approval requires a non-approved, non-paid invoice. =====

    /// <summary>Draft → Submitted (matched at submission time).</summary>
    public void MarkSubmitted() => Status = InvoiceStatus.Submitted;

    /// <summary>Draft → Exception (price/qty variance at submission), capturing the reason.</summary>
    public void MarkException(string? reason) { Status = InvoiceStatus.Exception; ExceptionReason = reason; }

    /// <summary>→ Approved for payment. Guards against an already-terminal invoice; the invoice service
    /// enforces the Exception-must-be-resolved-first rule before calling this.</summary>
    public void Approve()
    {
        if (Status is InvoiceStatus.Approved or InvoiceStatus.Paid)
            throw new DomainRuleException($"Invoice {Code} is already {Status}.");
        Status = InvoiceStatus.Approved;
    }

    /// <summary>TEST/SEED ONLY — sets the status directly, bypassing transitions. Never call from
    /// production service code (enforced by the ArchitectureTests source-scan).</summary>
    public Invoice SeededAs(InvoiceStatus status) { Status = status; return this; }
}

public class InvoiceLine
{
    public Guid Id { get; private set; } = Guid.NewGuid();   // stable grain key for facts/lineage (Slice H T1)
    public string ItemCode { get; set; } = default!;
    public string Description { get; set; } = "";
    public decimal Qty { get; set; }
    public string Uom { get; set; } = "Unit";
    public decimal UnitPrice { get; set; }
}
