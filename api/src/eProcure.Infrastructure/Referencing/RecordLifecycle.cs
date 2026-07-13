using eProcure.Domain.Procurement;
using eProcure.Domain.Sourcing;
using eProcure.Domain.Views;
using eProcure.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eProcure.Infrastructure.Referencing;

/// <summary>
/// CF-FIX3: Live vs Historical, decided by each record type's TERMINAL status — an
/// explicit ALLOWLIST per type, and FAIL CLOSED everywhere else: a record that cannot
/// be found, a type with no mapping, or a future enum member is LIVE and therefore
/// never purgeable (pinned by ReferenceProviderTests).
///
/// The per-type table (verified against the domain enums):
///   PurchaseOrder  → Matched | Closed | Discrepancy
///   Invoice        → Paid
///   Requisition    → header Cancelled; PR LINES (line-grain values) → Closed | Cancelled
///   Rfq            → Awarded | Cancelled   (RfqStatus.Closed = bidding closed, evaluation
///                    PENDING — that is a live transition, NOT historical)
///   Vendor / Onboarding → never historical (master data)
/// </summary>
public static class RecordLifecycle
{
    /// <summary>records (and optionally lines) → historical? One query per record type.</summary>
    public static async Task<Dictionary<(Guid RecordId, Guid? LineId), bool>> ClassifyAsync(
        AppDbContext db, RecordType type, IReadOnlyCollection<(Guid RecordId, Guid? LineId)> keys, CancellationToken ct)
    {
        var result = keys.ToDictionary(k => k, _ => false);   // default LIVE (fail closed)
        var recordIds = keys.Select(k => k.RecordId).Distinct().ToList();

        switch (type)
        {
            case RecordType.PurchaseOrder:
            {
                var historical = await db.PurchaseOrders.AsNoTracking()
                    .Where(p => recordIds.Contains(p.Id)
                        && (p.Status == PoStatus.Matched || p.Status == PoStatus.Closed || p.Status == PoStatus.Discrepancy))
                    .Select(p => p.Id).ToListAsync(ct);
                foreach (var k in keys.Where(k => historical.Contains(k.RecordId))) result[k] = true;
                break;
            }
            case RecordType.Invoice:
            {
                var historical = await db.Invoices.AsNoTracking()
                    .Where(i => recordIds.Contains(i.Id) && i.Status == InvoiceStatus.Paid)
                    .Select(i => i.Id).ToListAsync(ct);
                foreach (var k in keys.Where(k => historical.Contains(k.RecordId))) result[k] = true;
                break;
            }
            case RecordType.Requisition:
            {
                var prs = await db.PurchaseRequisitions.AsNoTracking().Include(p => p.Lines)
                    .Where(p => recordIds.Contains(p.Id)).ToListAsync(ct);
                foreach (var k in keys)
                {
                    var pr = prs.FirstOrDefault(p => p.Id == k.RecordId);
                    if (pr is null) continue;   // unknown record → Live
                    if (k.LineId is { } lineId)
                    {
                        var line = pr.Lines.FirstOrDefault(l => l.Id == lineId);
                        result[k] = line is not null
                            && line.LifecycleStatus is PrLineStatus.Closed or PrLineStatus.Cancelled;
                    }
                    else
                    {
                        result[k] = pr.HeaderStatus == PrHeaderStatus.Cancelled;
                    }
                }
                break;
            }
            case RecordType.Rfq:
            {
                var historical = await db.Rfqs.AsNoTracking()
                    .Where(r => recordIds.Contains(r.Id)
                        && (r.Status == RfqStatus.Awarded || r.Status == RfqStatus.Cancelled))
                    .Select(r => r.Id).ToListAsync(ct);
                foreach (var k in keys.Where(k => historical.Contains(k.RecordId))) result[k] = true;
                break;
            }
            // Vendor, Onboarding, and any FUTURE record type: no mapping → everything stays
            // Live (the fail-closed default set above). Deliberately no `default:` branch
            // that could be mistaken for completeness.
        }
        return result;
    }
}
