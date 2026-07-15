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

    // CFH-T3: native dimension fields that render as segment-backed pickers (options from a
    // same-named segment's values). The PR write derives the *Code companion via DimCode.
    private static readonly string[] DimensionFieldKeys = ["Department", "Location", "Category", "Job"];

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
        // CF-FIX5-T8: NetSuite's customform_ convention — the user keys the meaningful part,
        // the system guarantees the namespace. Existing ef_* codes are grandfathered
        // (immutable, resolve unchanged — no rename, no migration).
        string code;
        if (!string.IsNullOrWhiteSpace(req.Code))
        {
            var part = req.Code.Trim().ToLowerInvariant();
            foreach (var known in new[] { "customform_", "ef_" })
                if (part.StartsWith(known)) { part = part[known.Length..]; break; }
            if (part.Length == 0)
                throw new FormValidationException("The Internal ID needs a value after the customform_ prefix.");
            if (!System.Text.RegularExpressions.Regex.IsMatch(part, "^[a-z0-9_]+$"))
                throw new FormValidationException("The Internal ID may only use letters, digits and underscores.");
            code = "customform_" + part;
            if (await db.EntryFormDefs.AnyAsync(d => d.Code == code, ct))
                throw new FormValidationException($"A form with Internal ID '{code}' already exists — choose another.");
        }
        else
        {
            // Auto-derived (e.g. "New form (copy of X)"): the code is an INTERNAL id, so a
            // collision de-dupes with a numeric suffix instead of failing forever.
            code = "customform_" + SourcingMapping.DimCode(req.Name).ToLowerInvariant().Replace('-', '_');
            if (await db.EntryFormDefs.AnyAsync(d => d.Code == code, ct))
            {
                var n = 2;
                while (await db.EntryFormDefs.AnyAsync(d => d.Code == $"{code}_{n}", ct)) n++;
                code = $"{code}_{n}";
            }
        }
        await ValidateFieldsAsync(type, req.Fields, ct);

        var now = clock.UtcNow;
        var def = new EntryFormDef
        {
            Id = Guid.NewGuid(), Code = code, Name = req.Name.Trim(), RecordType = type,
            IsSystem = false, Active = true, CreatedUtc = now, UpdatedUtc = now,
        };
        db.EntryFormDefs.Add(def);
        AddLayout(def.Id, req.Fields);
        // CF-FIX4-T3 (L4): every new form starts with the record type's native sublist
        // order — immediately rearrangeable in the designer, parity with the standard form.
        var sublist = EntryFormVocabulary.SublistNativeColumns.GetValueOrDefault(type) ?? [];
        db.EntryFormSublistColumns.AddRange(sublist.Select((k, i) => new EntryFormSublistColumn
        {
            Id = Guid.NewGuid(), FormDefId = def.Id, FieldKey = k, Sort = i,
        }));
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(def, ct);
    }

    public async Task<EntryFormDefDto> UpdateAsync(Guid id, SaveEntryFormRequest req, CancellationToken ct = default)
    {
        var def = await LoadUserForm(id, ct);
        await ValidateFieldsAsync(def.RecordType, req.Fields, ct);
        if (!string.IsNullOrWhiteSpace(req.Name)) def.Name = req.Name.Trim();
        def.UpdatedUtc = clock.UtcNow;

        // Replace the FIELDS wholesale (the composer saves the full field list) but SYNC the
        // containers: a field save creates missing subtabs/groups by name and PRESERVES the
        // existing objects (their Hidden/ColumnBreak/Sort state and explicitly-created empty
        // containers survive). Containers are DELETED only through their explicit endpoints,
        // where the guards live (CF5-T2/T3).
        var beforeKeys = await db.EntryFormFields.Where(f => f.FormDefId == def.Id)
            .Select(f => f.FieldKey).ToListAsync(ct);
        db.EntryFormFields.RemoveRange(await db.EntryFormFields.Where(f => f.FormDefId == def.Id).ToListAsync(ct));
        var existingSubtabs = await db.EntryFormSubtabs.Where(s => s.FormDefId == def.Id).ToListAsync(ct);
        var existingGroups = await db.EntryFormGroups.Where(g => g.FormDefId == def.Id).ToListAsync(ct);
        AddLayout(def.Id, req.Fields, existingSubtabs, existingGroups);
        await db.SaveChangesAsync(ct);
        var removed = beforeKeys.Except(req.Fields.Select(x => x.FieldKey), StringComparer.OrdinalIgnoreCase).ToList();
        await ReconcileCustomFieldApplicationsAsync(def.RecordType, removed, ct);
        return await ToDtoAsync(def, ct);
    }

    /// <summary>CF-FIX4-T4: surgical UNPLACE (L6 — deletes the placement row, never values).
    /// System forms allow it for Custom/Segment keys only: the cascade may place onto a
    /// standard form, so removal must be possible there too — otherwise a placed field
    /// could never satisfy the CF-FIX-3 delete guard. Native structure stays frozen.</summary>
    public async Task<EntryFormDefDto> RemoveFieldAsync(Guid formId, string fieldKey, CancellationToken ct = default)
    {
        var def = await db.EntryFormDefs.FirstOrDefaultAsync(d => d.Id == formId, ct)
            ?? throw new NotFoundException($"Entry form {formId} not found.");
        var field = await db.EntryFormFields.FirstOrDefaultAsync(f => f.FormDefId == formId && f.FieldKey == fieldKey, ct)
            ?? throw new NotFoundException($"'{fieldKey}' is not placed on this form.");
        if (def.IsSystem)
        {
            var reg = await db.FieldRegistry.FirstOrDefaultAsync(r => r.RecordType == def.RecordType && r.FieldKey == fieldKey, ct);
            if (reg is null || reg.Kind == Domain.Views.FieldKind.Native)
                throw new DomainRuleException("Standard forms are read-only for native structure — only custom/segment placements can be removed.");
        }
        db.EntryFormFields.Remove(field);
        def.UpdatedUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        await ReconcileCustomFieldApplicationsAsync(def.RecordType, [fieldKey], ct);
        return await ToDtoAsync(def, ct);
    }

    /// <summary>CF-FIX4-T4 (bidirectional invariant, direction B): when a custom field's
    /// LAST placement on a record type's forms disappears, the applies-to application (and
    /// its registry row) drops with it — placement and applies-to never disagree. DATA
    /// SAFETY OUTRANKS TIDINESS: if stored values exist under that type, the application
    /// stays (applied-but-unplaced) so the values remain visible — L6 is the higher law.</summary>
    private async Task ReconcileCustomFieldApplicationsAsync(RecordType type, IReadOnlyCollection<string> removedKeys, CancellationToken ct)
    {
        if (removedKeys.Count == 0) return;
        var changed = false;
        foreach (var key in removedKeys.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            // CF-FIX4-T7 (direction B, segment grain): a segment losing its LAST placement
            // on a type's forms drops its application + registry row — unless ASSIGNMENTS
            // pin it (the data-safety rule, same as values pin custom-field applications).
            var segDef = await db.SegmentDefs.FirstOrDefaultAsync(d => d.Code == key, ct);
            if (segDef is not null)
            {
                var segStillPlaced = await db.EntryFormFields.AnyAsync(x => x.FieldKey == key
                    && db.EntryFormDefs.Any(d2 => d2.Id == x.FormDefId && d2.RecordType == type), ct);
                if (segStillPlaced) continue;
                // CF-FIX5-T7: only the HEADER application carries form placements; a LINE
                // application is flat (never placed), so this reconcile touches header only.
                var segApp = await db.SegmentApplications
                    .FirstOrDefaultAsync(a => a.SegmentDefId == segDef.Id && a.RecordType == type && !a.LineLevel, ct);
                if (segApp is null) continue;
                if (await db.SegmentAssignments.AnyAsync(a => a.SegmentDefId == segDef.Id && a.RecordType == type && a.LineId == null, ct))
                    continue;   // header assignments pin the header application
                db.SegmentApplications.Remove(segApp);
                db.FieldRegistry.RemoveRange(await db.FieldRegistry
                    .Where(r => r.SegmentDefId == segDef.Id && r.RecordType == type).ToListAsync(ct));
                changed = true;
                continue;
            }
            var fieldDef = await db.CustomFieldDefs.FirstOrDefaultAsync(d => d.Code == key, ct);
            if (fieldDef is null) continue;   // native keys are not applications
            var stillPlaced = await db.EntryFormFields.AnyAsync(x => x.FieldKey == key
                && db.EntryFormDefs.Any(d2 => d2.Id == x.FormDefId && d2.RecordType == type), ct);
            if (stillPlaced) continue;
            var app = await db.CustomFieldDefApplications
                .FirstOrDefaultAsync(a => a.FieldDefId == fieldDef.Id && a.RecordType == type, ct);
            if (app is null) continue;
            if (await db.CustomFieldValues.AnyAsync(v => v.FieldDefId == fieldDef.Id && v.RecordType == type, ct))
                continue;   // values pin the application — the field stays applied-but-unplaced
            db.CustomFieldDefApplications.Remove(app);
            var reg = await db.FieldRegistry.FirstOrDefaultAsync(r => r.CustomFieldDefId == fieldDef.Id && r.RecordType == type, ct);
            if (reg is not null) db.FieldRegistry.Remove(reg);
            changed = true;
        }
        if (changed) await db.SaveChangesAsync(ct);
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
        var deletedKeys = await db.EntryFormFields.Where(f => f.FormDefId == def.Id)
            .Select(f => f.FieldKey).ToListAsync(ct);
        db.EntryFormRoleMaps.RemoveRange(await db.EntryFormRoleMaps.Where(m => m.FormDefId == def.Id).ToListAsync(ct));
        db.EntryFormFields.RemoveRange(await db.EntryFormFields.Where(f => f.FormDefId == def.Id).ToListAsync(ct));
        db.EntryFormGroups.RemoveRange(await db.EntryFormGroups.Where(g => g.FormDefId == def.Id).ToListAsync(ct));
        db.EntryFormSubtabs.RemoveRange(await db.EntryFormSubtabs.Where(s => s.FormDefId == def.Id).ToListAsync(ct));
        db.EntryFormDefs.Remove(def);
        await db.SaveChangesAsync(ct);
        await ReconcileCustomFieldApplicationsAsync(def.RecordType, deletedKeys, ct);   // CF-FIX4-T4 direction B
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

    public async Task<ResolvedFormDto> ResolveAsync(string recordType, Guid? formId = null, CancellationToken ct = default)
    {
        var type = Parse(recordType);
        var viewAction = ViewVocabulary.ViewActionFor[type];
        if (!ActionCatalog.RolesFor(viewAction).Any(user.Roles.Contains))
            throw new ForbiddenException("Not permitted for your role.");

        // CF-FIX4-T5: an EXPLICIT form choice changes LAYOUT ONLY — submit re-resolves by
        // role (EnsureSubmittableAsync, OD-D7-2), so a chosen form can never dodge the role
        // form's required fields.
        var def = formId is { } fid
            ? await db.EntryFormDefs.FirstOrDefaultAsync(d => d.Id == fid && d.RecordType == type && d.Active, ct)
                ?? throw new FormValidationException("The chosen form is not an active form of this record type.")
            : await ResolveDefAsync(type, ct);
        var fields = await db.EntryFormFields.AsNoTracking()
            .Where(f => f.FormDefId == def.Id).OrderBy(f => f.Sort).ToListAsync(ct);
        var placement = await PlacementAsync(def.Id, ct);   // CF5: group/subtab objects → the wire strings
        // CF5-T2: a HIDDEN subtab's fields don't render. Hidden is layout, not permission —
        // required fields on a hidden subtab still gate submit (warn-but-allow, ruled D2).
        var hiddenSubtabs = await db.EntryFormSubtabs.AsNoTracking()
            .Where(s => s.FormDefId == def.Id && s.Hidden).Select(s => s.Name).ToListAsync(ct);
        var breaks = await db.EntryFormGroups.AsNoTracking()
            .Where(g => g.FormDefId == def.Id && g.ColumnBreak).Select(g => g.Id).ToListAsync(ct);

        var registry = await db.FieldRegistry.AsNoTracking()
            .Where(r => r.RecordType == type).ToDictionaryAsync(r => r.FieldKey, ct);
        // CFH-T3: a native DIMENSION field (Department/Location/Category/Job) whose key matches
        // a segment's Name renders as a segment-backed searchable PICKER of that segment's active
        // values — not free text. The picker writes the value label to the native column; the
        // companion *Code is derived server-side by the same DimCode the value codes use (aligned).
        var dimSegs = await db.SegmentDefs.AsNoTracking()
            .Where(d => DimensionFieldKeys.Contains(d.Name)).ToListAsync(ct);
        var dimOptions = new Dictionary<string, List<SegmentOptionDto>>();
        foreach (var seg in dimSegs)
        {
            // The picker STORES the value into the native label column (Department = "Maintenance");
            // the *Code companion is derived on save via DimCode. So the option's stored value is
            // the LABEL, not the segment value code — code==label here by design.
            var vals = await db.SegmentValues.AsNoTracking()
                .Where(v => v.SegmentDefId == seg.Id && v.Active).OrderBy(v => v.Sort).ThenBy(v => v.Label)
                .Select(v => new SegmentOptionDto(v.Label, v.Label)).ToListAsync(ct);
            if (vals.Count > 0) dimOptions[seg.Name] = vals;
        }
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
                var cfDef = await db.CustomFieldDefs.AsNoTracking().SingleAsync(d => d.Id == cfId, ct);
                // CF-FIX4-T8: an ARCHIVED field's values are hidden from EVERY live surface —
                // the placement stays (archive is reversible; un-archive restores the render),
                // but the resolved form skips it. The one central predicate.
                if (!Application.CustomFields.CustomFieldVisibility.ValueVisible(cfDef)) continue;
                if (cfDef.CustomListId is { } lid)
                    listCode = (await db.CustomLists.AsNoTracking().SingleAsync(l => l.Id == lid, ct)).Code;
            }
            else if (reg.Kind == FieldKind.Segment && reg.SegmentDefId is { } segId)
            {
                options = await db.SegmentValues.AsNoTracking()
                    .Where(v => v.SegmentDefId == segId && v.Active).OrderBy(v => v.Sort).ThenBy(v => v.Label)
                    .Select(v => new SegmentOptionDto(v.Code, v.Label)).ToListAsync(ct);
            }
            else if (reg.Kind == FieldKind.Native && dimOptions.TryGetValue(f.FieldKey, out var dimVals))
            {
                options = dimVals;   // CFH-T3: dimension native → segment-backed picker
            }

            var (subtabName, groupTitle) = placement[f.GroupId];
            if (subtabName is not null && hiddenSubtabs.Contains(subtabName)) continue;
            resolved.Add(new ResolvedFormFieldDto(
                f.FieldKey, f.Label ?? reg.Label, reg.DataType.ToString(), reg.Kind.ToString(),
                subtabName, groupTitle, f.Sort, f.DisplayType.ToString(), f.RequiredOnForm,
                ResolveDefault(f, reg), f.SourceFieldKey, f.FullWidth, f.Placeholder, listCode, options,
                breaks.Contains(f.GroupId)));
        }
        var sublist = await db.EntryFormSublistColumns.AsNoTracking()
            .Where(c => c.FormDefId == def.Id).OrderBy(c => c.Sort).Select(c => c.FieldKey).ToListAsync(ct);
        var available = await db.EntryFormDefs.AsNoTracking()
            .Where(d => d.RecordType == type && d.Active)
            .OrderByDescending(d => d.IsSystem).ThenBy(d => d.Name)
            .Select(d => new FormChoiceDto(d.Id, d.Name)).ToListAsync(ct);
        return new ResolvedFormDto(def.Id, def.Code, def.Name, type.ToString(), resolved,
            sublist.Count > 0 ? sublist : null,
            available.Count > 1 ? available : null);   // picker only when there IS a choice
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
                // T8: an archived required field can never be filled — it must not gate submit.
                FieldKind.Custom => reg.CustomFieldDefId is { } cfId
                    && (await db.CustomFieldDefs.AsNoTracking().AnyAsync(d => d.Id == cfId && d.ArchivedUtc != null, ct)
                        || await db.CustomFieldValues.AsNoTracking().AnyAsync(v => v.FieldDefId == cfId && v.RecordId == recordId, ct)),
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
        // CF-FIX4-T2: zero fields is VALID — the Header invariant (L3) guarantees structure,
        // and PO/GRN standard forms are placement containers until custom fields land (D-3).
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

    /// <summary>CF-FIX4-T3: the drag-drop write. ONE placement row is re-pointed (L1 — the
    /// same EntryFormField the T4 creation cascade writes); nothing else is touched. Removing
    /// a field from a form is DeleteFieldAsync-by-save (wholesale field replace) or the L6
    /// remove — never a value operation.</summary>
    public async Task<EntryFormDefDto> MoveFieldAsync(Guid formId, string fieldKey, MoveFieldRequest req, CancellationToken ct = default)
    {
        var def = await LoadUserForm(formId, ct);
        var field = await db.EntryFormFields.FirstOrDefaultAsync(f => f.FormDefId == formId && f.FieldKey == fieldKey, ct)
            ?? throw new NotFoundException($"'{fieldKey}' is not placed on this form.");
        var group = await db.EntryFormGroups.FirstOrDefaultAsync(g => g.Id == req.GroupId && g.FormDefId == formId, ct)
            ?? throw new FormValidationException("The target group does not exist on this form.");
        field.GroupId = group.Id;
        field.Sort = req.Sort;
        def.UpdatedUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(def, ct);
    }

    /// <summary>CF-FIX4-T3 (L4): replace the item sublist's column ORDER — flat, no groups.
    /// Keys must be native line columns (SublistNativeColumns) or Line-scope custom fields
    /// applying to this record type.</summary>
    public async Task<EntryFormDefDto> SaveSublistAsync(Guid formId, SaveSublistRequest req, CancellationToken ct = default)
    {
        var def = await LoadUserForm(formId, ct);
        var native = EntryFormVocabulary.SublistNativeColumns.GetValueOrDefault(def.RecordType) ?? [];
        var lineCodes = await db.CustomFieldDefs.AsNoTracking()
            .Where(d => d.Scope == "Line" && d.Active
                && db.CustomFieldDefApplications.Any(a => a.FieldDefId == d.Id && a.RecordType == def.RecordType))
            .Select(d => d.Code).ToListAsync(ct);
        if (req.FieldKeys.Distinct(StringComparer.OrdinalIgnoreCase).Count() != req.FieldKeys.Count)
            throw new FormValidationException("A sublist column may appear once.");
        foreach (var k in req.FieldKeys)
            if (!native.Contains(k) && !lineCodes.Contains(k))
                throw new FormValidationException($"'{k}' is not a line column on {def.RecordType}.");

        db.EntryFormSublistColumns.RemoveRange(db.EntryFormSublistColumns.Where(c => c.FormDefId == formId));
        db.EntryFormSublistColumns.AddRange(req.FieldKeys.Select((k, i) => new EntryFormSublistColumn
        {
            Id = Guid.NewGuid(), FormDefId = formId, FieldKey = k, Sort = i,
        }));
        def.UpdatedUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(def, ct);
    }

    /// <summary>CF5: the composer still SPEAKS strings (subtab name, group title) — this seam
    /// materializes them as subtab/group OBJECTS (find-or-create by name within the form, in
    /// order of first appearance) and points each field at its group. The T2/T3 designer adds
    /// first-class object CRUD on top; the wire contract stays compatible meanwhile.</summary>
    private void AddLayout(Guid formId, List<EntryFormFieldDto> fields,
        List<EntryFormSubtab>? existingSubtabs = null, List<EntryFormGroup>? existingGroups = null)
    {
        var subtabs = new Dictionary<string, EntryFormSubtab>(StringComparer.OrdinalIgnoreCase);
        var groups = new Dictionary<(string? Subtab, string Title), EntryFormGroup>();
        foreach (var s in existingSubtabs ?? []) subtabs[s.Name] = s;
        foreach (var g in existingGroups ?? [])
            groups[(g.SubtabId is { } sid ? (existingSubtabs ?? []).First(s => s.Id == sid).Name : null, g.Title)] = g;
        var preexistingSubtabs = subtabs.Count; var preexistingGroups = groups.Count;
        foreach (var f in fields)
        {
            var subtabName = Norm(f.Subtab);
            var title = string.IsNullOrWhiteSpace(f.FieldGroup) ? "Header" : f.FieldGroup.Trim();
            EntryFormSubtab? subtab = null;
            if (subtabName is not null && !subtabs.TryGetValue(subtabName, out subtab))
            {
                subtab = new EntryFormSubtab
                {
                    Id = Guid.NewGuid(), FormDefId = formId, Name = subtabName,
                    Sort = subtabs.Count, Hidden = false,
                };
                subtabs[subtabName] = subtab;
            }
            var gKey = (subtab?.Name, title);
            if (!groups.TryGetValue(gKey, out var group))
            {
                group = new EntryFormGroup
                {
                    Id = Guid.NewGuid(), FormDefId = formId, SubtabId = subtab?.Id,
                    Title = title, Sort = groups.Count, ColumnBreak = false,
                    IsHeader = subtab is null && title == "Header",
                };
                groups[gKey] = group;
            }
            db.EntryFormFields.Add(new EntryFormField
            {
                Id = Guid.NewGuid(), FormDefId = formId, FieldKey = f.FieldKey, GroupId = group.Id,
                Sort = f.Sort, DisplayType = Enum.Parse<EntryFormDisplayType>(f.DisplayType),
                RequiredOnForm = f.RequiredOnForm, DefaultValue = Norm(f.DefaultValue),
                SourceFieldKey = Norm(f.SourceFieldKey), FullWidth = f.FullWidth,
                Label = Norm(f.Label), Placeholder = Norm(f.Placeholder),
            });
        }
        // L3 (CF-FIX4-T1): a form can NEVER persist without its Header group — even when no
        // field lands in it, the invariant group is materialized so the placement cascade
        // always has a valid landing zone.
        if (!groups.Values.Any(g => g.IsHeader) && (existingGroups ?? []).All(g => !g.IsHeader))
            groups[(null, "Header")] = new EntryFormGroup
            {
                Id = Guid.NewGuid(), FormDefId = formId, SubtabId = null,
                Title = "Header", Sort = -1, ColumnBreak = false, IsHeader = true,
            };
        db.EntryFormSubtabs.AddRange(subtabs.Values.Where(s => (existingSubtabs ?? []).All(x => x.Id != s.Id)));
        db.EntryFormGroups.AddRange(groups.Values.Where(g => (existingGroups ?? []).All(x => x.Id != g.Id)));
        _ = (preexistingSubtabs, preexistingGroups);   // sort counters seeded from the dictionaries
    }

    /// <summary>GroupId → (subtab name, group title) for a form — the object→string join.</summary>
    private async Task<Dictionary<Guid, (string? Subtab, string Group)>> PlacementAsync(Guid formId, CancellationToken ct)
    {
        var subtabs = await db.EntryFormSubtabs.AsNoTracking()
            .Where(s => s.FormDefId == formId).ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        return await db.EntryFormGroups.AsNoTracking()
            .Where(g => g.FormDefId == formId)
            .ToDictionaryAsync(g => g.Id,
                g => ((string?)(g.SubtabId is { } sid ? subtabs[sid] : null), g.Title), ct);
    }

    private static string? Norm(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private async Task<EntryFormDefDto> ToDtoAsync(EntryFormDef def, CancellationToken ct)
    {
        var fields = await db.EntryFormFields.AsNoTracking()
            .Where(f => f.FormDefId == def.Id).OrderBy(f => f.Sort).ToListAsync(ct);
        var roles = await db.EntryFormRoleMaps.AsNoTracking()
            .Where(m => m.FormDefId == def.Id).Select(m => m.Role).OrderBy(r => r).ToListAsync(ct);
        var placement = await PlacementAsync(def.Id, ct);
        var subtabs = await db.EntryFormSubtabs.AsNoTracking().Where(s => s.FormDefId == def.Id)
            .OrderBy(s => s.Sort).ThenBy(s => s.Name)
            .Select(s => new EntryFormSubtabDto(s.Id, s.Name, s.Sort, s.Hidden)).ToListAsync(ct);
        var groups = await db.EntryFormGroups.AsNoTracking().Where(g => g.FormDefId == def.Id)
            .OrderBy(g => g.Sort).ThenBy(g => g.Title)
            .Select(g => new EntryFormGroupDto(g.Id, g.SubtabId, g.Title, g.Sort, g.ColumnBreak, g.IsHeader)).ToListAsync(ct);
        return new EntryFormDefDto(def.Id, def.Code, def.Name, def.RecordType.ToString(), def.IsSystem, def.Active,
            fields.Select(f => new EntryFormFieldDto(f.FieldKey, placement[f.GroupId].Subtab, placement[f.GroupId].Group, f.Sort,
                f.DisplayType.ToString(), f.RequiredOnForm, f.DefaultValue, f.SourceFieldKey,
                f.FullWidth, f.Label, f.Placeholder, f.GroupId)).ToList(),
            roles, subtabs, groups,
            await db.EntryFormSublistColumns.AsNoTracking().Where(c => c.FormDefId == def.Id)
                .OrderBy(c => c.Sort).Select(c => c.FieldKey).ToListAsync(ct));
    }

    // ---------- CF5-T2/T3: layout-object CRUD ----------

    public async Task<EntryFormDefDto> CreateSubtabAsync(Guid formId, SaveSubtabRequest req, CancellationToken ct = default)
    {
        var def = await LoadUserForm(formId, ct);
        var name = Norm(req.Name) ?? throw new FormValidationException("A subtab needs a name.");
        if (await db.EntryFormSubtabs.AnyAsync(s => s.FormDefId == formId && s.Name == name, ct))
            throw new FormValidationException($"Subtab '{name}' already exists on this form.");
        db.EntryFormSubtabs.Add(new EntryFormSubtab
        {
            Id = Guid.NewGuid(), FormDefId = formId, Name = name, Sort = req.Sort, Hidden = req.Hidden,
        });
        def.UpdatedUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(def, ct);
    }

    public async Task<EntryFormDefDto> UpdateSubtabAsync(Guid formId, Guid subtabId, SaveSubtabRequest req, CancellationToken ct = default)
    {
        var def = await LoadUserForm(formId, ct);
        var subtab = await db.EntryFormSubtabs.FirstOrDefaultAsync(s => s.Id == subtabId && s.FormDefId == formId, ct)
            ?? throw new NotFoundException("Subtab not found on this form.");
        var name = Norm(req.Name) ?? throw new FormValidationException("A subtab needs a name.");
        if (await db.EntryFormSubtabs.AnyAsync(s => s.FormDefId == formId && s.Name == name && s.Id != subtabId, ct))
            throw new FormValidationException($"Subtab '{name}' already exists on this form.");
        subtab.Name = name; subtab.Sort = req.Sort; subtab.Hidden = req.Hidden;
        def.UpdatedUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(def, ct);
    }

    public async Task<EntryFormDefDto> DeleteSubtabAsync(Guid formId, Guid subtabId, CancellationToken ct = default)
    {
        var def = await LoadUserForm(formId, ct);
        var subtab = await db.EntryFormSubtabs.FirstOrDefaultAsync(s => s.Id == subtabId && s.FormDefId == formId, ct)
            ?? throw new NotFoundException("Subtab not found on this form.");
        // The never-silently-drop rule: a subtab with groups refuses deletion (move or hide instead).
        if (await db.EntryFormGroups.AnyAsync(g => g.SubtabId == subtabId, ct))
            throw new DomainRuleException("This subtab still holds field groups — move its fields (or hide the subtab) first.");
        db.EntryFormSubtabs.Remove(subtab);
        def.UpdatedUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(def, ct);
    }

    public async Task<EntryFormDefDto> CreateGroupAsync(Guid formId, SaveGroupRequest req, CancellationToken ct = default)
    {
        var def = await LoadUserForm(formId, ct);
        var title = Norm(req.Title) ?? throw new FormValidationException("A field group needs a title.");
        if (req.SubtabId is { } sid && !await db.EntryFormSubtabs.AnyAsync(s => s.Id == sid && s.FormDefId == formId, ct))
            throw new FormValidationException("The target subtab does not exist on this form.");
        if (await db.EntryFormGroups.AnyAsync(g => g.FormDefId == formId && g.SubtabId == req.SubtabId && g.Title == title, ct))
            throw new FormValidationException($"Group '{title}' already exists in that container.");
        db.EntryFormGroups.Add(new EntryFormGroup
        {
            Id = Guid.NewGuid(), FormDefId = formId, SubtabId = req.SubtabId,
            Title = title, Sort = req.Sort, ColumnBreak = req.ColumnBreak,
        });
        def.UpdatedUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(def, ct);
    }

    public async Task<EntryFormDefDto> UpdateGroupAsync(Guid formId, Guid groupId, SaveGroupRequest req, CancellationToken ct = default)
    {
        var def = await LoadUserForm(formId, ct);
        var group = await db.EntryFormGroups.FirstOrDefaultAsync(g => g.Id == groupId && g.FormDefId == formId, ct)
            ?? throw new NotFoundException("Field group not found on this form.");
        var title = Norm(req.Title) ?? throw new FormValidationException("A field group needs a title.");
        if (req.SubtabId is { } sid && !await db.EntryFormSubtabs.AnyAsync(s => s.Id == sid && s.FormDefId == formId, ct))
            throw new FormValidationException("The target subtab does not exist on this form.");
        // L3: Header stays on the BODY, always — it is the cascade's guaranteed landing zone,
        // and a subtab can be hidden. (Rename is fine on non-system forms; system forms never
        // reach here — LoadUserForm refuses them wholesale.)
        if (group.IsHeader && req.SubtabId is not null)
            throw new DomainRuleException("The Header group is the form's fixed landing zone — it cannot move into a subtab.");
        group.Title = title; group.SubtabId = req.SubtabId; group.Sort = req.Sort; group.ColumnBreak = req.ColumnBreak;
        def.UpdatedUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(def, ct);
    }

    public async Task<EntryFormDefDto> DeleteGroupAsync(Guid formId, Guid groupId, CancellationToken ct = default)
    {
        var def = await LoadUserForm(formId, ct);
        var group = await db.EntryFormGroups.FirstOrDefaultAsync(g => g.Id == groupId && g.FormDefId == formId, ct)
            ?? throw new NotFoundException("Field group not found on this form.");
        if (group.IsHeader)
            throw new DomainRuleException("The Header group cannot be deleted — every form keeps it so field placement always has a valid target (L3).");
        if (await db.EntryFormFields.AnyAsync(f => f.GroupId == groupId, ct))
            throw new DomainRuleException("This group still holds fields — move them first.");
        db.EntryFormGroups.Remove(group);
        def.UpdatedUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(def, ct);
    }

    private static RecordType Parse(string raw) =>
        Enum.TryParse<RecordType>(raw, ignoreCase: true, out var t)
            ? t : throw new FormValidationException($"Unknown record type '{raw}'.");
}
