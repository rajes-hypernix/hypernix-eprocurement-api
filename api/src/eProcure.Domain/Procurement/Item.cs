namespace eProcure.Domain.Procurement;

/// <summary>CFH-T4: the Item Master — a small lookup source. Lines across the app keep storing
/// ItemCode as a plain string (no re-keying); the entry form PICKS from this master and auto-fills
/// Description + UoM. ItemCode is the unique business key.</summary>
public class Item
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ItemCode { get; set; } = default!;   // unique business key, e.g. VLV-GT-0150
    public string Description { get; set; } = "";
    public string Uom { get; set; } = "Unit";
    public bool Active { get; set; } = true;
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
