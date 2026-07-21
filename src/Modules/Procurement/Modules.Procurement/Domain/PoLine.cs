using FSH.Framework.Core.Domain;

namespace FSH.Modules.Procurement.Domain;

public sealed class PoLine : BaseEntity<Guid>
{
    public Guid PurchaseOrderId { get; private set; }
    public string ItemCode { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public string Uom { get; private set; } = default!;
    public decimal Qty { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal ReceivedQty { get; private set; }
    public decimal InvoicedQty { get; private set; }
    public string RfqLineCode { get; private set; } = default!;

    private PoLine() { }

    internal static PoLine Create(Guid purchaseOrderId, string itemCode, string description, string uom, decimal qty, decimal unitPrice, string rfqLineCode)
    {
        return new PoLine
        {
            Id = Guid.CreateVersion7(),
            PurchaseOrderId = purchaseOrderId,
            ItemCode = itemCode,
            Description = description,
            Uom = uom,
            Qty = qty,
            UnitPrice = unitPrice,
            RfqLineCode = rfqLineCode,
        };
    }

    internal void AddReceivedQty(decimal qty) => ReceivedQty += qty;

    internal void AddInvoicedQty(decimal qty) => InvoicedQty += qty;
}
