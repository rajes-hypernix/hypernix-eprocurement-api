using FSH.Framework.Core.Domain;

namespace FSH.Modules.Procurement.Domain;

public sealed class InvoiceLine : BaseEntity<Guid>
{
    public Guid InvoiceId { get; private set; }
    public string ItemCode { get; private set; } = default!;
    public decimal Qty { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal LineTotal => Qty * UnitPrice;

    private InvoiceLine() { }

    internal static InvoiceLine Create(Guid invoiceId, string itemCode, decimal qty, decimal unitPrice)
    {
        return new InvoiceLine
        {
            Id = Guid.CreateVersion7(),
            InvoiceId = invoiceId,
            ItemCode = itemCode,
            Qty = qty,
            UnitPrice = unitPrice,
        };
    }
}
