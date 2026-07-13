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

        // CF-FIX1-T5: the Internal ID is USER-SET (NetSuite-style) — the user controls the
        // meaningful part, the system guarantees the cf_ namespace. Absent → derived from the
        // label (API/back-compat path). Same immutability as before.
        string code;
        if (!string.IsNullOrWhiteSpace(req.Code))
        {
            var part = req.Code.Trim().ToLowerInvariant();
            if (part.StartsWith("cf_")) part = part[3..];
            if (part.Length == 0)
                throw new CustomFieldValidationException("The Internal ID needs a value after the cf_ prefix.");
            if (!System.Text.RegularExpressions.Regex.IsMatch(part, "^[a-z0-9_]+$"))
                throw new CustomFieldValidationException("The Internal ID may only use letters, digits and underscores.");
            code = "cf_" + part;
        }
        else
        {
            code = "cf_" + new string(req.Label.Trim().ToLowerInvariant()
                .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray()).Trim('_');
        }
        if (code.Length > 60) code = code[..60];
        if (await db.CustomFieldDefs.AnyAsync(d => d.Code == code, ct))
            throw new CustomFieldValidationException($"A custom field with Internal ID '{code}' already exists — choose another.");
        if (FieldRegistrySeed.Rows.Any(r => r.RecordType == type && string.Equals(r.FieldKey, code, StringComparison.OrdinalIgnoreCase)))
            throw new CustomFieldValidationException($"'{code}' collides with a native field key.");

        var scope = ParseScope(req.Scope);
        if (scope == "Line" && req.ShowInList)
            throw new CustomFieldValidationException("Show-in-list is a header-list concept — a LINE field has no header-list row. (Line-level search is deferred.)");
        if (scope == "Line" && !LineOwnership.SupportedTypes.Contains(type))
            throw new CustomFieldValidationException($"Line fields aren't supported on {type} yet — first delivery is Requisition/PurchaseOrder/Rfq lines.");

        var def = new CustomFieldDef
        {
            Code = code, Label = req.Label.Trim(), RecordType = type, DataType = dataType,
            CustomListId = req.CustomListId, Required = req.Required,
            HelpText = req.HelpText ?? "", Sort = req.Sort,
            DisplayType = ParseDisplayType(req.DisplayType), ShowInList = req.ShowInList,
            Scope = scope,
            CreatedUtc = clock.UtcNow, UpdatedUtc = clock.UtcNow,
        };
        db.CustomFieldDefs.Add(def);
        // The registry row IS the D3/D4 integration — same transaction, no drift window.
        // CF6-T1: LINE defs stay OUT of the registry — the view runner is header-grain (a
        // locked deferral); a registry row would put the field in the builder palette and lie.
        if (scope == "Header")
            db.FieldRegistry.Add(new FieldRegistryEntry
            {
                Id = def.Id,                               // def id doubles as the registry id for Custom rows
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
        var reg = await db.FieldRegistry.FirstOrDefaultAsync(r => r.CustomFieldDefId == def.Id, ct);
        if (reg is not null) reg.Label = def.Label;   // line defs carry no registry row (CF6-T1)
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
        var defs = await db.CustomFieldDefs.AsNoTracking()
            .Where(d => d.RecordType == type && d.Active && d.Scope == "Header")
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
        var defs = await db.CustomFieldDefs.AsNoTracking()
            .Where(d => d.RecordType == type && d.Active && d.Scope == "Line")
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
        var defs = await db.CustomFieldDefs.AsNoTracking()
            .Where(d => d.RecordType == type && d.Active && d.Scope == "Line")
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

    private static CustomFieldDefDto ToDto(CustomFieldDef d, int valueCount) => new(
        d.Id, d.Code, d.Label, d.RecordType.ToString(), d.DataType.ToString(), d.CustomListId,
        d.Required, d.HelpText, d.Active, d.Sort, valueCount, d.DisplayType, d.ShowInList, d.Scope);

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
