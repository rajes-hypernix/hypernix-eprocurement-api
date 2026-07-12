using eProcure.Domain.Views;
using eProcure.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eProcure.Infrastructure.Services;

/// <summary>CF6-T1: which record types carry line-scoped custom fields, and which line ids
/// belong to a record — the server-side ownership check behind every line-grain write
/// (first delivery per the locked ruling: PR / PO / RFQ lines; ASN/GRN follow the pattern).</summary>
internal static class LineOwnership
{
    public static readonly IReadOnlySet<RecordType> SupportedTypes =
        new HashSet<RecordType> { RecordType.Requisition, RecordType.PurchaseOrder, RecordType.Rfq };

    public static async Task<HashSet<Guid>> LineIdsOfAsync(AppDbContext db, RecordType type, Guid recordId, CancellationToken ct) =>
        type switch
        {
            RecordType.Requisition => (await db.PurchaseRequisitions.AsNoTracking().Include(p => p.Lines)
                .FirstAsync(p => p.Id == recordId, ct)).Lines.Select(l => l.Id).ToHashSet(),
            RecordType.PurchaseOrder => (await db.PurchaseOrders.AsNoTracking().Include(p => p.Lines)
                .FirstAsync(p => p.Id == recordId, ct)).Lines.Select(l => l.Id).ToHashSet(),
            RecordType.Rfq => (await db.Rfqs.AsNoTracking().Include(r => r.Lines)
                .FirstAsync(r => r.Id == recordId, ct)).Lines.Select(l => l.Id).ToHashSet(),
            _ => throw new Application.CustomFields.CustomFieldValidationException(
                $"Line fields aren't supported on {type} yet."),
        };
}
