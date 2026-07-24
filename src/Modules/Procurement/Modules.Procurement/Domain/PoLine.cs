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

    /// <summary>Null for direct-from-requisition/standalone lines — only set on the FromAward route.</summary>
    public string? RfqLineCode { get; private set; }

    /// <summary>Set only on the FromRequisition route — traceability only, not the ordered-quantity cap's source of truth (that ledger lives in Sourcing's PrLineOrder).</summary>
    public Guid? SourcePrLineId { get; private set; }

    public Guid? TaxCodeId { get; private set; }

    /// <summary>IC16 issue gate: an unconfirmed price blocks Issue.</summary>
    public bool PriceConfirmed { get; private set; }

    public decimal LineTotal => Qty * UnitPrice;

    private PoLine() { }

    internal static PoLine Create(
        Guid purchaseOrderId,
        string itemCode,
        string description,
        string uom,
        decimal qty,
        decimal unitPrice,
        string? rfqLineCode = null,
        Guid? sourcePrLineId = null,
        Guid? taxCodeId = null,
        bool priceConfirmed = false)
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
            SourcePrLineId = sourcePrLineId,
            TaxCodeId = taxCodeId,
            PriceConfirmed = priceConfirmed,
        };
    }

    /// <summary>Editing the price to a different value confirms it; an explicit confirm flag
    /// confirms without a change. One-way — never un-confirms.</summary>
    internal void UpdatePrice(decimal? unitPrice, bool priceConfirmed)
    {
        bool changed = unitPrice.HasValue && unitPrice.Value != UnitPrice;
        if (unitPrice.HasValue)
        {
            UnitPrice = unitPrice.Value;
        }

        if (changed || priceConfirmed)
        {
            PriceConfirmed = true;
        }
    }

    internal void AddReceivedQty(decimal qty) => ReceivedQty += qty;

    internal void AddInvoicedQty(decimal qty) => InvoicedQty += qty;
}
