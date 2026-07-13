using System.Globalization;
using eProcure.Application;
using eProcure.Application.Abstractions;
using eProcure.Application.Authorization;
using eProcure.Application.CustomFields;
using eProcure.Application.Onboarding;
using eProcure.Application.Procurement;
using eProcure.Application.Sourcing;
using eProcure.Application.Suppliers;
using eProcure.Application.Views;
using eProcure.Domain.CustomFields;
using eProcure.Domain.Views;
using eProcure.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eProcure.Infrastructure.Services;

/// <summary>
/// Custom fields (D5). Def creation inserts the FieldRegistry row (Kind=Custom) in the same
/// transaction — the registry is how the D3/D4 view/aggregate/KPI chain lights up for free.
/// Code/RecordType/DataType are immutable; lifecycle per ruling (values ever written →
/// deactivate-only; zero values → hard-delete removes the registry row). Value reads/writes
/// DECORATE the record type's existing scoped detail fetch (third use of the two-layer
/// convention: static action + dynamic View* + scoped fetch) — a principal who cannot reach
/// the record cannot reach its custom values.
/// </summary>
public sealed class CustomFieldService(
    AppDbContext db,
    IClock clock,
    IRecordReachability reachability,
    IEnumerable<ICustomFieldReferenceProvider> referenceProviders,
    IEnumerable<ICustomFieldDataProvider> dataProviders,
    IAuditLog audit) : ICustomFieldService
{
    // ---------- defs (A65) ----------

    // CF-FIX2-T3: "applies to X" = an application row exists. The ONE membership predicate.
    private IQueryable<CustomFieldDef> DefsFor(RecordType type) =>
        db.CustomFieldDefs.Where(d => db.CustomFieldDefApplications.Any(a => a.FieldDefId == d.Id && a.RecordType == type));

    private async Task<List<string>> AppliedTypesAsync(Guid defId, CancellationToken ct) =>
        (await db.CustomFieldDefApplications.AsNoTracking().Where(a => a.FieldDefId == defId)
            .Select(a => a.RecordType).ToListAsync(ct)).Select(x => x.ToString()).OrderBy(x => x).ToList();

    public async Task<IReadOnlyList<CustomFieldDefDto>> ListDefsAsync(string? recordType, CancellationToken ct = default)
    {
        var q = recordType is not null ? DefsFor(Parse(recordType)) : db.CustomFieldDefs;
        var defs = await q.AsNoTracking().OrderBy(d => d.Sort).ThenBy(d => d.Label).ToListAsync(ct);
        var counts = await db.CustomFieldValues.AsNoTracking()
            .GroupBy(v => v.FieldDefId).Select(g => new { g.Key, N = g.Count() }).ToListAsync(ct);
        var apps = await db.CustomFieldDefApplications.AsNoTracking().ToListAsync(ct);
        var byDef = counts.ToDictionary(x => x.Key, x => x.N);
        var appsByDef = apps.GroupBy(a => a.FieldDefId).ToDictionary(g => g.Key, g => g.Select(a => a.RecordType.ToString()).OrderBy(x => x).ToList());
        return defs.Select(d => ToDto(d, byDef.GetValueOrDefault(d.Id), appsByDef.GetValueOrDefault(d.Id) ?? [])).ToList();
    }

    public async Task<CustomFieldDefDto> CreateDefAsync(SaveCustomFieldDefRequest req, CancellationToken ct = default)
    {
        var type = Parse(req.RecordType);
        var dataType = ParseDataType(req.DataType);
        if (string.IsNullOrWhiteSpace(req.Label))
            throw new CustomFieldValidationException("A custom field needs a label.");
        if (dataType == CustomFieldDataType.ListValue && req.CustomListId is null)
            throw new CustomFieldValidationException("A ListValue field needs a custom list binding.");
        if (dataType != CustomFieldDataType.ListValue && req.CustomListId is not null)
            throw new CustomFieldValidationException("Only ListValue fields bind a custom list.");
        if (req.CustomListId is { } listId && !await db.CustomLists.AnyAsync(l => l.Id == listId, ct))
            throw new CustomFieldValidationException("The bound custom list does not exist.");

        var scope = ParseScope(req.Scope);
        if (scope == "Line" && req.ShowInList)
            throw new CustomFieldValidationException("Show-in-list is a header-list concept — a LINE field has no header-list row. (Line-level search is deferred.)");
        if (scope == "Line" && !LineOwnership.SupportedTypes.Contains(type))
            throw new CustomFieldValidationException($"Line fields aren't supported on {type} yet — first delivery is Requisition/PurchaseOrder/Rfq lines.");

        // CF-FIX2-T1: NetSuite's CONTEXTUAL prefix convention — the user keys the meaningful
        // part, the system guarantees the namespace BY SCOPE: custbody_ (header field) or
        // custcol_ (line column). Derivable, no new data; existing cf_* codes are immutable
        // and keep resolving (the non-breaking choice — no rename, no migration).
        var prefix = scope == "Line" ? "custcol_" : "custbody_";
        string code;
        if (!string.IsNullOrWhiteSpace(req.Code))
        {
            var part = req.Code.Trim().ToLowerInvariant();
            foreach (var known in new[] { "custbody_", "custcol_", "cf_" })
                if (part.StartsWith(known)) { part = part[known.Length..]; break; }
            if (part.Length == 0)
                throw new CustomFieldValidationException($"The Internal ID needs a value after the {prefix} prefix.");
            if (!System.Text.RegularExpressions.Regex.IsMatch(part, "^[a-z0-9_]+$"))
                throw new CustomFieldValidationException("The Internal ID may only use letters, digits and underscores.");
            code = prefix + part;
        }
        else
        {
            code = prefix + new string(req.Label.Trim().ToLowerInvariant()
                .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray()).Trim('_');
        }
        if (code.Length > 60) code = code[..60];
        if (await db.CustomFieldDefs.AnyAsync(d => d.Code == code, ct))
            throw new CustomFieldValidationException($"A custom field with Internal ID '{code}' already exists — choose another.");
        if (FieldRegistrySeed.Rows.Any(r => r.RecordType == type && string.Equals(r.FieldKey, code, StringComparison.OrdinalIgnoreCase)))
            throw new CustomFieldValidationException($"'{code}' collides with a native field key.");

        // CF-FIX2-T3: the applies-to SET (NetSuite). Absent → [RecordType] (wire back-compat).
        var applied = (req.RecordTypes is { Count: > 0 } ? req.RecordTypes.Select(Parse) : [type])
            .Distinct().ToList();
        foreach (var t2 in applied)
        {
            if (scope == "Line" && !LineOwnership.SupportedTypes.Contains(t2))
                throw new CustomFieldValidationException($"Line fields aren't supported on {t2} yet — first delivery is Requisition/PurchaseOrder/Rfq lines.");
            if (FieldRegistrySeed.Rows.Any(r => r.RecordType == t2 && string.Equals(r.FieldKey, code, StringComparison.OrdinalIgnoreCase)))
                throw new CustomFieldValidationException($"'{code}' collides with a native field key on {t2}.");
        }

        var def = new CustomFieldDef
        {
            Code = code, Label = req.Label.Trim(), DataType = dataType,
            CustomListId = req.CustomListId, Required = req.Required,
            HelpText = req.HelpText ?? "", Sort = req.Sort,
            DisplayType = ParseDisplayType(req.DisplayType), ShowInList = req.ShowInList,
            Scope = scope,
            CreatedUtc = clock.UtcNow, UpdatedUtc = clock.UtcNow,
        };
        db.CustomFieldDefs.Add(def);
        // One application row + (for Header scope) one registry row PER APPLIED TYPE —
        // the D3/D4 integration lights up on every type the field applies to.
        foreach (var t2 in applied)
        {
            db.CustomFieldDefApplications.Add(new CustomFieldDefApplication { FieldDefId = def.Id, RecordType = t2 });
            if (scope == "Header")
                db.FieldRegistry.Add(new FieldRegistryEntry
                {
                    Id = t2 == applied[0] ? def.Id : Guid.NewGuid(),   // first row keeps the def id (back-compat)
                    RecordType = t2, FieldKey = code, Kind = FieldKind.Custom,
                    Label = def.Label, DataType = RegistryTypeOf(dataType), CustomFieldDefId = def.Id,
                });
        }
        // CF-FIX4-T4: the mandatory cascade (cascade-aware callers only — Placements null is
        // the legacy applied-but-unplaced seam). Writes the SOLE placement authority:
        // EntryFormField rows, the exact rows the designer drags and L6 removes.
        await ApplyPlacementsAsync(def, applied, req.Placements, ct);
        await db.SaveChangesAsync(ct);
        return ToDto(def, 0, applied.Select(a => a.ToString()).OrderBy(x => x).ToList());
    }

    /// <summary>CF-FIX4-T4. Validates the cascade and writes EntryFormField placement rows +
    /// the DefaultGroupTitle HINT on each application (a title, never placement storage).
    /// Rules: placements are Header-scope only; every placement's type must be applied; the
    /// form must be active and of that type; the group must belong to the form (null → the
    /// form's IsHeader group, which L3 guarantees); every APPLIED form-bearing type needs
    /// ≥1 placement when the caller is cascade-aware.</summary>
    private async Task ApplyPlacementsAsync(CustomFieldDef def, IReadOnlyList<RecordType> applied,
        IReadOnlyList<FieldPlacementRequest>? placements, CancellationToken ct, IReadOnlyList<RecordType>? onlyTypes = null)
    {
        if (placements is null) return;
        if (def.Scope == "Line" && placements.Count > 0)
            throw new CustomFieldValidationException("Line fields are sublist columns — they have no group placement (L4).");
        var wanted = placements.Select(p => (Type: Parse(p.RecordType), p.FormId, p.GroupId)).ToList();
        if (wanted.Select(w => w.FormId).Distinct().Count() != wanted.Count)
            throw new CustomFieldValidationException("A field is placed at most once per form.");
        var scopeTypes = (onlyTypes ?? applied).ToList();
        foreach (var w in wanted)
            if (!applied.Contains(w.Type))
                throw new CustomFieldValidationException($"A placement names {w.Type}, which is not in the applies-to set — placement and applies-to can never disagree.");
        foreach (var t2 in scopeTypes)
        {
            var hasForms = await db.EntryFormDefs.AnyAsync(f => f.RecordType == t2 && f.Active, ct);
            if (hasForms && def.Scope == "Header" && wanted.All(w => w.Type != t2))
                throw new CustomFieldValidationException($"{t2} needs a form placement — pick which form(s) this field appears on (the group defaults to Header).");
        }
        foreach (var w in wanted.Where(x => scopeTypes.Contains(x.Type)))
        {
            var form = await db.EntryFormDefs.FirstOrDefaultAsync(f => f.Id == w.FormId, ct)
                ?? throw new CustomFieldValidationException("A placement names a form that does not exist.");
            if (form.RecordType != w.Type || !form.Active)
                throw new CustomFieldValidationException($"'{form.Name}' is not an active {w.Type} form.");
            var group = w.GroupId is { } gid
                ? await db.EntryFormGroups.FirstOrDefaultAsync(g => g.Id == gid && g.FormDefId == form.Id, ct)
                    ?? throw new CustomFieldValidationException($"The chosen group does not belong to '{form.Name}'.")
                : await db.EntryFormGroups.FirstAsync(g => g.FormDefId == form.Id && g.IsHeader, ct);   // L3: cannot miss
            if (await db.EntryFormFields.AnyAsync(x => x.FormDefId == form.Id && x.FieldKey == def.Code, ct))
                continue;   // already placed there (idempotent for update-adds)
            var maxSort = await db.EntryFormFields.Where(x => x.FormDefId == form.Id)
                .Select(x => (int?)x.Sort).MaxAsync(ct) ?? -1;
            db.EntryFormFields.Add(new Domain.Forms.EntryFormField
            {
                Id = Guid.NewGuid(), FormDefId = form.Id, FieldKey = def.Code, GroupId = group.Id,
                Sort = maxSort + 1, DisplayType = Domain.Forms.EntryFormDisplayType.Normal,
                RequiredOnForm = false,
            });
            var app = db.CustomFieldDefApplications.Local.FirstOrDefault(a => a.FieldDefId == def.Id && a.RecordType == w.Type)
                ?? await db.CustomFieldDefApplications.FirstOrDefaultAsync(a => a.FieldDefId == def.Id && a.RecordType == w.Type, ct);
            if (app is not null && app.DefaultGroupTitle is null) app.DefaultGroupTitle = group.Title;   // the HINT
            await audit.WriteAsync("CustomField", def.Code, "Placed on form",
                before: null, after: $"{form.Name} · {group.Title}", ct: ct);
        }
    }

    public async Task<CustomFieldDefDto> UpdateDefAsync(Guid id, SaveCustomFieldDefRequest req, CancellationToken ct = default)
    {
        var def = await Load(id, ct);
        if (ParseDataType(req.DataType) != def.DataType)
            throw new CustomFieldValidationException("Code and data type are immutable — create a new field instead.");
        if (ParseScope(req.Scope) != def.Scope)
            throw new CustomFieldValidationException("Scope (header vs line) is immutable — values already live at that grain; create a new field instead.");
        if (def.Scope == "Line" && req.ShowInList)
            throw new CustomFieldValidationException("Show-in-list is a header-list concept — a LINE field cannot use it.");
        if (string.IsNullOrWhiteSpace(req.Label))
            throw new CustomFieldValidationException("A custom field needs a label.");
        def.Label = req.Label.Trim();
        def.Required = req.Required;
        def.HelpText = req.HelpText ?? "";
        def.DisplayType = ParseDisplayType(req.DisplayType);
        def.ShowInList = req.ShowInList;
        def.Sort = req.Sort;
        def.UpdatedUtc = clock.UtcNow;
        foreach (var reg in await db.FieldRegistry.Where(r => r.CustomFieldDefId == def.Id).ToListAsync(ct))
            reg.Label = def.Label;   // one registry row per applied type; line defs carry none (CF6-T1)

        // CF-FIX2-T3: edit the applies-to set. Adding a type adds its application (+ registry)
        // row; REMOVING one is guarded — values stored under that type would be orphaned.
        if (req.RecordTypes is { Count: > 0 })
        {
            var wanted = req.RecordTypes.Select(Parse).Distinct().ToList();
            var current = await db.CustomFieldDefApplications.Where(a => a.FieldDefId == def.Id).ToListAsync(ct);
            var added = new List<RecordType>();
            foreach (var t2 in wanted)
            {
                if (def.Scope == "Line" && !LineOwnership.SupportedTypes.Contains(t2))
                    throw new CustomFieldValidationException($"Line fields aren't supported on {t2} yet.");
                if (current.All(a => a.RecordType != t2))
                {
                    added.Add(t2);
                    db.CustomFieldDefApplications.Add(new CustomFieldDefApplication { FieldDefId = def.Id, RecordType = t2 });
                    if (def.Scope == "Header" && !await db.FieldRegistry.AnyAsync(r => r.CustomFieldDefId == def.Id && r.RecordType == t2, ct))
                        db.FieldRegistry.Add(new FieldRegistryEntry
                        {
                            Id = Guid.NewGuid(), RecordType = t2, FieldKey = def.Code, Kind = FieldKind.Custom,
                            Label = def.Label, DataType = RegistryTypeOf(def.DataType), CustomFieldDefId = def.Id,
                        });
                }
            }
            foreach (var gone in current.Where(a => !wanted.Contains(a.RecordType)).ToList())
            {
                if (await db.CustomFieldValues.AnyAsync(v => v.FieldDefId == def.Id && v.RecordType == gone.RecordType, ct))
                    throw new CustomFieldValidationException(
                        $"Cannot remove {gone.RecordType} — this field has stored values there. Deactivate the field instead.");
                db.CustomFieldDefApplications.Remove(gone);
                var regGone = await db.FieldRegistry.FirstOrDefaultAsync(r => r.CustomFieldDefId == def.Id && r.RecordType == gone.RecordType, ct);
                if (regGone is not null) db.FieldRegistry.Remove(regGone);
                // CF-FIX4-T4 (bidirectional invariant, direction A): a type leaving the
                // applies-to set takes its PLACEMENT rows with it — a pure L6 layout op
                // (zero values here, guarded above), audited per form.
                var placements = await db.EntryFormFields
                    .Where(x => x.FieldKey == def.Code
                        && db.EntryFormDefs.Any(d2 => d2.Id == x.FormDefId && d2.RecordType == gone.RecordType))
                    .ToListAsync(ct);
                foreach (var p in placements)
                {
                    db.EntryFormFields.Remove(p);
                    await audit.WriteAsync("CustomField", def.Code, "Unplaced (applies-to removed)",
                        before: gone.RecordType.ToString(), after: null, ct: ct);
                }
            }
            // Newly-added types run the SAME cascade (cascade-aware callers must place them).
            await ApplyPlacementsAsync(def, wanted, req.Placements, ct, onlyTypes: added);
        }
        await db.SaveChangesAsync(ct);
        return ToDto(def, await db.CustomFieldValues.CountAsync(v => v.FieldDefId == def.Id, ct), await AppliedTypesAsync(def.Id, ct));
    }

    public async Task<CustomFieldDefDto> SetDefActiveAsync(Guid id, bool active, CancellationToken ct = default)
    {
        var def = await Load(id, ct);
        def.Active = active;
        def.UpdatedUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToDto(def, await db.CustomFieldValues.CountAsync(v => v.FieldDefId == def.Id, ct), await AppliedTypesAsync(def.Id, ct));
    }

    /// <summary>CF-FIX3-T2: the impact report — LOOPS the registered providers, never names
    /// a consumer (the anti-rot contract, pinned by ExtensibilityProofTests).</summary>
    public async Task<ImpactReportDto> GetReferencesAsync(Guid id, CancellationToken ct = default)
    {
        var def = await Load(id, ct);
        var refs = new List<FieldReference>();
        foreach (var p in referenceProviders)
            refs.AddRange(await p.FindReferencesAsync(def.Id, def.Code, ct));
        var data = new List<DataReferenceSummary>();
        foreach (var p in dataProviders)
            data.Add(await p.CountValuesAsync(def.Id, ct));
        var live = data.Sum(d => d.LiveCount);
        var historical = data.Sum(d => d.HistoricalCount);
        var blocked =
            refs.Count > 0 ? $"Still referenced by {string.Join(", ", refs.Select(r => r.ConsumerName).Distinct())} — clear those first (the list below is the to-do)."
            : live > 0 ? $"{live} value(s) live on OPEN records — a field in live use is never deleted."
            : null;
        return new ImpactReportDto(refs, data, live, historical,
            CanDelete: refs.Count == 0 && live == 0 && historical == 0,
            CanPurge: refs.Count == 0 && live == 0 && historical > 0,
            blocked ?? (historical > 0 ? $"{historical} value(s) remain on closed/historical records — Purge (governed) removes them with a snapshot." : null));
    }

    /// <summary>CF-FIX3-T3 Tier 2. Delete requires ZERO config references (every registered
    /// provider) AND zero data values. Closes the pre-CF-FIX3 bug where a zero-value field
    /// that was still a saved-view column hard-deleted.</summary>
    public async Task DeleteDefAsync(Guid id, CancellationToken ct = default)
    {
        var def = await Load(id, ct);
        var report = await GetReferencesAsync(id, ct);
        if (!report.CanDelete)
            throw new Domain.DomainRuleException(report.BlockedReason
                ?? "This field cannot be deleted — check its impact report.");
        var reg = await db.FieldRegistry.Where(r => r.CustomFieldDefId == def.Id).ToListAsync(ct);
        db.FieldRegistry.RemoveRange(reg);
        db.CustomFieldDefApplications.RemoveRange(await db.CustomFieldDefApplications.Where(a => a.FieldDefId == def.Id).ToListAsync(ct));
        db.CustomFieldDefs.Remove(def);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>CF-FIX3-T3 Tier 3 — the governed purge. ONE transaction: re-verify inside it
    /// (no check-then-act race), snapshot EVERY removed value to the audit log (record,
    /// line, rendered value), remove only allowlisted-HISTORICAL values via the registered
    /// data providers, then delete the field. Live/unknown values are untouchable —
    /// providers fail closed. Gated on PurgeCustomFieldHistory (A73) at the endpoint.</summary>
    public async Task PurgeAsync(Guid id, CancellationToken ct = default)
    {
        var def = await Load(id, ct);
        var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            var report = await GetReferencesAsync(id, ct);   // RE-verified inside the transaction
            if (!report.CanPurge)
                throw new Domain.DomainRuleException(report.BlockedReason
                    ?? "Purge is only available when config references are clear, no live values remain, and historical values exist.");

            var totalRemoved = 0;
            foreach (var p in dataProviders)
            {
                var snapshot = await p.PurgeHistoricalAsync(def.Id, ct);
                foreach (var v in snapshot.Removed)
                    await audit.WriteAsync("CustomField", def.Code, "Purged historical value",
                        before: $"{v.RecordType} {v.RecordLabel}{(v.LineId is not null ? $" line {v.LineId}" : "")}",
                        after: v.Value, ct: ct);
                totalRemoved += snapshot.Removed.Count;
            }
            var reg = await db.FieldRegistry.Where(r => r.CustomFieldDefId == def.Id).ToListAsync(ct);
            db.FieldRegistry.RemoveRange(reg);
            db.CustomFieldDefApplications.RemoveRange(await db.CustomFieldDefApplications.Where(a => a.FieldDefId == def.Id).ToListAsync(ct));
            db.CustomFieldDefs.Remove(def);
            await db.SaveChangesAsync(ct);
            await audit.WriteAsync("CustomField", def.Code, "Purged and deleted",
                after: $"{totalRemoved} historical value(s) removed (snapshotted above); field '{def.Label}' deleted", ct: ct);
            if (tx is not null) await tx.CommitAsync(ct);
        }
        catch
        {
            if (tx is not null) await tx.RollbackAsync(ct);
            throw;
        }
        finally
        {
            if (tx is not null) await tx.DisposeAsync();
        }
    }

    // ---------- values (A66/A67) ----------

    public async Task<IReadOnlyList<CustomValueDto>> GetValuesAsync(string recordType, Guid recordId, CancellationToken ct = default)
    {
        var type = Parse(recordType);
        await reachability.RequireReachableAsync(type, recordId, ct);   // A2F-T4: the ONE guard (was a 16-line twin)
        return await MergedAsync(type, recordId, ct);
    }

    public async Task<IReadOnlyList<CustomValueDto>> SaveValuesAsync(string recordType, Guid recordId, SaveCustomValuesRequest req, CancellationToken ct = default)
    {
        var type = Parse(recordType);
        await reachability.RequireReachableAsync(type, recordId, ct);   // A2F-T4: the ONE guard (was a 16-line twin)

        var defs = await DefsFor(type).Where(d => d.Active).ToListAsync(ct);
        var byCode = defs.ToDictionary(d => d.Code, StringComparer.OrdinalIgnoreCase);
        foreach (var key in req.Values.Keys)
        {
            if (!byCode.TryGetValue(key, out var d))
                throw new CustomFieldValidationException($"Unknown or inactive custom field '{key}' for {type}.");
            if (d.Scope == "Line")
                throw new CustomFieldValidationException($"'{d.Label}' is a LINE field — its values live per line, not on the header.");
        }

        // CF6-T1: line-grain writes — every line must BELONG to this record (server-checked),
        // every key must be a Line-scope def; same typed validation as headers.
        if (req.Lines is { Count: > 0 } lines)
        {
            var lineIds = await LineOwnership.LineIdsOfAsync(db, type, recordId, ct);
            var lineExisting = await db.CustomFieldValues
                .Where(v => v.RecordType == type && v.RecordId == recordId && v.LineId != null).ToListAsync(ct);
            foreach (var (lineId, lineValues) in lines)
            {
                if (!lineIds.Contains(lineId))
                    throw new CustomFieldValidationException("That line does not belong to this record.");
                foreach (var (key, raw) in lineValues)
                {
                    if (!byCode.TryGetValue(key, out var def))
                        throw new CustomFieldValidationException($"Unknown or inactive custom field '{key}' for {type}.");
                    if (def.Scope != "Line")
                        throw new CustomFieldValidationException($"'{def.Label}' is a HEADER field — it has no line grain.");
                    var value = raw?.Trim();
                    var row = lineExisting.FirstOrDefault(v => v.FieldDefId == def.Id && v.LineId == lineId);
                    if (string.IsNullOrEmpty(value))
                    {
                        if (row is not null) db.CustomFieldValues.Remove(row);
                        continue;
                    }
                    if (def.DisplayType != "Normal" && !string.Equals(value, Render(row) ?? "", StringComparison.Ordinal))
                        throw new CustomFieldValidationException($"'{def.Label}' is {def.DisplayType.ToLowerInvariant()} — not user-editable.");
                    row ??= db.CustomFieldValues.Add(new CustomFieldValue
                    {
                        FieldDefId = def.Id, RecordType = type, RecordId = recordId,
                        DataType = def.DataType, LineId = lineId,
                    }).Entity;
                    await WriteTypedAsync(row, def, value, ct);
                    row.UpdatedUtc = clock.UtcNow;
                }
            }
        }

        var existing = await db.CustomFieldValues
            .Where(v => v.RecordType == type && v.RecordId == recordId && v.LineId == null).ToListAsync(ct);

        // CF-FIX1 repair (found by the operator's required 'Remarks' field): a LINES-ONLY
        // write must not police header-required fields it isn't touching — the header grain
        // is enforced when the header grain is written.
        if (req.Values.Count == 0 && req.Lines is { Count: > 0 })
            { await db.SaveChangesAsync(ct); return await MergedAsync(type, recordId, ct); }

        foreach (var def in defs.Where(d => d.Scope == "Header"))
        {
            var supplied = req.Values.TryGetValue(def.Code, out var raw);
            var value = supplied ? raw?.Trim() : null;
            var row = existing.FirstOrDefault(v => v.FieldDefId == def.Id);

            // CF4-T12: Disabled/Inline fields are display-only — the server rejects any CHANGE
            // (an unchanged echo is tolerated so whole-form submitters don't break). Required
            // is not enforced on them either: the user cannot supply what they cannot edit.
            if (def.DisplayType != "Normal")
            {
                if (supplied && !string.Equals(value ?? "", Render(row) ?? "", StringComparison.Ordinal))
                    throw new CustomFieldValidationException(
                        $"'{def.Label}' is {def.DisplayType.ToLowerInvariant()} — not user-editable.");
                continue;
            }

            if (supplied && string.IsNullOrEmpty(value))
            {
                // Clearing: the honest null is the ABSENCE of the row.
                if (def.Required)
                    throw new CustomFieldValidationException($"'{def.Label}' is required.");
                if (row is not null) db.CustomFieldValues.Remove(row);
                continue;
            }
            if (!supplied)
            {
                if (def.Required && row is null)
                    throw new CustomFieldValidationException($"'{def.Label}' is required.");
                continue;
            }

            row ??= db.CustomFieldValues.Add(new CustomFieldValue
            {
                FieldDefId = def.Id, RecordType = type, RecordId = recordId, DataType = def.DataType,
            }).Entity;
            await WriteTypedAsync(row, def, value!, ct);
            row.UpdatedUtc = clock.UtcNow;
        }
        await db.SaveChangesAsync(ct);
        return await MergedAsync(type, recordId, ct);
    }

    // CF-FIX1-T8 wire formats (server-side, AUTHORITATIVE — client hints are convenience only):
    //   Date       ISO yyyy-MM-dd (the picker) OR exact dd/MM/yyyy text — nothing else.
    //   DateTime   ISO yyyy-MM-ddTHH:mm OR exact "dd/MM/yyyy HH:mm".
    //   Hyperlink  "url" or "url\nlabel" — absolute http(s) url; label → ValueLabel (companion).
    //   Image/Doc  "<fileId>::<name>" (AttachmentField's format) — file must EXIST in the
    //              store; Image additionally requires an image/* content type. Rides the
    //              existing FileStore + FileAccessPolicy — no new upload pipeline.
    private async Task WriteTypedAsync(CustomFieldValue row, CustomFieldDef def, string value, CancellationToken ct)
    {
        row.ValueText = null; row.ValueNumber = null; row.ValueMoney = null;
        row.ValueDate = null; row.ValueBool = null; row.ValueListCode = null;
        row.ValueDateTime = null; row.ValueLabel = null;
        switch (def.DataType)
        {
            case CustomFieldDataType.Text:
            case CustomFieldDataType.LongText:
                row.ValueText = value;
                break;
            case CustomFieldDataType.Int:
                if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var i) || i % 1 != 0)
                    throw new CustomFieldValidationException($"'{def.Label}' must be a whole number.");
                row.ValueNumber = i;
                break;
            case CustomFieldDataType.Decimal:
                if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var d))
                    throw new CustomFieldValidationException($"'{def.Label}' must be a number.");
                row.ValueNumber = d;
                break;
            case CustomFieldDataType.Money:
                if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var m))
                    throw new CustomFieldValidationException($"'{def.Label}' must be an amount.");
                row.ValueMoney = Math.Round(m, 2, MidpointRounding.AwayFromZero);
                break;
            case CustomFieldDataType.Date:
                if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", out var dt)
                    && !DateOnly.TryParseExact(value, "dd/MM/yyyy", out dt))
                    throw new CustomFieldValidationException($"'{def.Label}' must be a date in dd/mm/yyyy format.");
                row.ValueDate = dt;
                break;
            case CustomFieldDataType.DateTime:
                if (!System.DateTime.TryParseExact(value, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dtt)
                    && !System.DateTime.TryParseExact(value, "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out dtt))
                    throw new CustomFieldValidationException($"'{def.Label}' must be a date and time in dd/mm/yyyy hh:mm format.");
                row.ValueDateTime = System.DateTime.SpecifyKind(dtt, DateTimeKind.Utc);
                break;
            case CustomFieldDataType.Percent:
                if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var pct) || pct < 0 || pct > 100)
                    throw new CustomFieldValidationException($"'{def.Label}' must be a percentage between 0 and 100.");
                row.ValueNumber = pct;
                break;
            case CustomFieldDataType.Email:
                if (!System.Text.RegularExpressions.Regex.IsMatch(value, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                    throw new CustomFieldValidationException($"'{def.Label}' must be a valid email address.");
                row.ValueText = value;
                break;
            case CustomFieldDataType.Telephone:
                if (!System.Text.RegularExpressions.Regex.IsMatch(value, @"^[+()0-9\-\s]{5,25}$")
                    || value.Count(char.IsDigit) < 5)
                    throw new CustomFieldValidationException($"'{def.Label}' must be a valid phone number (digits, spaces, +, -, parentheses).");
                row.ValueText = value;
                break;
            case CustomFieldDataType.Hyperlink:
            {
                var parts = value.Split('\n', 2);
                var url = parts[0].Trim();
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
                    throw new CustomFieldValidationException($"'{def.Label}' must be a valid absolute http(s) URL.");
                row.ValueText = url;
                row.ValueLabel = parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]) ? parts[1].Trim() : null;
                break;
            }
            case CustomFieldDataType.Image:
            case CustomFieldDataType.Document:
            {
                var refParts = value.Split("::", 2);
                if (!Guid.TryParse(refParts[0], out var fileId))
                    throw new CustomFieldValidationException($"'{def.Label}' must reference an uploaded file.");
                var stored = await db.StoredFiles.AsNoTracking().FirstOrDefaultAsync(s => s.Id == fileId, ct)
                    ?? throw new CustomFieldValidationException($"'{def.Label}' references a file that does not exist.");
                if (def.DataType == CustomFieldDataType.Image
                    && !(stored.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ?? false))
                    throw new CustomFieldValidationException($"'{def.Label}' only accepts image files (got {stored.ContentType ?? "unknown"}).");
                row.ValueText = value;
                break;
            }
            case CustomFieldDataType.Bool:
                row.ValueBool = value.ToLowerInvariant() switch
                {
                    "true" or "yes" => true,
                    "false" or "no" => false,
                    _ => throw new CustomFieldValidationException($"'{def.Label}' must be Yes or No."),
                };
                break;
            case CustomFieldDataType.ListValue:
                var ok = await db.CustomListValues.AnyAsync(v => v.CustomListId == def.CustomListId && v.Code == value, ct);
                if (!ok) throw new CustomFieldValidationException($"'{value}' is not a value of '{def.Label}'s list.");
                row.ValueListCode = value;
                break;
            default:
                throw new CustomFieldValidationException($"Unsupported data type {def.DataType}.");
        }
    }

    private async Task<IReadOnlyList<CustomValueDto>> MergedAsync(RecordType type, Guid recordId, CancellationToken ct)
    {
        var defs = await DefsFor(type).AsNoTracking()
            .Where(d => d.Active && d.Scope == "Header")
            .OrderBy(d => d.Sort).ThenBy(d => d.Label).ToListAsync(ct);
        var values = await db.CustomFieldValues.AsNoTracking()
            .Where(v => v.RecordType == type && v.RecordId == recordId && v.LineId == null).ToListAsync(ct);
        var listIds = defs.Where(d => d.CustomListId is not null).Select(d => d.CustomListId!.Value).ToList();
        var listCodes = await db.CustomLists.AsNoTracking().Where(l => listIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => l.Code, ct);
        return defs.Select(d =>
        {
            var v = values.FirstOrDefault(x => x.FieldDefId == d.Id);
            return new CustomValueDto(d.Code, d.Label, d.DataType.ToString(), d.Required, d.HelpText,
                d.CustomListId is { } lid ? listCodes.GetValueOrDefault(lid) : null, Render(v), d.DisplayType);
        }).ToList();
    }

    /// <summary>Typed → the STRING formats the FieldSpec pipeline stores (ISO date, raw numeric,
    /// 'true'/'false', list code). Null = no row = honest null.</summary>
    internal static string? Render(CustomFieldValue? v) => v switch
    {
        null => null,
        { ValueText: { } t, ValueLabel: { } lbl } => t + "\n" + lbl,   // Hyperlink round-trips url\nlabel
        { ValueText: { } t } => t,
        { ValueDateTime: { } dtv } => dtv.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture),
        { ValueNumber: { } n } => n % 1 == 0 ? ((long)n).ToString(CultureInfo.InvariantCulture) : n.ToString(CultureInfo.InvariantCulture),
        { ValueMoney: { } m } => m.ToString(CultureInfo.InvariantCulture),
        { ValueDate: { } d } => d.ToString("yyyy-MM-dd"),
        { ValueBool: { } b } => b ? "true" : "false",
        { ValueListCode: { } c } => c,
        _ => null,
    };


    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<CustomValueDto>>> GetLineValuesAsync(string recordType, Guid recordId, CancellationToken ct = default)
    {
        var type = Parse(recordType);
        await reachability.RequireReachableAsync(type, recordId, ct);
        var defs = await DefsFor(type).AsNoTracking()
            .Where(d => d.Active && d.Scope == "Line")
            .OrderBy(d => d.Sort).ThenBy(d => d.Label).ToListAsync(ct);
        if (defs.Count == 0) return new Dictionary<Guid, IReadOnlyList<CustomValueDto>>();
        var values = await db.CustomFieldValues.AsNoTracking()
            .Where(v => v.RecordType == type && v.RecordId == recordId && v.LineId != null).ToListAsync(ct);
        var listIds = defs.Where(d => d.CustomListId is not null).Select(d => d.CustomListId!.Value).ToList();
        var listCodes = await db.CustomLists.AsNoTracking().Where(l => listIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => l.Code, ct);
        return values.GroupBy(v => v.LineId!.Value).ToDictionary(
            g => g.Key,
            g => (IReadOnlyList<CustomValueDto>)defs.Select(d =>
            {
                var v = g.FirstOrDefault(x => x.FieldDefId == d.Id);
                return new CustomValueDto(d.Code, d.Label, d.DataType.ToString(), d.Required, d.HelpText,
                    d.CustomListId is { } lid ? listCodes.GetValueOrDefault(lid) : null, Render(v), d.DisplayType);
            }).ToList());
    }

    public async Task<IReadOnlyList<CustomValueDto>> GetLineDefsAsync(string recordType, CancellationToken ct = default)
    {
        var type = Parse(recordType);
        var defs = await DefsFor(type).AsNoTracking()
            .Where(d => d.Active && d.Scope == "Line")
            .OrderBy(d => d.Sort).ThenBy(d => d.Label).ToListAsync(ct);
        var listIds = defs.Where(d => d.CustomListId is not null).Select(d => d.CustomListId!.Value).ToList();
        var listCodes = await db.CustomLists.AsNoTracking().Where(l => listIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => l.Code, ct);
        return defs.Select(d => new CustomValueDto(d.Code, d.Label, d.DataType.ToString(), d.Required, d.HelpText,
            d.CustomListId is { } lid ? listCodes.GetValueOrDefault(lid) : null, null, d.DisplayType)).ToList();
    }

    private static readonly string[] Scopes = ["Header", "Line"];

    private static string ParseScope(string raw) =>
        Scopes.FirstOrDefault(x => string.Equals(x, raw, StringComparison.OrdinalIgnoreCase))
            ?? throw new CustomFieldValidationException("Scope must be Header or Line.");

    private async Task<CustomFieldDef> Load(Guid id, CancellationToken ct) =>
        await db.CustomFieldDefs.FirstOrDefaultAsync(d => d.Id == id, ct)
            ?? throw new NotFoundException($"Custom field {id} not found.");

    private static CustomFieldDefDto ToDto(CustomFieldDef d, int valueCount, IReadOnlyList<string>? recordTypes = null) => new(
        d.Id, d.Code, d.Label, recordTypes is { Count: > 0 } ? recordTypes[0] : "", d.DataType.ToString(), d.CustomListId,
        d.Required, d.HelpText, d.Active, d.Sort, valueCount, d.DisplayType, d.ShowInList, d.Scope, recordTypes ?? []);

    private static readonly string[] DisplayTypes = ["Normal", "Disabled", "Inline"];

    private static string ParseDisplayType(string raw) =>
        DisplayTypes.FirstOrDefault(x => string.Equals(x, raw, StringComparison.OrdinalIgnoreCase))
            ?? throw new CustomFieldValidationException($"Display type must be one of: {string.Join(", ", DisplayTypes)}.");


    private static RecordType Parse(string raw) =>
        Enum.TryParse<RecordType>(raw, ignoreCase: true, out var t)
            ? t : throw new CustomFieldValidationException($"Unknown record type '{raw}'.");

    private static CustomFieldDataType ParseDataType(string raw) =>
        Enum.TryParse<CustomFieldDataType>(raw, ignoreCase: true, out var t)
            ? t : throw new CustomFieldValidationException($"Unknown data type '{raw}'.");

    internal static FieldDataType RegistryTypeOf(CustomFieldDataType t) => t switch
    {
        CustomFieldDataType.Text or CustomFieldDataType.LongText
            or CustomFieldDataType.Email or CustomFieldDataType.Telephone
            or CustomFieldDataType.Hyperlink or CustomFieldDataType.Image
            or CustomFieldDataType.Document => FieldDataType.Text,
        CustomFieldDataType.DateTime => FieldDataType.Instant,
        CustomFieldDataType.Int or CustomFieldDataType.Decimal or CustomFieldDataType.Percent => FieldDataType.Number,
        CustomFieldDataType.Money => FieldDataType.Money,
        CustomFieldDataType.Date => FieldDataType.Date,
        CustomFieldDataType.Bool => FieldDataType.Bool,
        CustomFieldDataType.ListValue => FieldDataType.Enum,
        _ => FieldDataType.Text,
    };
}
