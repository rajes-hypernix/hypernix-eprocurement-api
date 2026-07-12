using eProcure.Application;
using eProcure.Application.Abstractions;
using eProcure.Application.Authorization;
using eProcure.Application.Forms;
using eProcure.Application.Views;
using eProcure.Domain;
using eProcure.Domain.Forms;
using eProcure.Domain.Views;
using eProcure.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eProcure.Infrastructure.Services;

/// <summary>
/// D7 — the entry-form engine. Definitions are Admin Setup (A69); the resolve read is
/// all-principal (A71) with the record type's View* checked dynamically (4th use of the
/// convention). Field keys are registry-validated on save AND at resolve (the D3/D5
/// loud-fail rule — deliberately NO FK, which would silently convert D5's ruled
/// zero-value hard-delete into blocked deletes). RecordType is restricted to types with
/// a consuming surface (OD-D7-5).
/// </summary>
public sealed class EntryFormService(AppDbContext db, IClock clock, ICurrentUser user) : IEntryFormService, IEntryFormSubmitGuard
{
    /// <summary>
    /// FIXED GLOBAL ROLE PRECEDENCE (ruled, OD MODIFIED): resolution must never depend on
    /// a user record's role-array order — an admin reordering roles in the user editor
    /// would silently change which form a user gets. One documented constant, first role
    /// the caller HOLDS that has an Active mapped form wins, Standard fallback.
    /// Buyer outranks Approver (the transactional role owns entry surfaces); evaluators,
    /// Admin and Vendor follow — they hold no entry surface today, so their relative
    /// order is future-proofing, not live behaviour.
    /// </summary>
    public static readonly IReadOnlyList<string> RolePrecedence =
        ["Buyer", "Approver", "TechEvaluator", "CommEvaluator", "Admin", "Vendor"];

    // ---------- definitions (A69) ----------

    public async Task<IReadOnlyList<EntryFormDefDto>> ListAsync(string? recordType, CancellationToken ct = default)
    {
        var q = db.EntryFormDefs.AsNoTracking().AsQueryable();
        if (recordType is not null)
        {
            var type = Parse(recordType);
            q = q.Where(d => d.RecordType == type);
        }
        var defs = await q.OrderByDescending(d => d.IsSystem).ThenBy(d => d.Name).ToListAsync(ct);
        var result = new List<EntryFormDefDto>(defs.Count);
        foreach (var def in defs) result.Add(await ToDtoAsync(def, ct));
        return result;
    }

    public async Task<EntryFormDefDto> CreateAsync(SaveEntryFormRequest req, CancellationToken ct = default)
    {
        var type = Parse(req.RecordType);
        if (!EntryFormVocabulary.ConsumableRecordTypes.Contains(type))
            throw new FormValidationException($"No entry surface consumes {type} forms yet (OD-D7-5) — the restriction lifts at that surface's migration gate.");
        if (string.IsNullOrWhiteSpace(req.Name))
            throw new FormValidationException("A form needs a name.");
        var code = "ef_" + SourcingMapping.DimCode(req.Name).ToLowerInvariant().Replace('-', '_');
        // SWEEP-FIX-T1: the code is an INTERNAL id — copying "X (copy)" twice is a legitimate
        // action, so a collision de-dupes with a numeric suffix instead of failing forever.
        if (await db.EntryFormDefs.AnyAsync(d => d.Code == code, ct))
        {
            var n = 2;
            while (await db.EntryFormDefs.AnyAsync(d => d.Code == $"{code}_{n}", ct)) n++;
            code = $"{code}_{n}";
        }
        await ValidateFieldsAsync(type, req.Fields, ct);

        var now = clock.UtcNow;
        var def = new EntryFormDef
        {
            Id = Guid.NewGuid(), Code = code, Name = req.Name.Trim(), RecordType = type,
            IsSystem = false, Active = true, CreatedUtc = now, UpdatedUtc = now,
        };
        db.EntryFormDefs.Add(def);
        db.EntryFormFields.AddRange(ToEntities(def.Id, req.Fields));
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(def, ct);
    }

