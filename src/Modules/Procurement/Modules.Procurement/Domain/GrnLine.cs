using FSH.Framework.Core.Domain;

namespace FSH.Modules.Procurement.Domain;

public sealed class GrnLine : BaseEntity<Guid>
{
    public Guid GrnId { get; private set; }
    public string ItemCode { get; private set; } = default!;
    public decimal ExpectedQty { get; private set; }
    public decimal ReceivedQty { get; private set; }
    public string Condition { get; private set; } = "Good";

    private GrnLine() { }

    internal static GrnLine Create(Guid grnId, string itemCode, decimal expectedQty, decimal receivedQty)
    {
        return new GrnLine
        {
            Id = Guid.CreateVersion7(),
            GrnId = grnId,
            ItemCode = itemCode,
            ExpectedQty = expectedQty,
            ReceivedQty = receivedQty,
            Condition = receivedQty < expectedQty ? "Short" : "Good",
        };
    }
}
