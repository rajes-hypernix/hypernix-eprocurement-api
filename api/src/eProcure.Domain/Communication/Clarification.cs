namespace eProcure.Domain.Communication;

/// <summary>
/// A single clarification message between the buyer (procurement) and a vendor.
/// A thread is identified by (Scope, VendorId): Scope is "general" or an RFQ code
/// (e.g. "RFQ-2026-0079"). Buyer answers can be <see cref="Published"/> — shared with
/// all bidders on the RFQ (anonymised). Read state is tracked per side so each party
/// gets its own unread count (BUSINESS-RULES: vendors only ever see their own threads).
/// </summary>
public class Clarification
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Scope { get; set; } = "general";
    public Guid VendorId { get; set; }
    public string SenderKind { get; set; } = "buyer";   // "buyer" | "vendor"
    public string SenderName { get; set; } = "";
    public string? RecipientUserId { get; set; }         // buyer the vendor addressed (null = team / broadcast)
    public string Body { get; set; } = "";
    public bool Published { get; set; }
    public DateTime CreatedUtc { get; set; }
    public bool ReadByBuyer { get; set; }
    public bool ReadByVendor { get; set; }
}
