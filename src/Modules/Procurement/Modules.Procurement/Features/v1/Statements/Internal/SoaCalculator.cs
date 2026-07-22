using System.Globalization;
using FSH.Modules.Procurement.Contracts.Dtos;
using FSH.Modules.Procurement.Domain;

namespace FSH.Modules.Procurement.Features.v1.Statements.Internal;

/// <summary>
/// Pure derivation over POs, GRNs and invoices (no mutable SOA tables).
/// Adapted from the original eProcure StatementService / SoaData.
/// </summary>
internal sealed class SoaCalculator(
    List<PurchaseOrder> pos,
    List<Grn> grns,
    List<Invoice> invoices,
    DateTime asOf)
{
    private readonly Dictionary<Guid, Guid> _vendorByPoId = pos.ToDictionary(p => p.Id, p => p.VendorId);

    public IReadOnlyList<Guid> VendorIds { get; } = BuildVendorIds(pos, invoices, pos.ToDictionary(p => p.Id, p => p.VendorId));

    private static IReadOnlyList<Guid> BuildVendorIds(
        List<PurchaseOrder> pos,
        List<Invoice> invoices,
        Dictionary<Guid, Guid> vendorByPoId) =>
        pos.Select(p => p.VendorId)
            .Concat(invoices
                .Select(i => vendorByPoId.TryGetValue(i.PoId, out var vid) ? vid : (Guid?)null)
                .Where(v => v.HasValue)
                .Select(v => v!.Value))
            .Distinct()
            .ToList();

    public static string VendorName(Guid vendorId) => vendorId.ToString("N")[..8].ToUpperInvariant();

    public StatementSummaryDto ToSummary(Guid vendorId) => new(
        vendorId,
        VendorName(vendorId),
        Invoiced(vendorId),
        PaidAmount,
        Balance(vendorId),
        Grni(vendorId));

    public StatementDetailDto ToDetail(Guid vendorId) => new(
        vendorId,
        VendorName(vendorId),
        Invoiced(vendorId),
        PaidAmount,
        Balance(vendorId),
        Grni(vendorId),
        Aging(vendorId),
        Ledger(vendorId));

    private Guid? VendorIdForInvoice(Invoice invoice) =>
        _vendorByPoId.TryGetValue(invoice.PoId, out var vid) ? vid : null;

    private IEnumerable<Invoice> Posted(Guid vendorId) =>
        invoices.Where(i => VendorIdForInvoice(i) == vendorId && i.Status == InvoiceStatus.Approved);

    private decimal Invoiced(Guid vendorId) => Posted(vendorId).Sum(i => i.Total);

    private const decimal PaidAmount = 0;

    private decimal Balance(Guid vendorId) => Invoiced(vendorId) - PaidAmount;

    private decimal Grni(Guid vendorId) => pos.Where(p => p.VendorId == vendorId).Sum(p =>
    {
        var recvValue = p.Lines.Sum(l => l.ReceivedQty * l.UnitPrice);
        var invoicedSub = Posted(vendorId).Where(i => i.PoId == p.Id).Sum(i => i.Subtotal);
        return Math.Max(0, recvValue - invoicedSub);
    });

    private AgingDto Aging(Guid vendorId)
    {
        decimal cur = 0, d30 = 0, d60 = 0, d90 = 0;
        foreach (var i in Posted(vendorId))
        {
            var age = DaysAgo(i.InvoiceDate);
            var amt = i.Total;
            if (age <= 30) cur += amt;
            else if (age <= 60) d30 += amt;
            else if (age <= 90) d60 += amt;
            else d90 += amt;
        }

        return new AgingDto(cur, d30, d60, d90);
    }

    private IReadOnlyList<LedgerEntryDto> Ledger(Guid vendorId)
    {
        var e = new List<(DateOnly? D, string Ds, string Type, string Ref, decimal Credit, decimal Debit, decimal? Memo)>();

        foreach (var p in pos.Where(x => x.VendorId == vendorId && x.Status != PoStatus.Draft))
        {
            e.Add((null, "", "PO issued", p.Code, 0, 0, p.Lines.Sum(l => l.Qty * l.UnitPrice)));
        }

        foreach (var g in grns.Where(x => pos.Any(p => p.Id == x.PoId && p.VendorId == vendorId)))
        {
            var po = pos.First(p => p.Id == g.PoId);
            var receivedDate = DateOnly.FromDateTime(g.CreatedUtc);
            var val = g.Lines.Sum(l =>
                l.ReceivedQty * (po.Lines.FirstOrDefault(pl => pl.ItemCode == l.ItemCode)?.UnitPrice ?? 0));
            e.Add((receivedDate, Dmy(receivedDate), "Goods receipt", g.Code, 0, 0, val));
        }

        foreach (var i in Posted(vendorId))
        {
            e.Add((i.InvoiceDate, Dmy(i.InvoiceDate), "Invoice", i.Code, i.Total, 0, null));
        }

        var ordered = e.OrderBy(x => x.D ?? DateOnly.MinValue).ToList();
        var ledger = new List<LedgerEntryDto>();
        decimal bal = 0;
        foreach (var x in ordered)
        {
            bal += x.Credit - x.Debit;
            ledger.Add(new LedgerEntryDto(x.Ds, x.Ref, x.Type, x.Memo, x.Debit, x.Credit, bal));
        }

        return ledger;
    }

    private int DaysAgo(DateOnly? d) =>
        d is { } dd ? (int)Math.Round((asOf.Date - dd.ToDateTime(TimeOnly.MinValue)).TotalDays) : 0;

    private static string Dmy(DateOnly? d) =>
        d?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "";
}