    public async Task<EntryFormDefDto> UpdateAsync(Guid id, SaveEntryFormRequest req, CancellationToken ct = default)
    {
        var def = await LoadUserForm(id, ct);
        await ValidateFieldsAsync(def.RecordType, req.Fields, ct);
        if (!string.IsNullOrWhiteSpace(req.Name)) def.Name = req.Name.Trim();
        def.UpdatedUtc = clock.UtcNow;

        // Replace the layout wholesale (the composer saves the full field list). The D4
        // lesson: explicit AddRange for replacement children with preset PKs — a tracked
        // parent's nav discovery would misclassify them as Modified.
        db.EntryFormFields.RemoveRange(await db.EntryFormFields.Where(f => f.FormDefId == def.Id).ToListAsync(ct));
        db.EntryFormFields.AddRange(ToEntities(def.Id, req.Fields));
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(def, ct);
    }

    public async Task<EntryFormDefDto> SetActiveAsync(Guid id, bool active, CancellationToken ct = default)
    {
        var def = await LoadUserForm(id, ct);   // Standard forms refuse (the parity baseline never hides)
        def.Active = active;
        def.UpdatedUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(def, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var def = await LoadUserForm(id, ct);
        db.EntryFormRoleMaps.RemoveRange(await db.EntryFormRoleMaps.Where(m => m.FormDefId == def.Id).ToListAsync(ct));
        db.EntryFormFields.RemoveRange(await db.EntryFormFields.Where(f => f.FormDefId == def.Id).ToListAsync(ct));
        db.EntryFormDefs.Remove(def);
        await db.SaveChangesAsync(ct);
    }

    public async Task<EntryFormDefDto> AssignRolesAsync(Guid id, AssignRolesRequest req, CancellationToken ct = default)
    {
        var def = await db.EntryFormDefs.FirstOrDefaultAsync(d => d.Id == id, ct)
            ?? throw new NotFoundException($"Entry form {id} not found.");
        foreach (var role in req.Roles)
            if (!RolePrecedence.Contains(role))
                throw new FormValidationException($"Unknown role '{role}'.");

        // One preferred form per (record type, role): assigning a role here moves it off
        // whatever form held it (UQ(RecordType, Role) is the DB backstop).
        var mine = await db.EntryFormRoleMaps.Where(m => m.FormDefId == def.Id).ToListAsync(ct);
        db.EntryFormRoleMaps.RemoveRange(mine);
        var taken = await db.EntryFormRoleMaps
            .Where(m => m.RecordType == def.RecordType && req.Roles.Contains(m.Role)).ToListAsync(ct);
        db.EntryFormRoleMaps.RemoveRange(taken);
        db.EntryFormRoleMaps.AddRange(req.Roles.Select(r => new EntryFormRoleMap
        {
            Id = Guid.NewGuid(), RecordType = def.RecordType, Role = r, FormDefId = def.Id,
        }));
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(def, ct);
    }

    // ---------- resolve (A71 + dynamic View*) ----------

    public async Task<ResolvedFormDto> ResolveAsync(string recordType, CancellationToken ct = default)
    {
        var type = Parse(recordType);
        var viewAction = ViewVocabulary.ViewActionFor[type];
        if (!ActionCatalog.RolesFor(viewAction).Any(user.Roles.Contains))
            throw new ForbiddenException("Not permitted for your role.");

        var def = await ResolveDefAsync(type, ct);
        var fields = await db.EntryFormFields.AsNoTracking()
            .Where(f => f.FormDefId == def.Id).OrderBy(f => f.Sort).ToListAsync(ct);

        var registry = await db.FieldRegistry.AsNoTracking()
            .Where(r => r.RecordType == type).ToDictionaryAsync(r => r.FieldKey, ct);
        var resolved = new List<ResolvedFormFieldDto>(fields.Count);
        foreach (var f in fields)
        {
            // Registry liveness at RENDER — a form referencing a dead key fails loudly
            // (the D3/D5 rule), never a silently dropped field.
            if (!registry.TryGetValue(f.FieldKey, out var reg))
                throw new FormValidationException($"Form '{def.Name}' references '{f.FieldKey}', which is no longer a live field on {type} — fix the form in Setup.");

            string? listCode = null;
            List<SegmentOptionDto>? options = null;
            if (reg.Kind == FieldKind.Custom && reg.CustomFieldDefId is { } cfId)
            {
                var listId = (await db.CustomFieldDefs.AsNoTracking().SingleAsync(d => d.Id == cfId, ct)).CustomListId;
                if (listId is { } lid)
                    listCode = (await db.CustomLists.AsNoTracking().SingleAsync(l => l.Id == lid, ct)).Code;
            }
            else if (reg.Kind == FieldKind.Segment && reg.SegmentDefId is { } segId)
            {
                options = await db.SegmentValues.AsNoTracking()
                    .Where(v => v.SegmentDefId == segId && v.Active).OrderBy(v => v.Sort).ThenBy(v => v.Label)
                    .Select(v => new SegmentOptionDto(v.Code, v.Label)).ToListAsync(ct);
            }

            resolved.Add(new ResolvedFormFieldDto(
                f.FieldKey, f.Label ?? reg.Label, reg.DataType.ToString(), reg.Kind.ToString(),
                f.Subtab, f.FieldGroup, f.Sort, f.DisplayType.ToString(), f.RequiredOnForm,
                ResolveDefault(f, reg), f.SourceFieldKey, f.FullWidth, f.Placeholder, listCode, options));
        }
        return new ResolvedFormDto(def.Id, def.Code, def.Name, type.ToString(), resolved);
    }

    private async Task<EntryFormDef> ResolveDefAsync(RecordType type, CancellationToken ct)
    {
        var maps = await db.EntryFormRoleMaps.AsNoTracking().Where(m => m.RecordType == type).ToListAsync(ct);
        foreach (var role in RolePrecedence)
        {
            if (!user.Roles.Contains(role)) continue;
            var map = maps.FirstOrDefault(m => m.Role == role);
            if (map is null) continue;
            var mapped = await db.EntryFormDefs.AsNoTracking().FirstOrDefaultAsync(d => d.Id == map.FormDefId && d.Active, ct);
            if (mapped is not null) return mapped;
        }
        return await db.EntryFormDefs.AsNoTracking().FirstOrDefaultAsync(d => d.RecordType == type && d.IsSystem, ct)
            ?? throw new NotFoundException($"No standard entry form is seeded for {type}.");
    }

    /// <summary>Defaults arrive RESOLVED (dates: the shared DateTokens grammar → yyyy-MM-dd).
    /// Applied by the client to NEW records only — never clobbering an edit.</summary>
    private string? ResolveDefault(EntryFormField f, FieldRegistryEntry reg)
    {
        if (string.IsNullOrEmpty(f.DefaultValue)) return f.DefaultValue;
        if (reg.DataType is not (FieldDataType.Date or FieldDataType.Instant)) return f.DefaultValue;
        var day = DateTokens.TryResolveDay(f.DefaultValue, clock.UtcNow);
        if (day is { } d) return d.ToString("yyyy-MM-dd");
        if (f.DefaultValue.StartsWith('@'))
            throw new FormValidationException($"Unknown date token '{f.DefaultValue}' on '{f.FieldKey}'.");
        return f.DefaultValue;
    }

    // ---------- submit guard (OD-D7-2/3) ----------

    public async Task EnsureSubmittableAsync(RecordType type, Guid recordId, IReadOnlyDictionary<string, string?> nativeValues, CancellationToken ct = default)
    {
        // Server-side RE-RESOLUTION: the caller never names its form — a client-sent
        // formCode could dodge its role form's required fields (OD-D7-2, ruled).
        var def = await ResolveDefAsync(type, ct);
        var required = await db.EntryFormFields.AsNoTracking()
            .Where(f => f.FormDefId == def.Id && f.RequiredOnForm).ToListAsync(ct);
        if (required.Count == 0) return;

        var registry = await db.FieldRegistry.AsNoTracking()
            .Where(r => r.RecordType == type).ToDictionaryAsync(r => r.FieldKey, ct);
        var missing = new List<string>();
        foreach (var f in required)
        {
            if (!registry.TryGetValue(f.FieldKey, out var reg))
                throw new FormValidationException($"Form '{def.Name}' references '{f.FieldKey}', which is no longer a live field on {type} — fix the form in Setup.");
            var satisfied = reg.Kind switch
            {
                FieldKind.Native => nativeValues.TryGetValue(f.FieldKey, out var v) && !string.IsNullOrWhiteSpace(v),
                FieldKind.Custom => reg.CustomFieldDefId is { } cfId
                    && await db.CustomFieldValues.AsNoTracking().AnyAsync(v => v.FieldDefId == cfId && v.RecordId == recordId, ct),
                FieldKind.Segment => reg.SegmentDefId is { } segId
                    && await db.SegmentAssignments.AsNoTracking().AnyAsync(a =>
                        a.SegmentDefId == segId && a.RecordType == type && a.RecordId == recordId && a.LineId == null, ct),
                _ => true,
            };
            if (!satisfied) missing.Add(f.Label ?? reg.Label);
        }
        if (missing.Count > 0)
            throw new FormValidationException(
                $"Required on your form before submit: {string.Join(", ", missing)}. (Save as draft to complete them first.)");
    }

    // ---------- helpers ----------

    private async Task<EntryFormDef> LoadUserForm(Guid id, CancellationToken ct)
    {
        var def = await db.EntryFormDefs.FirstOrDefaultAsync(d => d.Id == id, ct)
            ?? throw new NotFoundException($"Entry form {id} not found.");
        if (def.IsSystem)
            throw new DomainRuleException("Standard forms are read-only — create a copy to change the layout.");
        return def;
    }

    private async Task ValidateFieldsAsync(RecordType type, List<EntryFormFieldDto> fields, CancellationToken ct)
    {
        if (fields.Count == 0) throw new FormValidationException("A form needs at least one field.");
        if (fields.Select(f => f.FieldKey).Distinct(StringComparer.OrdinalIgnoreCase).Count() != fields.Count)
            throw new FormValidationException("A field may appear once per form.");

        var registry = await db.FieldRegistry.AsNoTracking()
            .Where(r => r.RecordType == type).ToDictionaryAsync(r => r.FieldKey, ct);
        var controllable = EntryFormVocabulary.ControllableNativeKeys[type];
        var keys = fields.Select(f => f.FieldKey).ToHashSet();
        foreach (var f in fields)
        {
            if (!registry.TryGetValue(f.FieldKey, out var reg))
                throw new FormValidationException($"'{f.FieldKey}' is not a live field on {type}.");
            // Derived-not-invented: a native key is placeable iff the write DTO carries it —
            // placing Code/HeaderStatus/Value (or CostCentre/Project, which have no write
            // path — the convergence row owns them) would be a lie.
            if (reg.Kind == FieldKind.Native && !controllable.Contains(f.FieldKey))
                throw new FormValidationException($"'{f.FieldKey}' is a system field on {type} — it has no entry-form write path.");
            if (!Enum.TryParse<EntryFormDisplayType>(f.DisplayType, out _))
                throw new FormValidationException($"Unknown display type '{f.DisplayType}'.");
            if (f.SourceFieldKey is { } src && !keys.Contains(src))
                throw new FormValidationException($"'{f.FieldKey}' sources from '{src}', which is not on this form.");
            if (f.RequiredOnForm && Enum.Parse<EntryFormDisplayType>(f.DisplayType) == EntryFormDisplayType.Hidden)
                throw new FormValidationException($"'{f.FieldKey}' cannot be both hidden and required.");
        }
    }

    private static List<EntryFormField> ToEntities(Guid formId, List<EntryFormFieldDto> fields) =>
        fields.Select(f => new EntryFormField
        {
            Id = Guid.NewGuid(), FormDefId = formId, FieldKey = f.FieldKey, Subtab = Norm(f.Subtab),
            FieldGroup = string.IsNullOrWhiteSpace(f.FieldGroup) ? "Header" : f.FieldGroup.Trim(),
            Sort = f.Sort, DisplayType = Enum.Parse<EntryFormDisplayType>(f.DisplayType),
            RequiredOnForm = f.RequiredOnForm, DefaultValue = Norm(f.DefaultValue),
            SourceFieldKey = Norm(f.SourceFieldKey), FullWidth = f.FullWidth,
            Label = Norm(f.Label), Placeholder = Norm(f.Placeholder),
        }).ToList();

    private static string? Norm(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private async Task<EntryFormDefDto> ToDtoAsync(EntryFormDef def, CancellationToken ct)
    {
        var fields = await db.EntryFormFields.AsNoTracking()
            .Where(f => f.FormDefId == def.Id).OrderBy(f => f.Sort).ToListAsync(ct);
        var roles = await db.EntryFormRoleMaps.AsNoTracking()
            .Where(m => m.FormDefId == def.Id).Select(m => m.Role).OrderBy(r => r).ToListAsync(ct);
        return new EntryFormDefDto(def.Id, def.Code, def.Name, def.RecordType.ToString(), def.IsSystem, def.Active,
            fields.Select(f => new EntryFormFieldDto(f.FieldKey, f.Subtab, f.FieldGroup, f.Sort,
                f.DisplayType.ToString(), f.RequiredOnForm, f.DefaultValue, f.SourceFieldKey,
                f.FullWidth, f.Label, f.Placeholder)).ToList(),
            roles);
    }

    private static RecordType Parse(string raw) =>
        Enum.TryParse<RecordType>(raw, ignoreCase: true, out var t)
            ? t : throw new FormValidationException($"Unknown record type '{raw}'.");
}
