using FSH.Framework.Core.Domain;

namespace FSH.Modules.Procurement.Domain;

public sealed class PurchaseOrder : AggregateRoot<Guid>
{
    private readonly List<PoLine> _lines = [];

    public string Code { get; private set; } = default!;
    public Guid AwardId { get; private set; }
    public Guid RfqId { get; private set; }
    public Guid VendorId { get; private set; }
    public PoStatus Status { get; private set; } = PoStatus.Draft;
    public string Currency { get; private set; } = default!;
    public DateTime CreatedUtc { get; private set; }
    public DateTime UpdatedUtc { get; private set; }

    public IReadOnlyList<PoLine> Lines => _lines;

    public decimal TotalValue => _lines.Sum(l => l.Qty * l.UnitPrice);

    private PurchaseOrder() { }

    public static PurchaseOrder Create(string code, Guid awardId, Guid rfqId, Guid vendorId, string currency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        var now = DateTime.UtcNow;
        return new PurchaseOrder
        {
            Id = Guid.CreateVersion7(),
            Code = code.Trim(),
            AwardId = awardId,
            RfqId = rfqId,
            VendorId = vendorId,
            Currency = currency,
            CreatedUtc = now,
            UpdatedUtc = now,
        };
    }

    public PoLine AddLine(string itemCode, string description, string uom, decimal qty, decimal unitPrice, string rfqLineCode)
    {
        var line = PoLine.Create(Id, itemCode, description, uom, qty, unitPrice, rfqLineCode);
        _lines.Add(line);
        return line;
    }

    public void Issue()
    {
        if (Status != PoStatus.Draft)
            throw new ProcurementRuleException($"Cannot issue a PO that is {Status}.");
        Status = PoStatus.Issued;
        UpdatedUtc = DateTime.UtcNow;
    }

    public void Acknowledge()
    {
        if (Status != PoStatus.Issued)
            throw new ProcurementRuleException($"Cannot acknowledge a PO that is {Status}.");
        Status = PoStatus.Acknowledged;
        UpdatedUtc = DateTime.UtcNow;
    }

    public void ApplyReceivedQty(string itemCode, decimal qty)
    {
        var line = _lines.FirstOrDefault(l => string.Equals(l.ItemCode, itemCode, StringComparison.Ordinal))
            ?? throw new ProcurementRuleException($"Line {itemCode} not found on PO {Code}.");
        line.AddReceivedQty(qty);
        RecomputeReceiptStatus();
    }

    public void RecordReceipt()
    {
        RecomputeReceiptStatus();
    }

    public void MarkDiscrepancy()
    {
        Status = PoStatus.Discrepancy;
        UpdatedUtc = DateTime.UtcNow;
    }

    public void MarkMatched()
    {
        Status = PoStatus.Matched;
        UpdatedUtc = DateTime.UtcNow;
    }

    public void ClearDiscrepancy()
    {
        RecomputeReceiptStatus();
    }

    private void RecomputeReceiptStatus()
    {
        bool allReceived = _lines.All(l => l.ReceivedQty >= l.Qty);
        Status = allReceived ? PoStatus.Received : PoStatus.PartiallyReceived;
        UpdatedUtc = DateTime.UtcNow;
    }
}
