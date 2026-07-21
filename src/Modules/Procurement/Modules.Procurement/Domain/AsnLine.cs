using FSH.Framework.Core.Domain;

namespace FSH.Modules.Procurement.Domain;

public sealed class AsnLine : BaseEntity<Guid>
{
    public Guid AsnId { get; private set; }
    public string ItemCode { get; private set; } = default!;
    public decimal ShippedQty { get; private set; }
    public string? LotNo { get; private set; }

    private AsnLine() { }

    internal static AsnLine Create(Guid asnId, string itemCode, decimal shippedQty, string? lotNo)
    {
        return new AsnLine
        {
            Id = Guid.CreateVersion7(),
            AsnId = asnId,
            ItemCode = itemCode,
            ShippedQty = shippedQty,
            LotNo = lotNo,
        };
    }
}
