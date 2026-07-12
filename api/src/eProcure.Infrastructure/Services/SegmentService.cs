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
    IRecordReachability reachability) : ISegmentService
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
        var code = "seg_" + new string(req.Name.Trim().ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray()).Trim('_');
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
        // The list-value discipline (A2F-T3): assigned somewhere → deactivate; clean → hard delete.
        if (await db.SegmentAssignments.AnyAsync(a => a.SegmentValueId == valueId, ct))
        {
            value.Active = false;
            await db.SaveChangesAsync(ct);
            return await ToDtoAsync(def, ct);
        }
        db.SegmentValues.Remove(value);
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(def, ct);
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
        if (await db.SegmentAssignments.AnyAsync(a => a.SegmentDefId == def.Id, ct))
            throw new DomainRuleException($"{def.Name} has live assignments — dimension keys are never silently dropped. Clear the assignments (or deactivate the segment) first.");
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
        if (await db.SegmentApplications.AnyAsync(a => a.SegmentDefId == def.Id && a.RecordType == type, ct))
            throw new SegmentValidationException($"{def.Name} is already applied to {type}.");
        db.SegmentApplications.Add(new SegmentApplication
        {
            Id = Guid.NewGuid(), SegmentDefId = def.Id, RecordType = type, LineLevel = req.LineLevel,
        });
        // Registry lockstep (D5 pattern): the applied segment becomes filterable on that type.
        db.FieldRegistry.Add(new FieldRegistryEntry
        {
            Id = Guid.NewGuid(), RecordType = type, FieldKey = def.Code, Kind = FieldKind.Segment,
            Label = def.Name, DataType = FieldDataType.Enum, SegmentDefId = def.Id,
        });
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(def, ct);
    }

    public async Task<SegmentDefDto> UnapplyAsync(Guid defId, string recordType, CancellationToken ct = default)
    {
        var def = await LoadUserDef(defId, ct);
        var type = Parse(recordType);
        var app = await db.SegmentApplications.FirstOrDefaultAsync(a => a.SegmentDefId == def.Id && a.RecordType == type, ct)
            ?? throw new NotFoundException($"{def.Name} is not applied to {type}.");
        if (await db.SegmentAssignments.AnyAsync(a => a.SegmentDefId == def.Id && a.RecordType == type, ct))
            throw new DomainRuleException($"{def.Name} has assignments on {type} — dimension keys are never silently dropped.");
        db.SegmentApplications.Remove(app);
        db.FieldRegistry.RemoveRange(await db.FieldRegistry
            .Where(r => r.SegmentDefId == def.Id && r.RecordType == type).ToListAsync(ct));
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
        var defs = apps.Select(a => a.Def).ToDictionary(d => d.Code, StringComparer.OrdinalIgnoreCase);
        foreach (var key in req.Assignments.Keys)
        {
            if (!defs.TryGetValue(key, out var def))
                throw new SegmentValidationException($"Segment '{key}' is not applied to {type}.");
            if (def.IsSystem && type == RecordType.Requisition)
                throw new SegmentValidationException($"'{def.Name}' on requisitions is a projection of the PR's own field — edit the PR (convergence row owns the switch).");
            if (req.LineId is not null && !apps.First(a => a.Def.Id == def.Id).App.LineLevel)
                throw new SegmentValidationException($"'{def.Name}' is header-level on {type}.");
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
        var relevant = lineId is null ? apps : apps.Where(a => a.App.LineLevel).ToList();
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
