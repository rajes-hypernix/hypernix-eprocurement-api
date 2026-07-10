using eProcure.Application;
using eProcure.Application.Abstractions;
using eProcure.Application.Procurement;
using eProcure.Application.Suppliers;
using eProcure.Domain;
using eProcure.Domain.Procurement;
using eProcure.Domain.Sourcing;
using eProcure.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eProcure.Infrastructure.Services;

public sealed class PoService(
    AppDbContext db,
    IClock clock,
    IAuditLog audit,
    INetSuiteClient netsuite,
    ICurrentUser user) : IPoService
{
    public async Task<IReadOnlyList<PoListItem>> ListAsync(CancellationToken ct = default)
    {
        var q = db.PurchaseOrders.AsNoTracking().Include(p => p.Lines).AsQueryable();
        if (user.VendorId is { } vid) q = q.Where(p => p.VendorId == vid);  // [G] vendor scoping
        var pos = await q.OrderByDescending(p => p.CreatedUtc).ToListAsync(ct);
        var vendors = await VendorNames(ct);
        var rfqCodes = await RfqCodes(ct);
        return pos.Select(p => new PoListItem(
            p.Id, p.Code, p.VendorId, vendors.GetValueOrDefault(p.VendorId, "Vendor"),
            p.RfqId is { } r ? rfqCodes.GetValueOrDefault(r) : null,
            p.Status.ToString(), p.Total, p.Lines.Sum(l => l.ReceivedQty), p.Lines.Sum(l => l.Qty),
            p.Acknowledged, p.NsId)).ToList();
    }

    public async Task<PoDetail?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var po = await db.PurchaseOrders.AsNoTracking().Include(p => p.Lines).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (po is null) return null;
        VendorAccess.EnsureCanAccess(user, po.VendorId);   // [G] a vendor may only read its own PO
        return await Map(po, ct);
    }

    public async Task<PoDetail> IssueAsync(Guid id, CancellationToken ct = default)
    {
        EnsureInternal();
        var po = await Load(id, ct);
        po.Issue();                     // guards Draft; same message as before
        po.NsId ??= $"NS-PO-{po.Code[^4..]}";
        po.UpdatedUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        await netsuite.PushPurchaseOrderAsync(po.Code, ct);  // stub
        await audit.WriteAsync("Po", po.Code, "PO issued", before: "Draft", after: $"Issued · pushed to NetSuite {po.NsId}", ct: ct);
        return await Map(po, ct);
    }

    public async Task<PoDetail> AcknowledgeAsync(Guid id, CancellationToken ct = default)
    {
        var po = await Load(id, ct);
        VendorAccess.EnsureCanAccess(user, po.VendorId);   // only the awarded vendor acknowledges
        po.Acknowledge();               // guards Issued; same message as before
        po.Acknowledged = true;
        po.UpdatedUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Po", po.Code, "PO acknowledged", before: "Issued", after: "Acknowledged", ct: ct);
        return await Map(po, ct);
    }

    private void EnsureInternal()
    {
        if (user.VendorId is not null) throw new ForbiddenException("Only an internal buyer can issue POs.");
    }

    private async Task<PurchaseOrder> Load(Guid id, CancellationToken ct) =>
        await db.PurchaseOrders.Include(p => p.Lines).FirstOrDefaultAsync(p => p.Id == id, ct)
        ?? throw new NotFoundException($"PO {id} not found.");

    private async Task<Dictionary<Guid, string>> VendorNames(CancellationToken ct) =>
        await db.Vendors.AsNoTracking().ToDictionaryAsync(v => v.Id, v => v.Name, ct);

    private async Task<Dictionary<Guid, string>> RfqCodes(CancellationToken ct) =>
        await db.Rfqs.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.Code, ct);

    private async Task<PoDetail> Map(PurchaseOrder p, CancellationToken ct)
    {
        var vendors = await VendorNames(ct);
        var rfqCodes = await RfqCodes(ct);
        return new PoDetail(p.Id, p.Code, p.VendorId, vendors.GetValueOrDefault(p.VendorId, "Vendor"),
            p.RfqId is { } r ? rfqCodes.GetValueOrDefault(r) : null, p.AwardCode,
            p.Status.ToString(), p.Currency, p.Incoterm, p.NsId, p.Acknowledged, p.Total,
            p.Lines.Select(l => new PoLineDto(l.ItemCode, l.Description, l.Qty, l.Uom, l.UnitPrice,
                l.ReceivedQty, l.InvoicedQty, l.Qty * l.UnitPrice)).ToList());
    }
}
