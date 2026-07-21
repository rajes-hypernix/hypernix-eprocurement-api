using FSH.Framework.Core.Domain;

namespace FSH.Modules.Procurement.Domain;

public sealed class Invoice : AggregateRoot<Guid>
{
    public const decimal SstRate = 0.08m;
    public const decimal PriceTolerance = 0.02m;

    private readonly List<InvoiceLine> _lines = [];

    public string Code { get; private set; } = default!;
    public Guid PoId { get; private set; }
    public Guid? GrnId { get; private set; }
    public string InvoiceNo { get; private set; } = default!;
    public InvoiceStatus Status { get; private set; } = InvoiceStatus.Submitted;
    public DateOnly? InvoiceDate { get; private set; }
    public decimal WhtRate { get; private set; }
    public string? ExceptionReason { get; private set; }
    public DateTime CreatedUtc { get; private set; }

    public IReadOnlyList<InvoiceLine> Lines => _lines;

    public decimal Subtotal => _lines.Sum(l => l.LineTotal);
    public decimal SstAmount => Math.Round(Subtotal * SstRate, 0, MidpointRounding.AwayFromZero);
    public decimal WhtAmount => Math.Round(Subtotal * (WhtRate / 100m), 0, MidpointRounding.AwayFromZero);
    public decimal Total => Subtotal + SstAmount - WhtAmount;

    private Invoice() { }

    public static Invoice Create(string code, Guid poId, Guid? grnId, string invoiceNo, DateOnly? invoiceDate, decimal whtRate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(invoiceNo);
        return new Invoice
        {
            Id = Guid.CreateVersion7(),
            Code = code.Trim(),
            PoId = poId,
            GrnId = grnId,
            InvoiceNo = invoiceNo.Trim(),
            InvoiceDate = invoiceDate,
            WhtRate = whtRate,
            CreatedUtc = DateTime.UtcNow,
        };
    }

    public InvoiceLine AddLine(string itemCode, decimal qty, decimal unitPrice)
    {
        var line = InvoiceLine.Create(Id, itemCode, qty, unitPrice);
        _lines.Add(line);
        return line;
    }

    public void MarkSubmitted()
    {
        Status = InvoiceStatus.Submitted;
    }

    public void MarkException(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ExceptionReason = reason;
        Status = InvoiceStatus.Exception;
    }

    public void Approve()
    {
        if (Status == InvoiceStatus.Exception)
            throw new ProcurementRuleException("Cannot approve an invoice with an unresolved exception.");
        Status = InvoiceStatus.Approved;
    }

    public void ResolveException()
    {
        if (Status != InvoiceStatus.Exception)
            throw new ProcurementRuleException("Invoice does not have an exception to resolve.");
        ExceptionReason = null;
        Status = InvoiceStatus.Submitted;
    }
}
