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
    IRecordReachability reachability) : ICustomFieldService
{
    // ---------- defs (A65) ----------

    public async Task<IReadOnlyList<CustomFieldDefDto>> ListDefsAsync(string? recordType, CancellationToken ct = default)
    {
        var q = db.CustomFieldDefs.AsNoTracking().AsQueryable();
        if (recordType is not null) q = q.Where(d => d.RecordType == Parse(recordType));
        var defs = await q.OrderBy(d => d.RecordType).ThenBy(d => d.Sort).ThenBy(d => d.Label).ToListAsync(ct);
        var counts = await db.CustomFieldValues.AsNoTracking()
            .GroupBy(v => v.FieldDefId).Select(g => new { g.Key, N = g.Count() }).ToListAsync(ct);
        var byDef = counts.ToDictionary(x => x.Key, x => x.N);
        return defs.Select(d => ToDto(d, byDef.GetValueOrDefault(d.Id))).ToList();
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

        var code = "cf_" + new string(req.Label.Trim().ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray()).Trim('_');
        if (code.Length > 60) code = code[..60];
        if (await db.CustomFieldDefs.AnyAsync(d => d.Code == code, ct))
            throw new CustomFieldValidationException($"A custom field with code '{code}' already exists.");
        if (FieldRegistrySeed.Rows.Any(r => r.RecordType == type && string.Equals(r.FieldKey, code, StringComparison.OrdinalIgnoreCase)))
            throw new CustomFieldValidationException($"'{code}' collides with a native field key.");

        var def = new CustomFieldDef
        {
            Code = code, Label = req.Label.Trim(), RecordType = type, DataType = dataType,
            CustomListId = req.CustomListId, Required = req.Required,
            HelpText = req.HelpText ?? "", Sort = req.Sort,
            DisplayType = ParseDisplayType(req.DisplayType), ShowInList = req.ShowInList,
            CreatedUtc = clock.UtcNow, UpdatedUtc = clock.UtcNow,
        };
        await ApplyInsertBeforeAsync(def, req.InsertBeforeId, req.Sort, ct);
        db.CustomFieldDefs.Add(def);
        // The registry row IS the D3/D4 integration — same transaction, no drift window.
        db.FieldRegistry.Add(new FieldRegistryEntry
        {
            Id = def.Id,                                   // def id doubles as the registry id for Custom rows
            RecordType = type, FieldKey = code, Kind = FieldKind.Custom,
            Label = def.Label, DataType = RegistryTypeOf(dataType), CustomFieldDefId = def.Id,
        });
        await db.SaveChangesAsync(ct);
        return ToDto(def, 0);
    }

    public async Task<CustomFieldDefDto> UpdateDefAsync(Guid id, SaveCustomFieldDefRequest req, CancellationToken ct = default)
    {
        var def = await Load(id, ct);
        if (Parse(req.RecordType) != def.RecordType || ParseDataType(req.DataType) != def.DataType)
            throw new CustomFieldValidationException("Code, record type and data type are immutable — create a new field instead.");
        if (string.IsNullOrWhiteSpace(req.Label))
            throw new CustomFieldValidationException("A custom field needs a label.");
        def.Label = req.Label.Trim();
        def.Required = req.Required;
        def.HelpText = req.HelpText ?? "";
        def.DisplayType = ParseDisplayType(req.DisplayType);
        def.ShowInList = req.ShowInList;
        await ApplyInsertBeforeAsync(def, req.InsertBeforeId, req.Sort, ct);
        def.UpdatedUtc = clock.UtcNow;
        var reg = await db.FieldRegistry.FirstAsync(r => r.CustomFieldDefId == def.Id, ct);
        reg.Label = def.Label;
        await db.SaveChangesAsync(ct);
        return ToDto(def, await db.CustomFieldValues.CountAsync(v => v.FieldDefId == def.Id, ct));
    }

    public async Task<CustomFieldDefDto> SetDefActiveAsync(Guid id, bool active, CancellationToken ct = default)
    {
        var def = await Load(id, ct);
        def.Active = active;
        def.UpdatedUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToDto(def, await db.CustomFieldValues.CountAsync(v => v.FieldDefId == def.Id, ct));
    }

    public async Task DeleteDefAsync(Guid id, CancellationToken ct = default)
    {
        var def = await Load(id, ct);
        // Ruled: any values EVER written → deactivate-only, forever.
        if (await db.CustomFieldValues.AnyAsync(v => v.FieldDefId == def.Id, ct))
            throw new Domain.DomainRuleException("This field has values — deactivate it instead; fields with data are never deleted.");
        var reg = await db.FieldRegistry.Where(r => r.CustomFieldDefId == def.Id).ToListAsync(ct);
        db.FieldRegistry.RemoveRange(reg);
        db.CustomFieldDefs.Remove(def);
        await db.SaveChangesAsync(ct);
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

        var defs = await db.CustomFieldDefs.Where(d => d.RecordType == type && d.Active).ToListAsync(ct);
        var byCode = defs.ToDictionary(d => d.Code, StringComparer.OrdinalIgnoreCase);
        foreach (var key in req.Values.Keys)
            if (!byCode.ContainsKey(key))
                throw new CustomFieldValidationException($"Unknown or inactive custom field '{key}' for {type}.");

        var existing = await db.CustomFieldValues
            .Where(v => v.RecordType == type && v.RecordId == recordId).ToListAsync(ct);

        foreach (var def in defs)
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

    private async Task WriteTypedAsync(CustomFieldValue row, CustomFieldDef def, string value, CancellationToken ct)
    {
        row.ValueText = null; row.ValueNumber = null; row.ValueMoney = null;
        row.ValueDate = null; row.ValueBool = null; row.ValueListCode = null;
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
                if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", out var dt))
                    throw new CustomFieldValidationException($"'{def.Label}' must be an ISO date (yyyy-MM-dd).");
                row.ValueDate = dt;
                break;
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
        var defs = await db.CustomFieldDefs.AsNoTracking()
            .Where(d => d.RecordType == type && d.Active)
            .OrderBy(d => d.Sort).ThenBy(d => d.Label).ToListAsync(ct);
        var values = await db.CustomFieldValues.AsNoTracking()
            .Where(v => v.RecordType == type && v.RecordId == recordId).ToListAsync(ct);
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
        { ValueText: { } t } => t,
        { ValueNumber: { } n } => n % 1 == 0 ? ((long)n).ToString(CultureInfo.InvariantCulture) : n.ToString(CultureInfo.InvariantCulture),
        { ValueMoney: { } m } => m.ToString(CultureInfo.InvariantCulture),
        { ValueDate: { } d } => d.ToString("yyyy-MM-dd"),
        { ValueBool: { } b } => b ? "true" : "false",
        { ValueListCode: { } c } => c,
        _ => null,
    };


    private async Task<CustomFieldDef> Load(Guid id, CancellationToken ct) =>
        await db.CustomFieldDefs.FirstOrDefaultAsync(d => d.Id == id, ct)
            ?? throw new NotFoundException($"Custom field {id} not found.");

    private static CustomFieldDefDto ToDto(CustomFieldDef d, int valueCount) => new(
        d.Id, d.Code, d.Label, d.RecordType.ToString(), d.DataType.ToString(), d.CustomListId,
        d.Required, d.HelpText, d.Active, d.Sort, valueCount, d.DisplayType, d.ShowInList);

    private static readonly string[] DisplayTypes = ["Normal", "Disabled", "Inline"];

    private static string ParseDisplayType(string raw) =>
        DisplayTypes.FirstOrDefault(x => string.Equals(x, raw, StringComparison.OrdinalIgnoreCase))
            ?? throw new CustomFieldValidationException($"Display type must be one of: {string.Join(", ", DisplayTypes)}.");

    /// <summary>CF4-T12 insert-before: the def takes the target's slot; the target and every
    /// def at/after it shift down one. Null target = the raw integer sort, as before.</summary>
    private async Task ApplyInsertBeforeAsync(CustomFieldDef def, Guid? insertBeforeId, int fallbackSort, CancellationToken ct)
    {
        if (insertBeforeId is not { } beforeId) { def.Sort = fallbackSort; return; }
        if (beforeId == def.Id)
            throw new CustomFieldValidationException("A field cannot be inserted before itself.");
        // Normalize the WHOLE sibling order (the display order: Sort then Label) with the def
        // wedged in at the target's index — integer ties are resolved once, deterministically.
        var siblings = await db.CustomFieldDefs
            .Where(d => d.RecordType == def.RecordType && d.Id != def.Id)
            .OrderBy(d => d.Sort).ThenBy(d => d.Label).ToListAsync(ct);
        var idx = siblings.FindIndex(d => d.Id == beforeId);
        if (idx < 0)
            throw new CustomFieldValidationException("The insert-before field does not exist on this record type.");
        siblings.Insert(idx, def);
        for (var i = 0; i < siblings.Count; i++) siblings[i].Sort = i;
    }

    private static RecordType Parse(string raw) =>
        Enum.TryParse<RecordType>(raw, ignoreCase: true, out var t)
            ? t : throw new CustomFieldValidationException($"Unknown record type '{raw}'.");

    private static CustomFieldDataType ParseDataType(string raw) =>
        Enum.TryParse<CustomFieldDataType>(raw, ignoreCase: true, out var t)
            ? t : throw new CustomFieldValidationException($"Unknown data type '{raw}'.");

    internal static FieldDataType RegistryTypeOf(CustomFieldDataType t) => t switch
    {
        CustomFieldDataType.Text or CustomFieldDataType.LongText => FieldDataType.Text,
        CustomFieldDataType.Int or CustomFieldDataType.Decimal => FieldDataType.Number,
        CustomFieldDataType.Money => FieldDataType.Money,
        CustomFieldDataType.Date => FieldDataType.Date,
        CustomFieldDataType.Bool => FieldDataType.Bool,
        CustomFieldDataType.ListValue => FieldDataType.Enum,
        _ => FieldDataType.Text,
    };
}
