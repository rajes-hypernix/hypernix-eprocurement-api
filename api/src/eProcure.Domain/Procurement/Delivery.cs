namespace eProcure.Domain.Procurement;

public enum AsnStatus { Draft, InTransit, Received }

/// <summary>
/// Advance shipping notice raised by a vendor against a PO. ShippedQty is clamped to
/// remaining-to-ship; a line fully committed cannot be re-shipped (BUSINESS-RULES [Q]).
/// </summary>
public class Asn
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Code { get; set; } = default!;          // ASN-2026-0001
    public Guid PoId { get; set; }
    public Guid VendorId { get; set; }
    public string Carrier { get; set; } = "";
    public string TrackingNo { get; set; } = "";
    public string ShippedDate { get; set; } = "";
    public string ExpectedDate { get; set; } = "";
    public AsnStatus Status { get; private set; } = AsnStatus.InTransit;
    public string? GrnCode { get; set; }
    public List<AsnLine> Lines { get; set; } = [];
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }

    /// <summary>→ Received, once the buyer posts a GRN against this shipment (no longer in transit).
    /// The pre-Slice-G code set this with no precondition; preserved as-is (behaviour-preserving).</summary>
    public void MarkReceived() => Status = AsnStatus.Received;

    /// <summary>TEST/SEED ONLY — sets the status directly, bypassing transitions. Never call from
    /// production service code (enforced by the ArchitectureTests source-scan).</summary>
    public Asn SeededAs(AsnStatus status) { Status = status; return this; }
}

public class AsnLine
{
    public string ItemCode { get; set; } = default!;
    public string Description { get; set; } = "";
    public decimal ShippedQty { get; set; }
    public string Uom { get; set; } = "Unit";
    public string LotNo { get; set; } = "";
}

/// <summary>
/// Goods receipt note posted by the buyer against an ASN. ReceivedQty is capped to
/// min(shipped, outstanding); under-receipt is flagged Short and frees the shortfall
/// back into remaining-to-ship (BUSINESS-RULES [Q]).
/// </summary>
public class Grn
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Code { get; set; } = default!;          // GRN-2026-0001
    public Guid AsnId { get; set; }
    public Guid PoId { get; set; }
    public string ReceivedDate { get; set; } = "";
    public string ReceivedBy { get; set; } = "";
    public string? NsId { get; set; }
    public List<GrnLine> Lines { get; set; } = [];
    public DateTime CreatedUtc { get; set; }
}

public class GrnLine
{
    public string ItemCode { get; set; } = default!;
    public string Description { get; set; } = "";
    public decimal ExpectedQty { get; set; }
    public decimal ReceivedQty { get; set; }
    public string Condition { get; set; } = "Good";       // Good | Short
}
