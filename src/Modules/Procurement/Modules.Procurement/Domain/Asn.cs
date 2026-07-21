using FSH.Framework.Core.Domain;

namespace FSH.Modules.Procurement.Domain;

public sealed class Asn : AggregateRoot<Guid>
{
    private readonly List<AsnLine> _lines = [];

    public string Code { get; private set; } = default!;
    public Guid PoId { get; private set; }
    public AsnStatus Status { get; private set; } = AsnStatus.InTransit;
    public string Carrier { get; private set; } = default!;
    public string TrackingNo { get; private set; } = default!;
    public DateOnly? ShippedDate { get; private set; }
    public DateOnly? ExpectedDate { get; private set; }
    public DateTime CreatedUtc { get; private set; }

    public IReadOnlyList<AsnLine> Lines => _lines;

    private Asn() { }

    public static Asn Create(string code, Guid poId, string carrier, string trackingNo, DateOnly? shippedDate, DateOnly? expectedDate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        return new Asn
        {
            Id = Guid.CreateVersion7(),
            Code = code.Trim(),
            PoId = poId,
            Carrier = carrier,
            TrackingNo = trackingNo,
            ShippedDate = shippedDate,
            ExpectedDate = expectedDate,
            CreatedUtc = DateTime.UtcNow,
        };
    }

    public AsnLine AddLine(string itemCode, decimal shippedQty, string? lotNo)
    {
        var line = AsnLine.Create(Id, itemCode, shippedQty, lotNo);
        _lines.Add(line);
        return line;
    }

    public void MarkReceived()
    {
        if (Status != AsnStatus.InTransit)
            throw new ProcurementRuleException($"Cannot receive an ASN that is {Status}.");
        Status = AsnStatus.Received;
    }
}
