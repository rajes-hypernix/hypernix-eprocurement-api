namespace FSH.Modules.Sourcing.Domain;

/// <summary>One vendor's allocated quantity/price for one RFQ line within an <see cref="Award"/>.</summary>
public sealed class AwardAllocation
{
    public string RfqLineCode { get; private set; }

    /// <summary>Bare reference into Modules.Suppliers — no cross-schema FK, matching RfqInvitation.VendorId.</summary>
    public Guid VendorId { get; private set; }

    public decimal Qty { get; private set; }
    public decimal UnitPrice { get; private set; }

    public AwardAllocation(string rfqLineCode, Guid vendorId, decimal qty, decimal unitPrice)
    {
        RfqLineCode = rfqLineCode;
        VendorId = vendorId;
        Qty = qty;
        UnitPrice = unitPrice;
    }
}
