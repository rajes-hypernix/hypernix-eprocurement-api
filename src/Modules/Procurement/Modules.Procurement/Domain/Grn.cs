using FSH.Framework.Core.Domain;

namespace FSH.Modules.Procurement.Domain;

public sealed class Grn : AggregateRoot<Guid>
{
    private readonly List<GrnLine> _lines = [];

    public string Code { get; private set; } = default!;
    public Guid AsnId { get; private set; }
    public Guid PoId { get; private set; }
    public DateTime CreatedUtc { get; private set; }

    public IReadOnlyList<GrnLine> Lines => _lines;

    private Grn() { }

    public static Grn Create(string code, Guid asnId, Guid poId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        return new Grn
        {
            Id = Guid.CreateVersion7(),
            Code = code.Trim(),
            AsnId = asnId,
            PoId = poId,
            CreatedUtc = DateTime.UtcNow,
        };
    }

    public GrnLine AddLine(string itemCode, decimal expectedQty, decimal receivedQty)
    {
        var line = GrnLine.Create(Id, itemCode, expectedQty, receivedQty);
        _lines.Add(line);
        return line;
    }
}
