using eProcure.Application.CustomFields;
using eProcure.Domain.Views;
using eProcure.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eProcure.Infrastructure.Referencing;

/// <summary>CF-FIX4-T6: form placements of a segment (seg_* keys in EntryFormField — the
/// L1 placement object). Def grain only: VALUES are never individually placed on forms.</summary>
public sealed class SegmentFormPlacementProvider(AppDbContext db) : ISegmentReferenceProvider
{
    public string ConsumerName => "Entry Forms";

    public async Task<IReadOnlyList<FieldReference>> FindReferencesAsync(Guid segmentDefId, string segmentCode, Guid? valueId, CancellationToken ct = default)
    {
        if (valueId is not null) return [];
        var rows = await (
            from f in db.EntryFormFields.AsNoTracking()
            join d in db.EntryFormDefs.AsNoTracking() on f.FormDefId equals d.Id
            where f.FieldKey == segmentCode
            select new { d.Id, d.Name, d.RecordType, f.RequiredOnForm }).ToListAsync(ct);
        return rows.Select(r => new FieldReference(
            ConsumerName, FieldRefKind.FormPlacement, r.Id, r.Name,
            $"{r.RecordType} form{(r.RequiredOnForm ? " · MANDATORY at submit" : "")}", r.RequiredOnForm)).ToList();
    }
}

/// <summary>Saved-view columns/filters on the segment key (def grain) and filter VALUES
/// matching a segment value's code (value grain).</summary>
public sealed class SavedViewSegmentProvider(AppDbContext db) : ISegmentReferenceProvider
{
    public string ConsumerName => "Saved Views";

    public async Task<IReadOnlyList<FieldReference>> FindReferencesAsync(Guid segmentDefId, string segmentCode, Guid? valueId, CancellationToken ct = default)
    {
        if (valueId is null)
        {
            var views = await db.SavedViews.AsNoTracking().Include(v => v.Columns).Include(v => v.Filters)
                .Where(v => v.Columns.Any(c => c.FieldKey == segmentCode) || v.Filters.Any(f => f.FieldKey == segmentCode))
                .ToListAsync(ct);
            var refs = new List<FieldReference>();
            foreach (var v in views)
            {
                var owner = v.IsSystem ? "system view" : v.IsShared ? "shared view" : $"private view ({v.OwnerUserId})";
                if (v.Columns.Any(c => c.FieldKey == segmentCode))
                    refs.Add(new FieldReference(ConsumerName, FieldRefKind.ViewColumn, v.Id, v.Name, $"column · {owner}"));
                if (v.Filters.Any(f => f.FieldKey == segmentCode))
                    refs.Add(new FieldReference(ConsumerName, FieldRefKind.ViewFilter, v.Id, v.Name, $"filter criterion · {owner}"));
            }
            return refs;
        }
        // Value grain: a view filtering the segment key WITH this value's code as criterion.
        var code = await db.SegmentValues.AsNoTracking().Where(v => v.Id == valueId).Select(v => v.Code).FirstOrDefaultAsync(ct);
        if (code is null) return [];
        var hits = await db.SavedViews.AsNoTracking().Include(v => v.Filters)
            .Where(v => v.Filters.Any(f => f.FieldKey == segmentCode && (f.Value == code || f.Value2 == code)))
            .ToListAsync(ct);
        return hits.Select(v => new FieldReference(
            ConsumerName, FieldRefKind.ViewFilter, v.Id, v.Name,
            $"filter VALUE '{code}' · {(v.IsShared || v.IsSystem ? "shared" : $"private ({v.OwnerUserId})")}")).ToList();
    }
}

/// <summary>The assignment store: SegmentAssignments joined to record lifecycle (the same
/// FAIL-CLOSED RecordLifecycle allowlist as custom-field values). Purge removes ONLY
/// allowlisted-historical rows and snapshots each one.</summary>
public sealed class SegmentAssignmentDataProvider(AppDbContext db) : ISegmentDataProvider
{
    public string StoreName => "Record assignments";

    public async Task<DataReferenceSummary> CountAssignmentsAsync(Guid segmentDefId, Guid? valueId, CancellationToken ct = default)
    {
        var rows = await Rows(segmentDefId, valueId).AsNoTracking()
            .Select(a => new { a.RecordType, a.RecordId, a.LineId }).ToListAsync(ct);
        var counts = new List<DataTypeCount>();
        var live = 0; var historical = 0;
        foreach (var grp in rows.GroupBy(r => r.RecordType))
        {
            var map = await RecordLifecycle.ClassifyAsync(db, grp.Key, grp.Select(r => (r.RecordId, r.LineId)).ToList(), ct);
            var h = grp.Count(r => map.GetValueOrDefault((r.RecordId, r.LineId)));
            counts.Add(new DataTypeCount(grp.Key.ToString(), grp.Count() - h, h));
            live += grp.Count() - h; historical += h;
        }
        return new DataReferenceSummary(StoreName, live, historical, counts);
    }

    public async Task<PurgeSnapshot> PurgeHistoricalAsync(Guid segmentDefId, Guid? valueId, CancellationToken ct = default)
    {
        var rows = await Rows(segmentDefId, valueId).ToListAsync(ct);
        var labels = await db.SegmentValues.AsNoTracking().Where(v => v.SegmentDefId == segmentDefId)
            .ToDictionaryAsync(v => v.Id, v => $"{v.Code} · {v.Label}", ct);
        var removed = new List<PurgedValue>();
        foreach (var grp in rows.GroupBy(r => r.RecordType))
        {
            var map = await RecordLifecycle.ClassifyAsync(db, grp.Key, grp.Select(r => (r.RecordId, r.LineId)).ToList(), ct);
            foreach (var row in grp)
            {
                if (!map.GetValueOrDefault((row.RecordId, row.LineId))) continue;   // Live/unknown → NEVER purge
                removed.Add(new PurgedValue(row.RecordType.ToString(), row.RecordId, row.LineId,
                    await RecordLabelAsync(row.RecordType, row.RecordId, ct), labels.GetValueOrDefault(row.SegmentValueId)));
                db.SegmentAssignments.Remove(row);
            }
        }
        return new PurgeSnapshot(StoreName, removed);
    }

    private IQueryable<Domain.Segments.SegmentAssignment> Rows(Guid defId, Guid? valueId) =>
        db.SegmentAssignments.Where(a => a.SegmentDefId == defId && (valueId == null || a.SegmentValueId == valueId));

    private async Task<string> RecordLabelAsync(RecordType type, Guid id, CancellationToken ct) => type switch
    {
        RecordType.PurchaseOrder => await db.PurchaseOrders.AsNoTracking().Where(p => p.Id == id).Select(p => p.Code).FirstOrDefaultAsync(ct) ?? id.ToString(),
        RecordType.Invoice => await db.Invoices.AsNoTracking().Where(i => i.Id == id).Select(i => i.Code).FirstOrDefaultAsync(ct) ?? id.ToString(),
        RecordType.Requisition => await db.PurchaseRequisitions.AsNoTracking().Where(p => p.Id == id).Select(p => p.Code).FirstOrDefaultAsync(ct) ?? id.ToString(),
        RecordType.Rfq => await db.Rfqs.AsNoTracking().Where(r => r.Id == id).Select(r => r.Code).FirstOrDefaultAsync(ct) ?? id.ToString(),
        _ => id.ToString(),
    };
}
