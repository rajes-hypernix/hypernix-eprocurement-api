using System.Globalization;
using eProcure.Application;
using eProcure.Application.Abstractions;
using eProcure.Application.Procurement;
using eProcure.Application.Suppliers;
using eProcure.Domain;
using eProcure.Domain.Procurement;
using eProcure.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eProcure.Infrastructure.Services;

public sealed class DeliveryService(
    AppDbContext db,
    IClock clock,
    ICodeGenerator codes,
    IAuditLog audit,
    INetSuiteClient netsuite,
    ICurrentUser user) : IDeliveryService
{
    public async Task<IReadOnlyList<AsnListDto>> ListAsync(CancellationToken ct = default)
    {
        var q = db.Asns.AsNoTracking().AsQueryable();
        if (user.VendorId is { } vid) q = q.Where(a => a.VendorId == vid);  // [G] vendor scoping
        var asns = await q.OrderByDescending(a => a.CreatedUtc).ToListAsync(ct);
        var pos = await PoCodes(ct);
        var vendors = await VendorNames(ct);
        return asns.Select(a => new AsnListDto(a.Id, a.Code, pos.GetValueOrDefault(a.PoId, "—"),
            vendors.GetValueOrDefault(a.VendorId, "Vendor"), a.Carrier, a.ExpectedDate, a.Status.ToString(), a.GrnCode)).ToList();
    }

    public async Task<AsnDetailDto?> GetAsync(Guid asnId, CancellationToken ct = default)
    {
        var a = await db.Asns.AsNoTracking().Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == asnId, ct);
        if (a is null) return null;
        VendorAccess.EnsureCanAccess(user, a.VendorId);
        var pos = await PoCodes(ct);
        var vendors = await VendorNames(ct);
        return new AsnDetailDto(a.Id, a.Code, a.PoId, pos.GetValueOrDefault(a.PoId, "—"),
            vendors.GetValueOrDefault(a.VendorId, "Vendor"), a.Carrier, a.TrackingNo, a.ShippedDate, a.ExpectedDate,
            a.Status.ToString(), a.GrnCode,
            a.Lines.Select(l => new AsnLineDto(l.ItemCode, l.Description, l.ShippedQty, l.Uom, l.LotNo)).ToList());
    }

    public async Task<ShipPlanDto> GetShipPlanAsync(Guid poId, CancellationToken ct = default)
    {
        var po = await LoadPo(poId, ct);
        VendorAccess.EnsureCanAccess(user, po.VendorId);
        var inTransit = await InTransitByItem(poId, ct);
        var lines = po.Lines.Select(l =>
        {
            var it = inTransit.GetValueOrDefault(l.ItemCode, 0);
            var remaining = Math.Max(0, l.Qty - it - l.ReceivedQty);
            return new ShipPlanLineDto(l.ItemCode, l.Description, l.Qty, l.ReceivedQty, it, remaining, l.Uom);
        }).ToList();
        return new ShipPlanDto(po.Id, po.Code, lines.Any(l => l.Remaining > 0), lines);
    }

    public async Task<AsnDetailDto> CreateAsnAsync(Guid poId, CreateAsnRequest req, CancellationToken ct = default)
    {
        var po = await LoadPo(poId, ct);
        VendorAccess.EnsureCanAccess(user, po.VendorId);
        if (po.Status is not (PoStatus.Acknowledged or PoStatus.PartiallyReceived))
            throw new DomainRuleException("The PO must be acknowledged before shipping.");

        var inTransit = await InTransitByItem(poId, ct);
        var lines = new List<AsnLine>();
        foreach (var rl in req.Lines)
        {
            var pol = po.Lines.FirstOrDefault(l => l.ItemCode == rl.ItemCode);
            if (pol is null) continue;
            var remaining = Math.Max(0, pol.Qty - inTransit.GetValueOrDefault(rl.ItemCode, 0) - pol.ReceivedQty);
            // [Q] clamp ShippedQty to remaining-to-ship (no over-ship)
            var shipped = Math.Max(0, Math.Min(rl.ShippedQty, remaining));
            if (shipped > 0)
                lines.Add(new AsnLine { ItemCode = pol.ItemCode, Description = pol.Description, ShippedQty = shipped, Uom = pol.Uom, LotNo = rl.LotNo });
        }
        // [Q] nothing left to ship → no duplicate ASN
        if (lines.Count == 0)
            throw new DomainRuleException("Nothing left to ship — all lines are already in transit or received.");

        var asn = new Asn
        {
            Code = await codes.NextAsync("ASN", ct),
            PoId = poId, VendorId = po.VendorId, Carrier = req.Carrier, TrackingNo = req.TrackingNo,
            ShippedDate = req.ShippedDate, ExpectedDate = req.ExpectedDate,   // Status defaults to InTransit
            Lines = lines, CreatedUtc = clock.UtcNow, UpdatedUtc = clock.UtcNow,
        };
        db.Asns.Add(asn);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Asn", asn.Code, "ASN submitted (in transit)", after: $"PO {po.Code} · {lines.Count} line(s)", ct: ct);
        return (await GetAsync(asn.Id, ct))!;
    }

    public async Task<GrnDetailDto?> GetGrnForAsnAsync(Guid asnId, CancellationToken ct = default)
    {
        var grn = await db.Grns.AsNoTracking().Include(g => g.Lines).FirstOrDefaultAsync(g => g.AsnId == asnId, ct);
        return grn is null ? null : await MapGrn(grn, ct);
    }

    public async Task<GrnDetailDto> ReceiveAsync(Guid asnId, ReceiveRequest req, CancellationToken ct = default)
    {
        EnsureInternal();
        var asn = await db.Asns.Include(a => a.Lines).FirstOrDefaultAsync(a => a.Id == asnId, ct)
            ?? throw new NotFoundException($"ASN {asnId} not found.");
        if (asn.Status != AsnStatus.InTransit)
            throw new DomainRuleException("Only an in-transit ASN can be received.");
        var po = await LoadPo(asn.PoId, ct);

        var grnLines = new List<GrnLine>();
        foreach (var al in asn.Lines)
        {
            var pol = po.Lines.First(l => l.ItemCode == al.ItemCode);
            var outstanding = Math.Max(0, pol.Qty - pol.ReceivedQty);
            var input = req.Lines.FirstOrDefault(x => x.ItemCode == al.ItemCode)?.ReceivedQty ?? 0;
            // [Q] cap received to min(shipped, outstanding)
            var rq = Math.Max(0, Math.Min(input, Math.Min(al.ShippedQty, outstanding)));
            var condition = rq < al.ShippedQty ? "Short" : "Good";   // under-receipt = Short
            pol.ReceivedQty = Math.Min(pol.Qty, pol.ReceivedQty + rq);
            grnLines.Add(new GrnLine { ItemCode = al.ItemCode, Description = al.Description, ExpectedQty = al.ShippedQty, ReceivedQty = rq, Condition = condition });
        }

        var code = await codes.NextAsync("GRN", ct);
        // Receipt date is a business date, not an instant: it's the Malaysia calendar day of receipt.
        // Kuching is a fixed UTC+8 (no DST), so the local day is UtcNow+8 as a plain DateOnly — no
        // string formatting, no stored offset (Slice H T4 retired the AddHours(8)-into-a-string hack).
        var receivedDate = DateOnly.FromDateTime(clock.UtcNow.AddHours(8));
        var grn = new Grn
        {
            Code = code, AsnId = asn.Id, PoId = po.Id, ReceivedDate = receivedDate,
            ReceivedBy = user.UserName ?? "Procurement", NsId = $"NS-IR-{code[^4..]}", Lines = grnLines, CreatedUtc = clock.UtcNow,
        };
        db.Grns.Add(grn);
        // Under-receipt: ASN becomes Received (no longer in transit), so the shortfall
        // re-enters remaining-to-ship automatically.
        asn.MarkReceived();
        asn.GrnCode = grn.Code;
        asn.UpdatedUtc = clock.UtcNow;

        if (po.Status is PoStatus.Issued or PoStatus.Acknowledged or PoStatus.PartiallyReceived)
        {
            var allReceived = po.Lines.Sum(l => l.ReceivedQty) >= po.Lines.Sum(l => l.Qty);
            po.RecordReceipt(allReceived);
            po.UpdatedUtc = clock.UtcNow;
        }
        await db.SaveChangesAsync(ct);
        await netsuite.PushItemReceiptAsync(grn.Code, ct);   // stub
        await audit.WriteAsync("Po", po.Code, "Goods receipt posted", after: $"{grn.Code} against {asn.Code}", ct: ct);
        await audit.WriteAsync("Asn", asn.Code, "Goods receipt posted", after: grn.Code, ct: ct);
        return await MapGrn(grn, ct);
    }

    // ---- helpers ----

    private void EnsureInternal()
    {
        if (user.VendorId is not null) throw new ForbiddenException("Only the buyer can receive goods.");
    }

    private async Task<PurchaseOrder> LoadPo(Guid id, CancellationToken ct) =>
        await db.PurchaseOrders.Include(p => p.Lines).FirstOrDefaultAsync(p => p.Id == id, ct)
        ?? throw new NotFoundException($"PO {id} not found.");

    private async Task<Dictionary<string, decimal>> InTransitByItem(Guid poId, CancellationToken ct)
    {
        var asns = await db.Asns.AsNoTracking().Include(a => a.Lines)
            .Where(a => a.PoId == poId && a.Status == AsnStatus.InTransit).ToListAsync(ct);
        return asns.SelectMany(a => a.Lines)
            .GroupBy(l => l.ItemCode)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.ShippedQty));
    }

    private async Task<Dictionary<Guid, string>> PoCodes(CancellationToken ct) =>
        await db.PurchaseOrders.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Code, ct);

    private async Task<Dictionary<Guid, string>> VendorNames(CancellationToken ct) =>
        await db.Vendors.AsNoTracking().ToDictionaryAsync(v => v.Id, v => v.Name, ct);

    private async Task<GrnDetailDto> MapGrn(Grn g, CancellationToken ct)
    {
        var pos = await PoCodes(ct);
        var asnCode = (await db.Asns.AsNoTracking().FirstOrDefaultAsync(a => a.Id == g.AsnId, ct))?.Code ?? "—";
        return new GrnDetailDto(g.Id, g.Code, g.AsnId, asnCode, pos.GetValueOrDefault(g.PoId, "—"), g.ReceivedDate, g.NsId,
            g.Lines.Select(l => new GrnLineDto(l.ItemCode, l.Description, l.ExpectedQty, l.ReceivedQty, l.Condition)).ToList());
    }
}
