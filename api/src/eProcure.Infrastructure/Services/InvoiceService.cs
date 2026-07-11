using eProcure.Application;
using eProcure.Application.Abstractions;
using eProcure.Application.Procurement;
using eProcure.Application.Suppliers;
using eProcure.Domain;
using eProcure.Domain.Procurement;
using eProcure.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eProcure.Infrastructure.Services;

public sealed class InvoiceService(
    AppDbContext db,
    IClock clock,
    ICodeGenerator codes,
    IAuditLog audit,
    INetSuiteClient netsuite,
    ICurrentUser user) : IInvoiceService
{
    public async Task<IReadOnlyList<InvoiceListDto>> ListAsync(CancellationToken ct = default)
    {
        var q = db.Invoices.AsNoTracking().Include(i => i.Lines).AsQueryable();
        if (user.VendorId is { } vid) q = q.Where(i => i.VendorId == vid);  // [G] vendor scoping
        var invoices = await q.OrderByDescending(i => i.CreatedUtc).ToListAsync(ct);
        var pos = await PosWithLines(ct);
        var vendors = await VendorNames(ct);
        return invoices.Select(i => new InvoiceListDto(
            i.Id, i.Code, pos.GetValueOrDefault(i.PoId)?.Code ?? "—", vendors.GetValueOrDefault(i.VendorId, "Vendor"),
            i.InvoiceNo, i.Status.ToString(), MatchStatus(i, pos.GetValueOrDefault(i.PoId)), i.Subtotal, i.Total, Payable(i))).ToList();
    }

    public async Task<InvoiceDetailDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var inv = await db.Invoices.AsNoTracking().Include(i => i.Lines).FirstOrDefaultAsync(i => i.Id == id, ct);
        if (inv is null) return null;
        VendorAccess.EnsureCanAccess(user, inv.VendorId);
        var po = await PoById(inv.PoId, ct);
        return Map(inv, po);
    }

    public async Task<InvoiceBillablePlan> GetBillablePlanAsync(Guid poId, CancellationToken ct = default)
    {
        var po = await LoadPo(poId, ct);
        VendorAccess.EnsureCanAccess(user, po.VendorId);
        var invoiced = await InvoicedByItem(poId, ct);
        var lines = po.Lines.Select(l =>
        {
            var already = invoiced.GetValueOrDefault(l.ItemCode, 0);
            return new InvoiceBillablePlanLine(l.ItemCode, l.Description, l.ReceivedQty, already,
                Math.Max(0, l.ReceivedQty - already), l.UnitPrice, l.Uom);
        }).ToList();
        return new InvoiceBillablePlan(po.Id, po.Code, lines);
    }

    public async Task<InvoiceDetailDto> SubmitAsync(Guid poId, SubmitInvoiceRequest req, CancellationToken ct = default)
    {
        var po = await LoadPo(poId, ct);
        VendorAccess.EnsureCanAccess(user, po.VendorId);
        var invoiced = await InvoicedByItem(poId, ct);

        var lines = new List<InvoiceLine>();
        var priceVariance = false;
        string? reason = null;
        foreach (var rl in req.Lines)
        {
            var pol = po.Lines.FirstOrDefault(l => l.ItemCode == rl.ItemCode);
            if (pol is null) continue;
            var billable = Math.Max(0, pol.ReceivedQty - invoiced.GetValueOrDefault(rl.ItemCode, 0));
            // [Q] invoice qty <= received − already invoiced (no over-billing)
            var qty = Math.Max(0, Math.Min(rl.Qty, billable));
            if (qty <= 0) continue;
            var price = rl.UnitPrice;
            // [Q] price variance beyond tolerance → Exception
            if (Math.Abs(price - pol.UnitPrice) > pol.UnitPrice * Invoice.PriceTolerance)
            {
                priceVariance = true;
                reason = $"Unit price billed above PO ({rl.ItemCode}: RM {price:N2} vs RM {pol.UnitPrice:N2}).";
            }
            lines.Add(new InvoiceLine { ItemCode = pol.ItemCode, Description = pol.Description, Qty = qty, Uom = pol.Uom, UnitPrice = price });
            pol.InvoicedQty += qty;
        }
        if (lines.Count == 0)
            throw new DomainRuleException("Enter an invoice quantity on at least one billable line.");

        // 3-way-match lineage (T3): link the receipt when the PO has exactly one GRN. With multiple
        // partial receipts we can't tell which one this invoice matches, so leave it null (never guess).
        var grnIds = await db.Grns.Where(g => g.PoId == poId).Select(g => g.Id).ToListAsync(ct);
        var inv = new Invoice
        {
            Code = await codes.NextAsync("INV", ct),
            PoId = poId, GrnId = grnIds.Count == 1 ? grnIds[0] : null,
            VendorId = po.VendorId, InvoiceNo = req.InvoiceNo, Date = req.Date,
            WhtRate = req.WhtRate, Lines = lines,
            CreatedUtc = clock.UtcNow, UpdatedUtc = clock.UtcNow,
        };
        if (priceVariance) inv.MarkException(reason); else inv.MarkSubmitted(clock.UtcNow);
        db.Invoices.Add(inv);
        if (priceVariance && po.Status is PoStatus.Issued or PoStatus.Acknowledged or PoStatus.PartiallyReceived or PoStatus.Received)
            po.MarkDiscrepancy();
        await db.SaveChangesAsync(ct);

        await audit.WriteAsync("Invoice", inv.Code, $"Invoice submitted ({(priceVariance ? "exception" : "matched")})",
            after: $"PO {po.Code} · total RM {inv.Total:N2}", ct: ct);
        return Map(inv, po);
    }

    public async Task<InvoiceDetailDto> ApproveAsync(Guid id, CancellationToken ct = default)
    {
        EnsureInternal();
        var inv = await Load(id, ct);
        // [G] Exception blocks payment — must be resolved first.
        if (inv.Status == InvoiceStatus.Exception)
            throw new DomainRuleException("This invoice is in Exception (price/qty variance) and cannot be approved for payment until resolved.");
        if (inv.Status is InvoiceStatus.Approved or InvoiceStatus.Paid)
            throw new DomainRuleException($"Invoice {inv.Code} is already {inv.Status}.");
        await ApproveInternal(inv, "Approved invoice for payment", ct);
        return Map(inv, await PoById(inv.PoId, ct));
    }

    public async Task<InvoiceDetailDto> ResolveExceptionAsync(Guid id, CancellationToken ct = default)
    {
        EnsureInternal();
        var inv = await Load(id, ct);
        if (inv.Status != InvoiceStatus.Exception)
            throw new DomainRuleException($"Invoice {inv.Code} is not in Exception.");
        inv.ExceptionReason = null;
        await ApproveInternal(inv, "Resolved exception & approved invoice", ct);
        return Map(inv, await PoById(inv.PoId, ct));
    }

    private async Task ApproveInternal(Invoice inv, string action, CancellationToken ct)
    {
        inv.Approve(clock.UtcNow);                  // guards not already Approved/Paid
        inv.NsId ??= $"NS-VB-{(await NextSeqSuffix(ct))}";
        inv.UpdatedUtc = clock.UtcNow;

        var po = await LoadPo(inv.PoId, ct);
        if (po.Status is PoStatus.Received or PoStatus.PartiallyReceived or PoStatus.Discrepancy
            && po.Lines.Sum(l => l.InvoicedQty) >= po.Lines.Sum(l => l.Qty))
            po.MarkMatched();
        await db.SaveChangesAsync(ct);
        await netsuite.PushVendorBillAsync(inv.Code, ct);   // stub
        await audit.WriteAsync("Invoice", inv.Code, action, after: $"Vendor Bill {inv.NsId}", ct: ct);
    }

    // ---- helpers ----

    private static bool Payable(Invoice i) => i.Status == InvoiceStatus.Approved;  // Exception/Submitted not payable

    private static string MatchStatus(Invoice inv, PurchaseOrder? po)
    {
        if (po is null) return "Variance";
        var ok = inv.Lines.All(l =>
        {
            var pol = po.Lines.FirstOrDefault(p => p.ItemCode == l.ItemCode);
            if (pol is null) return false;
            var qtyOk = l.Qty <= pol.ReceivedQty;
            var priceOk = Math.Abs(l.UnitPrice - pol.UnitPrice) <= pol.UnitPrice * Invoice.PriceTolerance;
            return qtyOk && priceOk;
        });
        return ok ? "Matched" : "Variance";
    }

    private void EnsureInternal()
    {
        if (user.VendorId is not null) throw new ForbiddenException("Only the buyer can approve invoices.");
    }

    private async Task<Invoice> Load(Guid id, CancellationToken ct) =>
        await db.Invoices.Include(i => i.Lines).FirstOrDefaultAsync(i => i.Id == id, ct)
        ?? throw new NotFoundException($"Invoice {id} not found.");

    private async Task<PurchaseOrder> LoadPo(Guid id, CancellationToken ct) =>
        await db.PurchaseOrders.Include(p => p.Lines).FirstOrDefaultAsync(p => p.Id == id, ct)
        ?? throw new NotFoundException($"PO {id} not found.");

    private async Task<PurchaseOrder?> PoById(Guid id, CancellationToken ct) =>
        await db.PurchaseOrders.AsNoTracking().Include(p => p.Lines).FirstOrDefaultAsync(p => p.Id == id, ct);

    private async Task<Dictionary<string, decimal>> InvoicedByItem(Guid poId, CancellationToken ct)
    {
        var invoices = await db.Invoices.AsNoTracking().Include(i => i.Lines).Where(i => i.PoId == poId).ToListAsync(ct);
        return invoices.SelectMany(i => i.Lines).GroupBy(l => l.ItemCode).ToDictionary(g => g.Key, g => g.Sum(l => l.Qty));
    }

    private async Task<Dictionary<Guid, PurchaseOrder>> PosWithLines(CancellationToken ct) =>
        await db.PurchaseOrders.AsNoTracking().Include(p => p.Lines).ToDictionaryAsync(p => p.Id, ct);

    private async Task<Dictionary<Guid, string>> VendorNames(CancellationToken ct) =>
        await db.Vendors.AsNoTracking().ToDictionaryAsync(v => v.Id, v => v.Name, ct);

    private async Task<int> NextSeqSuffix(CancellationToken ct) => 50200 + await db.Invoices.CountAsync(ct);

    private static InvoiceDetailDto Map(Invoice inv, PurchaseOrder? po) => new(
        inv.Id, inv.Code, inv.PoId, po?.Code ?? "—", "", inv.InvoiceNo, inv.Date, inv.Status.ToString(),
        MatchStatus(inv, po), inv.ExceptionReason, inv.NsId, inv.WhtRate,
        inv.Subtotal, inv.Sst, inv.Wht, inv.Total, Payable(inv),
        inv.Lines.Select(l =>
        {
            var pol = po?.Lines.FirstOrDefault(p => p.ItemCode == l.ItemCode);
            var poPrice = pol?.UnitPrice ?? 0;
            var recv = pol?.ReceivedQty ?? 0;
            return new InvoiceLineDto(l.ItemCode, l.Description, l.Qty, recv, l.UnitPrice, poPrice,
                l.Qty <= recv, Math.Abs(l.UnitPrice - poPrice) <= poPrice * Invoice.PriceTolerance, l.Qty * l.UnitPrice);
        }).ToList());
}
