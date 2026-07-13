using eProcure.Application.CustomFields;
using eProcure.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eProcure.Infrastructure.Referencing;

/// <summary>Entry-form placements — PER-FORM grain (one FieldReference per form the field
/// is placed on, TargetId = the form id), so the future per-form-applicability slice is
/// just more rows from this provider, never a registry retrofit. IsMandatory surfaces
/// RequiredOnForm so the impact report can red-flag the dangerous clears.</summary>
public sealed class EntryFormReferenceProvider(AppDbContext db) : ICustomFieldReferenceProvider
{
    public string ConsumerName => "Entry Forms";

    public async Task<IReadOnlyList<FieldReference>> FindReferencesAsync(Guid fieldDefId, string fieldCode, CancellationToken ct = default)
    {
        var rows = await (
            from f in db.EntryFormFields.AsNoTracking()
            join d in db.EntryFormDefs.AsNoTracking() on f.FormDefId equals d.Id
            where f.FieldKey == fieldCode
            select new { d.Id, d.Name, d.RecordType, f.RequiredOnForm }).ToListAsync(ct);
        return rows.Select(r => new FieldReference(
            ConsumerName, FieldRefKind.FormPlacement, r.Id, r.Name,
            $"{r.RecordType} form{(r.RequiredOnForm ? " · MANDATORY at submit" : "")}", r.RequiredOnForm)).ToList();
    }
}

/// <summary>Saved-view columns and filters (fields), and filter VALUES (list values).</summary>
public sealed class SavedViewReferenceProvider(AppDbContext db)
    : ICustomFieldReferenceProvider, ICustomListValueReferenceProvider
{
    public string ConsumerName => "Saved Views";

    public async Task<IReadOnlyList<FieldReference>> FindReferencesAsync(Guid fieldDefId, string fieldCode, CancellationToken ct = default)
    {
        var views = await db.SavedViews.AsNoTracking().Include(v => v.Columns).Include(v => v.Filters)
            .Where(v => v.Columns.Any(c => c.FieldKey == fieldCode) || v.Filters.Any(f => f.FieldKey == fieldCode))
            .ToListAsync(ct);
        var refs = new List<FieldReference>();
        foreach (var v in views)
        {
            var owner = v.IsSystem ? "system view" : v.IsShared ? "shared view" : $"private view ({v.OwnerUserId})";
            if (v.Columns.Any(c => c.FieldKey == fieldCode))
                refs.Add(new FieldReference(ConsumerName, FieldRefKind.ViewColumn, v.Id, v.Name, $"column · {owner}"));
            if (v.Filters.Any(f => f.FieldKey == fieldCode))
                refs.Add(new FieldReference(ConsumerName, FieldRefKind.ViewFilter, v.Id, v.Name, $"filter criterion · {owner}"));
        }
        return refs;
    }

    public async Task<IReadOnlyList<FieldReference>> FindReferencesAsync(Guid listId, string listCode, string valueCode, CancellationToken ct = default)
    {
        // A list VALUE is referenced when a view filters a field bound to this list with
        // this value as the criterion value.
        var boundCodes = await db.CustomFieldDefs.AsNoTracking()
            .Where(d => d.CustomListId == listId).Select(d => d.Code).ToListAsync(ct);
        if (boundCodes.Count == 0) return [];
        var views = await db.SavedViews.AsNoTracking().Include(v => v.Filters)
            .Where(v => v.Filters.Any(f => boundCodes.Contains(f.FieldKey) && (f.Value == valueCode || f.Value2 == valueCode)))
            .ToListAsync(ct);
        return views.Select(v => new FieldReference(
            ConsumerName, FieldRefKind.ViewFilter, v.Id, v.Name,
            $"filter VALUE '{valueCode}' · {(v.IsShared || v.IsSystem ? "shared" : $"private ({v.OwnerUserId})")}")).ToList();
    }
}

/// <summary>Segments hold no structural references to custom fields or list values TODAY —
/// this provider ships registered-but-empty so the consumer pattern is complete and future
/// segment references (e.g. list-backed segment sources) have a home. Deliberate, ruled.</summary>
public sealed class SegmentReferenceProvider : ICustomFieldReferenceProvider, ICustomListValueReferenceProvider
{
    public string ConsumerName => "Segments";
    public Task<IReadOnlyList<FieldReference>> FindReferencesAsync(Guid fieldDefId, string fieldCode, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<FieldReference>>([]);
    public Task<IReadOnlyList<FieldReference>> FindReferencesAsync(Guid listId, string listCode, string valueCode, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<FieldReference>>([]);
}
