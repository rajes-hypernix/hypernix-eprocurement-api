using eProcure.Application.Abstractions;
using eProcure.Application.Search;
using eProcure.Domain.Sourcing;
using eProcure.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eProcure.Infrastructure.Services;

/// <summary>
/// Global search over code + title/name fields (D2). Scoping happens IN the
/// queries — a vendor principal's search never materialises another vendor's
/// rows, so a cross-vendor code match returns zero hits rather than being
/// filtered late. Mirrors RfqService.ListAsync's invitation rule.
/// </summary>
public sealed class SearchService(AppDbContext db, ICurrentUser user) : ISearchService
{
    private const int PerTypeCap = 8;

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string q, CancellationToken ct = default)
    {
        var nq = (q ?? "").Trim().ToLowerInvariant();
        if (nq.Length < 2) return [];

        var vid = user.VendorId;
        var hits = new List<SearchHit>();

        var vendors = db.Vendors.AsNoTracking()
            .Where(v => v.Code.ToLower().Contains(nq) || v.Name.ToLower().Contains(nq) || v.RegisteredName.ToLower().Contains(nq));
        if (vid is { } v1) vendors = vendors.Where(v => v.Id == v1);   // a vendor finds only itself
        hits.AddRange((await vendors.OrderBy(v => v.Code).Take(PerTypeCap).Select(v => new { v.Id, v.Code, v.RegisteredName }).ToListAsync(ct))
            .Select(v => new SearchHit("Vendor", v.Id, v.Code, v.RegisteredName)));

        if (vid is null)
        {
            // PRs are buyer-side records: no vendor principal ever sees one.
            var prs = await db.PurchaseRequisitions.AsNoTracking()
                .Where(p => p.Code.ToLower().Contains(nq) || p.Memo.ToLower().Contains(nq))
                .OrderByDescending(p => p.Code).Take(PerTypeCap)
                .Select(p => new { p.Id, p.Code, p.Memo }).ToListAsync(ct);
            hits.AddRange(prs.Select(p => new SearchHit("Requisition", p.Id, p.Code, p.Memo)));
        }

        var rfqs = db.Rfqs.AsNoTracking()
            .Where(r => r.Code.ToLower().Contains(nq) || r.Title.ToLower().Contains(nq));
        if (vid is { } v2)
            rfqs = rfqs.Where(r => r.Invitations.Any(i => i.VendorId == v2 && i.Status != RfqInvitationStatus.Rescinded));
        hits.AddRange((await rfqs.OrderByDescending(r => r.Code).Take(PerTypeCap).Select(r => new { r.Id, r.Code, r.Title }).ToListAsync(ct))
            .Select(r => new SearchHit("Rfq", r.Id, r.Code, r.Title)));

        var pos = db.PurchaseOrders.AsNoTracking().Where(p => p.Code.ToLower().Contains(nq));
        if (vid is { } v3) pos = pos.Where(p => p.VendorId == v3);
        var poRows = await pos.OrderByDescending(p => p.Code).Take(PerTypeCap).Select(p => new { p.Id, p.Code, p.VendorId }).ToListAsync(ct);
        var poVendorNames = await db.Vendors.AsNoTracking()
            .Where(x => poRows.Select(p => p.VendorId).Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.RegisteredName, ct);
        hits.AddRange(poRows.Select(p => new SearchHit("PurchaseOrder", p.Id, p.Code, poVendorNames.GetValueOrDefault(p.VendorId, ""))));

        var invoices = db.Invoices.AsNoTracking()
            .Where(i => i.Code.ToLower().Contains(nq) || i.InvoiceNo.ToLower().Contains(nq));
        if (vid is { } v4) invoices = invoices.Where(i => i.VendorId == v4);
        hits.AddRange((await invoices.OrderByDescending(i => i.Code).Take(PerTypeCap).Select(i => new { i.Id, i.Code, i.InvoiceNo }).ToListAsync(ct))
            .Select(i => new SearchHit("Invoice", i.Id, i.Code, i.InvoiceNo)));

        return hits;
    }
}
