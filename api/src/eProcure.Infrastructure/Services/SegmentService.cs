using eProcure.Application;
using eProcure.Application.Abstractions;
using eProcure.Application.Authorization;
using eProcure.Application.Onboarding;
using eProcure.Application.Procurement;
using eProcure.Application.Segments;
using eProcure.Application.Sourcing;
using eProcure.Application.Suppliers;
using eProcure.Application.Views;
using eProcure.Domain;
using eProcure.Domain.Segments;
using eProcure.Domain.Views;
using eProcure.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eProcure.Infrastructure.Services;

/// <summary>
/// The dimension engine (D6). User segments keep the D5 registry-lockstep pattern per
/// APPLICATION (a seg_* Kind=Segment row per applied record type, options = the values);
/// the four SYSTEM segments have NO registry rows this slice (ruled — Native columns
/// filter; group-by addresses by definition) and are read-only here: their vocabulary
/// grows only through the projection, and changes belong to the convergence row.
/// Assignments ride the folded A66/A67 gate (same species as custom values, ruled) with
/// the same three layers: static action + dynamic View* + the record's scoped fetch.
/// </summary>
public sealed class SegmentService(
    AppDbContext db,
    IClock clock,
    IRecordReachability reachability,
    IEnumerable<eProcure.Application.CustomFields.ISegmentReferenceProvider>? refProviders = null,
    IEnumerable<eProcure.Application.CustomFields.ISegmentDataProvider>? dataProviders = null,
    eProcure.Application.Abstractions.IAuditLog? audit = null) : ISegmentService
{
    // ---------- defs / values / applications (A68) ----------

    public async Task<IReadOnlyList<SegmentDefDto>> ListDefsAsync(CancellationToken ct = default)
    {
        var defs = await db.SegmentDefs.AsNoTracking().OrderBy(d => d.IsSystem).ThenBy(d => d.Name).ToListAsync(ct);
        var result = new List<SegmentDefDto>();
        foreach (var d in defs) result.Add(await ToDtoAsync(d, ct));
        return result;
    }

    public async Task<SegmentDefDto> CreateDefAsync(SaveSegmentDefRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            throw new SegmentValidationException("A segment needs a name.");
        // CF-FIX5-T8: NetSuite's custseg_ convention — the SAME custseg_ id serves a segment
        // whether it is applied at header or line (it is ONE dimension, T7). Existing seg_*
        // codes are grandfathered (immutable — no rename, no migration).
        string code;
        if (!string.IsNullOrWhiteSpace(req.Code))
        {
            var part = req.Code.Trim().ToLowerInvariant();
            foreach (var known in new[] { "custseg_", "seg_" })
                if (part.StartsWith(known)) { part = part[known.Length..]; break; }
            if (part.Length == 0)
                throw new SegmentValidationException("The Internal ID needs a value after the custseg_ prefix.");
            if (!System.Text.RegularExpressions.Regex.IsMatch(part, "^[a-z0-9_]+$"))
                throw new SegmentValidationException("The Internal ID may only use letters, digits and underscores.");
            code = "custseg_" + part;
        }
        else
        {
            code = "custseg_" + new string(req.Name.Trim().ToLowerInvariant()
                .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray()).Trim('_');
        }
        if (code.Length > 60) code = code[..60];
        if (await db.SegmentDefs.AnyAsync(d => d.Code == code, ct))
            throw new SegmentValidationException($"A segment with code '{code}' already exists.");
        var def = new SegmentDef
        {
            Id = Guid.NewGuid(), Code = code, Name = req.Name.Trim(),
            HasHierarchy = req.HasHierarchy, Required = req.Required,
            CreatedUtc = clock.UtcNow, UpdatedUtc = clock.UtcNow,
        };
        db.SegmentDefs.Add(def);
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(def, ct);
    }

    public async Task<SegmentDefDto> UpdateDefAsync(Guid id, SaveSegmentDefRequest req, CancellationToken ct = default)
    {
        var def = await LoadUserDef(id, ct);
        if (string.IsNullOrWhiteSpace(req.Name))
            throw new SegmentValidationException("A segment needs a name.");
        def.Name = req.Name.Trim();
        def.HasHierarchy = req.HasHierarchy;
        def.Required = req.Required;
        def.UpdatedUtc = clock.UtcNow;
        foreach (var reg in await db.FieldRegistry.Where(r => r.FieldKey == def.Code && r.Kind == FieldKind.Segment).ToListAsync(ct))
            reg.Label = def.Name;
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(def, ct);
    }

    public async Task<SegmentDefDto> AddValueAsync(Guid defId, SaveSegmentValueRequest req, CancellationToken ct = default)
    {
        var def = await LoadUserDef(defId, ct);
        if (string.IsNullOrWhiteSpace(req.Label))
            throw new SegmentValidationException("A segment value needs a label.");
        var code = SourcingMapping.DimCode(req.Label);
        if (await db.SegmentValues.AnyAsync(v => v.SegmentDefId == def.Id && v.Code == code, ct))
            throw new SegmentValidationException($"Value '{code}' already exists on {def.Name}.");
        if (req.ParentValueId is { } pid && !await db.SegmentValues.AnyAsync(v => v.Id == pid && v.SegmentDefId == def.Id, ct))
            throw new SegmentValidationException("The parent value does not belong to this segment.");
        db.SegmentValues.Add(new SegmentValue
        {
            Id = SegmentSeed.ValueId(def.Id, code), SegmentDefId = def.Id,
            Code = code, Label = req.Label.Trim(), ParentValueId = req.ParentValueId, Sort = req.Sort,
        });
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(def, ct);
    }

    public async Task<SegmentDefDto> UpdateValueAsync(Guid defId, Guid valueId, UpdateSegmentValueRequest req, CancellationToken ct = default)
    {
        var def = await LoadUserDef(defId, ct);
        var value = await db.SegmentValues.FirstOrDefaultAsync(v => v.Id == valueId && v.SegmentDefId == def.Id, ct)
            ?? throw new NotFoundException($"Segment value {valueId} not found on {def.Name}.");
        if (string.IsNullOrWhiteSpace(req.Label))
            throw new SegmentValidationException("A segment value needs a label.");
        if (req.ParentValueId is { } pid && (pid == valueId ||
            !await db.SegmentValues.AnyAsync(v => v.Id == pid && v.SegmentDefId == def.Id, ct)))
            throw new SegmentValidationException("The parent value does not belong to this segment.");
        // CF2-T6: the CODE is the stored dimension key — label edits never re-key history.
        value.Label = req.Label.Trim();
        value.ParentValueId = req.ParentValueId;
        value.Sort = req.Sort;
        value.Active = req.Active;
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(def, ct);
    }

    public async Task<SegmentDefDto> DeleteValueAsync(Guid defId, Guid valueId, CancellationToken ct = default)
    {
        var def = await LoadUserDef(defId, ct);
        var value = await db.SegmentValues.FirstOrDefaultAsync(v => v.Id == valueId && v.SegmentDefId == def.Id, ct)
            ?? throw new NotFoundException($"Segment value {valueId} not found on {def.Name}.");
        if (await db.SegmentValues.AnyAsync(v => v.ParentValueId == valueId, ct))
            throw new DomainRuleException($"'{value.Label}' has child values — repoint or remove them first.");
        // CF-FIX4-T6: the CF-FIX-3 tiers replace the silent deactivate-fallback. Delete
        // REFUSES loudly with the report's reason; Tier 1 stays UpdateValueAsync(active:false);
        // Tier 3 is PurgeValueAsync (A73).
        var report = await BuildReportAsync(def, value.Id, ct);
        if (!report.CanDelete)
            throw new DomainRuleException(report.BlockedReason
                ?? $"'{value.Label}' is still referenced or assigned — deactivate it instead, or purge its history (governed).");
        db.SegmentValues.Remove(value);
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(def, ct);
    }

    public async Task<eProcure.Application.CustomFields.ImpactReportDto> GetReferencesAsync(Guid id, CancellationToken ct = default)
    {
        var def = await db.SegmentDefs.FirstOrDefaultAsync(d => d.Id == id, ct)
            ?? throw new NotFoundException($"Segment {id} not found.");
        return await BuildReportAsync(def, null, ct);
    }

    public async Task<eProcure.Application.CustomFields.ImpactReportDto> GetValueReferencesAsync(Guid valueId, CancellationToken ct = default)
    {
        var value = await db.SegmentValues.AsNoTracking().FirstOrDefaultAsync(v => v.Id == valueId, ct)
            ?? throw new NotFoundException($"Segment value {valueId} not found.");
        var def = await db.SegmentDefs.FirstAsync(d => d.Id == value.SegmentDefId, ct);
        return await BuildReportAsync(def, valueId, ct);
    }

    /// <summary>The CF-FIX-3 report shape, segment grain: LOOP the registered providers,
    /// never name a consumer (the anti-rot contract).</summary>
    private async Task<eProcure.Application.CustomFields.ImpactReportDto> BuildReportAsync(
        Domain.Segments.SegmentDef def, Guid? valueId, CancellationToken ct)
    {
        var refs = new List<eProcure.Application.CustomFields.FieldReference>();
        foreach (var p in refProviders ?? [])
            refs.AddRange(await p.FindReferencesAsync(def.Id, def.Code, valueId, ct));
        var data = new List<eProcure.Application.CustomFields.DataReferenceSummary>();
        foreach (var p in dataProviders ?? [])
            data.Add(await p.CountAssignmentsAsync(def.Id, valueId, ct));
        var live = data.Sum(d => d.LiveCount);
        var historical = data.Sum(d => d.HistoricalCount);
        var blocked =
            refs.Count > 0 ? $"Still referenced by {string.Join(", ", refs.Select(r => r.ConsumerName).Distinct())} — clear those first (the list below is the to-do)."
            : live > 0 ? $"{live} assignment(s) live on OPEN records — a dimension in live use is never deleted."
            : null;
        return new eProcure.Application.CustomFields.ImpactReportDto(refs, data, live, historical,
            CanDelete: refs.Count == 0 && live == 0 && historical == 0,
            CanPurge: refs.Count == 0 && live == 0 && historical > 0,
            blocked ?? (historical > 0 ? $"{historical} assignment(s) remain on closed/historical records — Purge (governed) removes them with a snapshot." : null));
    }

    /// <summary>CF-FIX4-T6 Tier 3, segment DEF grain — the CF-FIX-3 purge contract verbatim:
    /// transactional, RE-VERIFIED inside the transaction, every removed assignment
    /// snapshotted to the audit, then the def (values, applications, registry) deleted.</summary>
    public async Task PurgeAsync(Guid id, CancellationToken ct = default)
    {
        var def = await LoadUserDef(id, ct);   // system defs refuse — convergence-owned
        var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            var report = await BuildReportAsync(def, null, ct);
            if (!report.CanPurge)
                throw new DomainRuleException(report.BlockedReason
                    ?? "Purge is only available when references are clear, no live assignments remain, and historical assignments exist.");
            foreach (var p in dataProviders ?? [])
            {
                var snapshot = await p.PurgeHistoricalAsync(def.Id, null, ct);
                foreach (var v in snapshot.Removed)
                    if (audit is not null)
                        await audit.WriteAsync("Segment", def.Code, "Purged historical assignment",
                            before: $"{v.RecordType} {v.RecordLabel}{(v.LineId is not null ? $" line {v.LineId}" : "")}", after: v.Value, ct: ct);
            }
            db.FieldRegistry.RemoveRange(await db.FieldRegistry.Where(r => r.SegmentDefId == def.Id).ToListAsync(ct));
            db.SegmentApplications.RemoveRange(await db.SegmentApplications.Where(a => a.SegmentDefId == def.Id).ToListAsync(ct));
            db.SegmentValues.RemoveRange(await db.SegmentValues.Where(v => v.SegmentDefId == def.Id).ToListAsync(ct));
            db.SegmentDefs.Remove(def);
            if (audit is not null) await audit.WriteAsync("Segment", def.Code, "Purged and deleted", ct: ct);
            await db.SaveChangesAsync(ct);
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

    /// <summary>Tier 3, VALUE grain — same contract; the value row goes with its history.</summary>
    public async Task PurgeValueAsync(Guid valueId, CancellationToken ct = default)
    {
        var value = await db.SegmentValues.FirstOrDefaultAsync(v => v.Id == valueId, ct)
            ?? throw new NotFoundException($"Segment value {valueId} not found.");
        var def = await db.SegmentDefs.FirstAsync(d => d.Id == value.SegmentDefId, ct);
        var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            var report = await BuildReportAsync(def, valueId, ct);
            if (!report.CanPurge)
                throw new DomainRuleException(report.BlockedReason
                    ?? "Purge is only available when references are clear, no live assignments remain, and historical assignments exist.");
            foreach (var p in dataProviders ?? [])
            {
                var snapshot = await p.PurgeHistoricalAsync(def.Id, valueId, ct);
                foreach (var v in snapshot.Removed)
                    if (audit is not null)
                        await audit.WriteAsync("Segment", $"{def.Code}:{value.Code}", "Purged historical assignment",
                            before: $"{v.RecordType} {v.RecordLabel}{(v.LineId is not null ? $" line {v.LineId}" : "")}", after: v.Value, ct: ct);
            }
            db.SegmentValues.Remove(value);
            if (audit is not null) await audit.WriteAsync("Segment", $"{def.Code}:{value.Code}", "Purged and deleted", ct: ct);
            await db.SaveChangesAsync(ct);
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

    public async Task<SegmentDefDto> SetDefActiveAsync(Guid id, bool active, CancellationToken ct = default)
    {
        var def = await LoadUserDef(id, ct);
        def.Active = active;
        def.UpdatedUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(def, ct);
    }

    public async Task DeleteDefAsync(Guid id, CancellationToken ct = default)
    {
        var def = await LoadUserDef(id, ct);   // system defs refuse here (convergence row owns them)
        // CF-FIX4-T6: report-driven tiers — refs (forms, views) AND assignments block, with
        // the reason named; historical-only offers the governed purge instead.
        var report = await BuildReportAsync(def, null, ct);
        if (!report.CanDelete)
            throw new DomainRuleException(report.BlockedReason
                ?? $"{def.Name} is still referenced or assigned — dimension keys are never silently dropped.");
        db.FieldRegistry.RemoveRange(await db.FieldRegistry.Where(r => r.SegmentDefId == def.Id).ToListAsync(ct));
        db.SegmentApplications.RemoveRange(await db.SegmentApplications.Where(a => a.SegmentDefId == def.Id).ToListAsync(ct));
        db.SegmentValues.RemoveRange(await db.SegmentValues.Where(v => v.SegmentDefId == def.Id).ToListAsync(ct));
        db.SegmentDefs.Remove(def);
        await db.SaveChangesAsync(ct);
    }

    public async Task<SegmentDefDto> ApplyAsync(Guid defId, ApplySegmentRequest req, CancellationToken ct = default)
    {
        var def = await LoadUserDef(defId, ct);
        var type = Parse(req.RecordType);
        if (await db.SegmentApplications.AnyAsync(a => a.SegmentDefId == def.Id && a.RecordType == type && a.LineLevel == req.LineLevel, ct))
            throw new SegmentValidationException($"{def.Name} is already applied to {type} {(req.LineLevel ? "at line level" : "at header level")}.");
        db.SegmentApplications.Add(new SegmentApplication
        {
            Id = Guid.NewGuid(), SegmentDefId = def.Id, RecordType = type, LineLevel = req.LineLevel,
        });
        // Registry lockstep (D5 pattern): the applied segment becomes filterable on that type.
        // CF-FIX5-T7: the dimension is filterable whether applied at header OR line, so the
        // registry row is ONE per (segment, type) — written idempotently (a second-level
        // apply must not duplicate it).
        if (!await db.FieldRegistry.AnyAsync(r => r.SegmentDefId == def.Id && r.RecordType == type, ct))
            db.FieldRegistry.Add(new FieldRegistryEntry
            {
                Id = Guid.NewGuid(), RecordType = type, FieldKey = def.Code, Kind = FieldKind.Segment,
                Label = def.Name, DataType = FieldDataType.Enum, SegmentDefId = def.Id,
            });
        // CF-FIX4-T7: a HEADER apply may carry form placements — the SAME L1 EntryFormField
        // rows the T4 cascade writes and the designer drags. A LINE apply is FLAT (L4 — no
        // groups on lines) and must not carry any.
        if (req.Placements is { Count: > 0 })
        {
            if (req.LineLevel)
                throw new SegmentValidationException("A line-level application is flat — line dimensions have no form/group placement (L4).");
            if (req.Placements.Select(p => p.FormId).Distinct().Count() != req.Placements.Count)
                throw new SegmentValidationException("A segment is placed at most once per form.");
            foreach (var p in req.Placements)
            {
                var form = await db.EntryFormDefs.FirstOrDefaultAsync(x => x.Id == p.FormId, ct)
                    ?? throw new SegmentValidationException("A placement names a form that does not exist.");
                if (form.RecordType != type || !form.Active)
                    throw new SegmentValidationException($"'{form.Name}' is not an active {type} form.");
                var group = p.GroupId is { } gid
                    ? await db.EntryFormGroups.FirstOrDefaultAsync(g => g.Id == gid && g.FormDefId == form.Id, ct)
                        ?? throw new SegmentValidationException($"The chosen group does not belong to '{form.Name}'.")
                    : await db.EntryFormGroups.FirstAsync(g => g.FormDefId == form.Id && g.IsHeader, ct);   // L3: cannot miss
                if (await db.EntryFormFields.AnyAsync(x => x.FormDefId == form.Id && x.FieldKey == def.Code, ct))
                    continue;
                var maxSort = await db.EntryFormFields.Where(x => x.FormDefId == form.Id)
                    .Select(x => (int?)x.Sort).MaxAsync(ct) ?? -1;
                db.EntryFormFields.Add(new Domain.Forms.EntryFormField
                {
                    Id = Guid.NewGuid(), FormDefId = form.Id, FieldKey = def.Code, GroupId = group.Id,
                    Sort = maxSort + 1, DisplayType = Domain.Forms.EntryFormDisplayType.Normal,
                    RequiredOnForm = false,
                });
            }
        }
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(def, ct);
    }

    public async Task<SegmentDefDto> UnapplyAsync(Guid defId, string recordType, bool lineLevel = false, CancellationToken ct = default)
    {
        var def = await LoadUserDef(defId, ct);
        var type = Parse(recordType);
        // CF-FIX5-T7: unapply targets ONE level; the other stays.
        var app = await db.SegmentApplications.FirstOrDefaultAsync(a => a.SegmentDefId == def.Id && a.RecordType == type && a.LineLevel == lineLevel, ct)
            ?? throw new NotFoundException($"{def.Name} is not applied to {type} {(lineLevel ? "at line level" : "at header level")}.");
        // Assignments PIN the application at THAT grain (header = LineId null, line = LineId set).
        if (await db.SegmentAssignments.AnyAsync(a => a.SegmentDefId == def.Id && a.RecordType == type
                && (lineLevel ? a.LineId != null : a.LineId == null), ct))
            throw new DomainRuleException($"{def.Name} has {(lineLevel ? "line" : "header")} assignments on {type} — dimension keys are never silently dropped.");
        db.SegmentApplications.Remove(app);
        // CF-FIX5-T7: the shared registry row (filterable dimension) survives while the OTHER
        // level is still applied — drop it only when NO application of either level remains.
        if (!await db.SegmentApplications.AnyAsync(a => a.SegmentDefId == def.Id && a.RecordType == type && a.LineLevel != lineLevel, ct))
            db.FieldRegistry.RemoveRange(await db.FieldRegistry
                .Where(r => r.SegmentDefId == def.Id && r.RecordType == type).ToListAsync(ct));
        // Form placements belong to the HEADER application (line dimensions are flat — no form
        // fields), so they leave only when the header level is removed.
        if (!lineLevel)
            db.EntryFormFields.RemoveRange(await db.EntryFormFields
                .Where(x => x.FieldKey == def.Code
                    && db.EntryFormDefs.Any(d2 => d2.Id == x.FormDefId && d2.RecordType == type)).ToListAsync(ct));
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(def, ct);
    }

    // ---------- assignments (folded A66/A67) ----------

    public async Task<IReadOnlyList<SegmentAssignmentDto>> GetAssignmentsAsync(string recordType, Guid recordId, Guid? lineId, CancellationToken ct = default)
    {
        var type = Parse(recordType);
        await reachability.RequireReachableAsync(type, recordId, ct);   // A2F-T4: the ONE guard (was a 16-line twin)
        return await MergedAsync(type, recordId, lineId, ct);
    }

    public async Task<IReadOnlyList<SegmentAssignmentDto>> SaveAssignmentsAsync(string recordType, Guid recordId, SaveSegmentAssignmentsRequest req, CancellationToken ct = default)
    {
        var type = Parse(recordType);
        await reachability.RequireReachableAsync(type, recordId, ct);   // A2F-T4: the ONE guard (was a 16-line twin)

        var apps = await ApplicationsFor(type, ct);
        // CF-FIX5-T7: a segment can have TWO applications (header + line) — dedupe to the
        // distinct defs so the by-code lookup never collides.
        var defs = apps.Select(a => a.Def).DistinctBy(d => d.Id).ToDictionary(d => d.Code, StringComparer.OrdinalIgnoreCase);
        foreach (var key in req.Assignments.Keys)
        {
            if (!defs.TryGetValue(key, out var def))
                throw new SegmentValidationException($"Segment '{key}' is not applied to {type}.");
            if (def.IsSystem && type == RecordType.Requisition)
                throw new SegmentValidationException($"'{def.Name}' on requisitions is a projection of the PR's own field — edit the PR (convergence row owns the switch).");
            // A LINE write requires a LINE application (either level may exist independently).
            if (req.LineId is not null && !apps.Any(a => a.Def.Id == def.Id && a.App.LineLevel))
                throw new SegmentValidationException($"'{def.Name}' is not applied at line level on {type}.");
        }

        foreach (var (key, raw) in req.Assignments)
        {
            var def = defs[key];
            var existing = await db.SegmentAssignments.FirstOrDefaultAsync(a =>
                a.SegmentDefId == def.Id && a.RecordType == type && a.RecordId == recordId && a.LineId == req.LineId, ct);
            var valueCode = string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
            if (valueCode is null)
            {
                if (def.Required && req.LineId is null)
                    throw new SegmentValidationException($"'{def.Name}' is required.");
                if (existing is not null) db.SegmentAssignments.Remove(existing);
                continue;
            }
            var value = await db.SegmentValues.FirstOrDefaultAsync(v => v.SegmentDefId == def.Id && v.Code == valueCode && v.Active, ct)
                ?? throw new SegmentValidationException($"'{valueCode}' is not an active value of {def.Name}.");
            if (existing is null)
                db.SegmentAssignments.Add(new SegmentAssignment
                {
                    Id = Guid.NewGuid(), SegmentDefId = def.Id, SegmentValueId = value.Id,
                    RecordType = type, RecordId = recordId, LineId = req.LineId, UpdatedUtc = clock.UtcNow,
                });
            else
            {
                existing.SegmentValueId = value.Id;
                existing.UpdatedUtc = clock.UtcNow;
            }
        }
        await db.SaveChangesAsync(ct);
        return await MergedAsync(type, recordId, req.LineId, ct);
    }

    private async Task<IReadOnlyList<SegmentAssignmentDto>> MergedAsync(RecordType type, Guid recordId, Guid? lineId, CancellationToken ct)
    {
        var apps = await ApplicationsFor(type, ct);
        // CF-FIX5-T7: a HEADER read sees only header apps; a LINE read only line apps.
        var relevant = apps.Where(a => a.App.LineLevel == (lineId is not null)).ToList();
        var defIds = relevant.Select(a => a.Def.Id).ToList();
        var assignments = await db.SegmentAssignments.AsNoTracking()
            .Where(a => defIds.Contains(a.SegmentDefId) && a.RecordType == type && a.RecordId == recordId && a.LineId == lineId)
            .ToListAsync(ct);
        var values = await db.SegmentValues.AsNoTracking()
            .Where(v => defIds.Contains(v.SegmentDefId)).OrderBy(v => v.Sort).ThenBy(v => v.Label).ToListAsync(ct);

        return relevant.Select(a =>
        {
            var assigned = assignments.FirstOrDefault(x => x.SegmentDefId == a.Def.Id);
            var value = assigned is null ? null : values.FirstOrDefault(v => v.Id == assigned.SegmentValueId);
            return new SegmentAssignmentDto(
                a.Def.Code, a.Def.Name, a.Def.Required,
                values.Where(v => v.SegmentDefId == a.Def.Id && v.Active)
                    .Select(v => new SegmentValueDto(v.Id, v.Code, v.Label, v.ParentValueId, v.Active, v.Sort)).ToList(),
                value?.Code, value?.Label);
        }).ToList();
    }

    private async Task<List<(SegmentDef Def, SegmentApplication App)>> ApplicationsFor(RecordType type, CancellationToken ct)
    {
        var apps = await db.SegmentApplications.AsNoTracking().Where(a => a.RecordType == type).ToListAsync(ct);
        var defIds = apps.Select(a => a.SegmentDefId).ToList();
        var defs = await db.SegmentDefs.AsNoTracking().Where(d => defIds.Contains(d.Id) && d.Active).ToListAsync(ct);
        return apps.Where(a => defs.Any(d => d.Id == a.SegmentDefId))
            .Select(a => (defs.First(d => d.Id == a.SegmentDefId), a)).ToList();
    }


    private async Task<SegmentDef> LoadUserDef(Guid id, CancellationToken ct)
    {
        var def = await db.SegmentDefs.FirstOrDefaultAsync(d => d.Id == id, ct)
            ?? throw new NotFoundException($"Segment {id} not found.");
        if (def.IsSystem)
            throw new DomainRuleException("System segments are projections of PR fields — the convergence row owns changes to them.");
        return def;
    }

    private async Task<SegmentDefDto> ToDtoAsync(SegmentDef d, CancellationToken ct)
    {
        var values = await db.SegmentValues.AsNoTracking().Where(v => v.SegmentDefId == d.Id)
            .OrderBy(v => v.Sort).ThenBy(v => v.Label).ToListAsync(ct);
        var apps = await db.SegmentApplications.AsNoTracking().Where(a => a.SegmentDefId == d.Id).ToListAsync(ct);
        return new SegmentDefDto(d.Id, d.Code, d.Name, d.HasHierarchy, d.Required, d.Active, d.IsSystem,
            values.Select(v => new SegmentValueDto(v.Id, v.Code, v.Label, v.ParentValueId, v.Active, v.Sort)).ToList(),
            apps.Select(a => new SegmentApplicationDto(a.RecordType.ToString(), a.LineLevel)).ToList());
    }

    private static RecordType Parse(string raw) =>
        Enum.TryParse<RecordType>(raw, ignoreCase: true, out var t)
            ? t : throw new SegmentValidationException($"Unknown record type '{raw}'.");
}

/// <summary>
/// The (iii-a) one-way projection. Reads the PR's *Code/label COLUMNS (the single truth),
/// upserts the system segments' values (codes ARE the column codes — DimCode identity by
/// construction) and assignments (deterministic ids matching the migration's backfill).
/// Wired into BOTH RequisitionService write paths and the dev seeder; the Postgres probe
/// goes red if any future path forgets this call.
/// </summary>
public sealed class SegmentProjection(AppDbContext db, IClock clock) : ISegmentProjection
{
    public async Task ProjectRequisitionAsync(Guid prId, CancellationToken ct = default)
    {
        var pr = await db.PurchaseRequisitions.AsNoTracking().FirstOrDefaultAsync(p => p.Id == prId, ct);
        if (pr is null) return;
        foreach (var seg in SegmentSeed.SystemSegments)
        {
            var defId = SegmentSeed.DefId(seg.Code);
            var (code, label) = seg.Code switch
            {
                "seg_department" => (pr.DepartmentCode, pr.Department),
                "seg_location" => (pr.LocationCode, pr.Location),
                "seg_category" => (pr.CategoryCode, pr.Category),
                "seg_job" => (pr.JobCode, pr.Job),
                _ => ("", ""),
            };
            var existing = await db.SegmentAssignments.FirstOrDefaultAsync(a =>
                a.SegmentDefId == defId && a.RecordType == Domain.Views.RecordType.Requisition &&
                a.RecordId == pr.Id && a.LineId == null, ct);

            if (string.IsNullOrEmpty(code))
            {
                if (existing is not null) db.SegmentAssignments.Remove(existing);
                continue;
            }
            var valueId = SegmentSeed.ValueId(defId, code);
            if (!await db.SegmentValues.AnyAsync(v => v.Id == valueId, ct))
                db.SegmentValues.Add(new SegmentValue { Id = valueId, SegmentDefId = defId, Code = code, Label = label });
            if (existing is null)
                db.SegmentAssignments.Add(new SegmentAssignment
                {
                    Id = Guid.NewGuid(), SegmentDefId = defId, SegmentValueId = valueId,
                    RecordType = Domain.Views.RecordType.Requisition, RecordId = pr.Id, LineId = null, UpdatedUtc = clock.UtcNow,
                });
            else if (existing.SegmentValueId != valueId)
            {
                existing.SegmentValueId = valueId;
                existing.UpdatedUtc = clock.UtcNow;
            }
        }
        await db.SaveChangesAsync(ct);
    }
}
