using System.Globalization;
using eProcure.Application;
using eProcure.Application.Abstractions;
using eProcure.Application.Procurement;
using eProcure.Application.Suppliers;
using eProcure.Domain.Procurement;
using eProcure.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eProcure.Infrastructure.Services;

/// <summary>
/// Statement of Account = server-side derivation over POs, GRNs and invoices (no new
/// mutable tables). Amounts are always recomputed server-side ([$]); a vendor may only
/// read its own statement ([G]).
/// </summary>
public sealed class StatementService(AppDbContext db, IClock clock, ICurrentUser user) : IStatementService
{
    public async Task<IReadOnlyList<StatementSummaryDto>> ListAsync(CancellationToken ct = default)
    {
        var data = await LoadAsync(ct);
        return data.VendorIds
            .Select(vid => new StatementSummaryDto(vid, data.Name(vid), data.Invoiced(vid), data.Paid(vid), data.Balance(vid), data.Grni(vid)))
            .OrderByDescending(s => s.Balance + s.Grni)
            .ToList();
    }

    public async Task<StatementDetailDto?> GetAsync(Guid vendorId, CancellationToken ct = default)
    {
        VendorAccess.EnsureCanAccess(user, vendorId);   // [G] vendor scoping
        var data = await LoadAsync(ct);
        if (!data.VendorIds.Contains(vendorId)) return null;
        return Detail(data, vendorId);
    }

    public async Task<StatementDetailDto?> GetMineAsync(CancellationToken ct = default)
    {
        if (user.VendorId is not { } vid) throw new ForbiddenException("Only a vendor user has a statement here.");
        var data = await LoadAsync(ct);
        return data.VendorIds.Contains(vid) ? Detail(data, vid) : new StatementDetailDto(vid, "", 0, 0, 0, 0, new AgingDto(0, 0, 0, 0), []);
    }

    private static StatementDetailDto Detail(SoaData d, Guid vid) => new(
        vid, d.Name(vid), d.Invoiced(vid), d.Paid(vid), d.Balance(vid), d.Grni(vid), d.Aging(vid), d.Ledger(vid));

    private async Task<SoaData> LoadAsync(CancellationToken ct)
    {
        var pos = await db.PurchaseOrders.AsNoTracking().Include(p => p.Lines).ToListAsync(ct);
        var grns = await db.Grns.AsNoTracking().Include(g => g.Lines).ToListAsync(ct);
        var invoices = await db.Invoices.AsNoTracking().Include(i => i.Lines).ToListAsync(ct);
        var vendors = await db.Vendors.AsNoTracking().ToDictionaryAsync(v => v.Id, v => v.Name, ct);
        return new SoaData(pos, grns, invoices, vendors, clock.UtcNow);
    }

    /// <summary>Pure derivation helper (mirrors the prototype's soa* functions).</summary>
    private sealed class SoaData(
        List<PurchaseOrder> pos, List<Grn> grns, List<Invoice> invoices, Dictionary<Guid, string> vendors, DateTime asOf)
    {
        public IReadOnlyList<Guid> VendorIds { get; } =
            pos.Select(p => p.VendorId).Concat(invoices.Select(i => i.VendorId)).Distinct().ToList();

        public string Name(Guid vid) => vendors.GetValueOrDefault(vid, "Vendor");

        private IEnumerable<Invoice> Posted(Guid vid) =>
            invoices.Where(i => i.VendorId == vid && i.Status is InvoiceStatus.Approved or InvoiceStatus.Paid);

        public decimal Invoiced(Guid vid) => Posted(vid).Sum(i => i.Total);
        public decimal Paid(Guid vid) => invoices.Where(i => i.VendorId == vid && i.Status == InvoiceStatus.Paid).Sum(i => i.Total);
        public decimal Balance(Guid vid) => Invoiced(vid) - Paid(vid);

        public decimal Grni(Guid vid) => pos.Where(p => p.VendorId == vid).Sum(p =>
        {
            var recvValue = p.Lines.Sum(l => l.ReceivedQty * l.UnitPrice);
            var invoicedSub = Posted(vid).Where(i => i.PoId == p.Id).Sum(i => i.Subtotal);
            return Math.Max(0, recvValue - invoicedSub);
        });

        public AgingDto Aging(Guid vid)
        {
            decimal cur = 0, d30 = 0, d60 = 0, d90 = 0;
            foreach (var i in invoices.Where(x => x.VendorId == vid && x.Status == InvoiceStatus.Approved))
            {
                var age = DaysAgo(i.Date);
                var amt = i.Total;
                if (age <= 30) cur += amt; else if (age <= 60) d30 += amt; else if (age <= 90) d60 += amt; else d90 += amt;
            }
            return new AgingDto(cur, d30, d60, d90);
        }

        public IReadOnlyList<LedgerEntryDto> Ledger(Guid vid)
        {
            var e = new List<(DateTime? D, string Ds, string Type, string Ref, decimal Credit, decimal Debit, decimal? Memo)>();
            foreach (var p in pos.Where(x => x.VendorId == vid && x.Status != PoStatus.Draft))
                e.Add((Parse(""), "", "PO issued", p.Code, 0, 0, p.Lines.Sum(l => l.Qty * l.UnitPrice)));
            foreach (var g in grns.Where(x => pos.Any(p => p.Id == x.PoId && p.VendorId == vid)))
            {
                var po = pos.First(p => p.Id == g.PoId);
                var val = g.Lines.Sum(l => l.ReceivedQty * (po.Lines.FirstOrDefault(pl => pl.ItemCode == l.ItemCode)?.UnitPrice ?? 0));
                e.Add((Parse(g.ReceivedDate), g.ReceivedDate, "Goods receipt", g.Code, 0, 0, val));
            }
            foreach (var i in Posted(vid))
                e.Add((Parse(i.Date), i.Date, "Invoice", i.Code, i.Total, 0, null));
            foreach (var i in invoices.Where(x => x.VendorId == vid && x.Status == InvoiceStatus.Paid))
                e.Add((Parse(i.Date), i.Date, "Payment", i.Code, 0, i.Total, null));

            var ordered = e.OrderBy(x => x.D ?? DateTime.MinValue).ToList();
            var ledger = new List<LedgerEntryDto>();
            decimal bal = 0;
            foreach (var x in ordered)
            {
                bal += x.Credit - x.Debit;
                ledger.Add(new LedgerEntryDto(x.Ds, x.Ref, x.Type, x.Memo, x.Debit, x.Credit, bal));
            }
            return ledger;
        }

        private int DaysAgo(string dmy) => Parse(dmy) is { } d ? (int)Math.Round((asOf.Date - d.Date).TotalDays) : 0;

        private static DateTime? Parse(string dmy) =>
            DateTime.TryParseExact(dmy, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
    }
}
