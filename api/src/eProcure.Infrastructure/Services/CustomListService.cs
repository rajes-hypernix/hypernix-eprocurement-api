using eProcure.Application;
using eProcure.Application.Abstractions;
using eProcure.Application.Configuration;
using eProcure.Domain;
using eProcure.Domain.Configuration;
using eProcure.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eProcure.Infrastructure.Services;

public sealed class CustomListService(
    AppDbContext db, IClock clock,
    IEnumerable<Application.CustomFields.ICustomListValueReferenceProvider>? valueRefProviders = null,
    IEnumerable<Application.CustomFields.ICustomListValueDataProvider>? valueDataProviders = null,
    Application.Abstractions.IAuditLog? audit = null) : ICustomListService
{
    public async Task<IReadOnlyList<CustomListDto>> ListAsync(CancellationToken ct = default)
    {
        var lists = await db.CustomLists.AsNoTracking().Include(l => l.Values)
            .OrderBy(l => l.Name).ToListAsync(ct);
        return [.. lists.Select(ToDto)];
    }

    public async Task<CustomListDto?> GetAsync(string code, CancellationToken ct = default)
    {
        var list = await db.CustomLists.AsNoTracking().Include(l => l.Values)
            .FirstOrDefaultAsync(l => l.Code == code, ct);
        return list is null ? null : ToDto(list);
    }

    public async Task<CustomListDto> CreateListAsync(CreateCustomListRequest req, CancellationToken ct = default)
    {
        // CF-FIX1-T4 + CF-FIX2-T1: ONE id concept, NetSuite-prefixed — the user keys the
        // meaningful part; the system stores CUSTLIST_<PART> (lists keep the UPPER-case
        // convention). Existing unprefixed codes (BANK, COUNTRY…) are immutable and resolve
        // unchanged — the non-breaking choice, no rename.
        var rawPart = req.Code.Trim().ToUpperInvariant();
        if (rawPart.StartsWith("CUSTLIST_")) rawPart = rawPart["CUSTLIST_".Length..];
        if (string.IsNullOrWhiteSpace(rawPart)) throw new DomainRuleException("A custom list needs an Internal ID.");
        if (!System.Text.RegularExpressions.Regex.IsMatch(rawPart, "^[A-Z0-9_]+$"))
            throw new DomainRuleException("The Internal ID may only use letters, digits and underscores.");
        var code = "CUSTLIST_" + rawPart;
        if (await db.CustomLists.AnyAsync(l => l.Code == code, ct))
            throw new DomainRuleException($"A custom list with Internal ID '{code}' already exists.");
        // CF-FIX1-T9: a dangling list-level dependency is rejected at create. Reparenting has
        // NO api path (UpdateCustomListRequest carries no ParentListCode) — so list-level
        // dependency CYCLES are impossible BY CONSTRUCTION: a parent must already exist when
        // its child is created, and neither end can be re-pointed afterwards.
        if (req.ParentListCode is { } plc && !await db.CustomLists.AnyAsync(l => l.Code == plc, ct))
            throw new DomainRuleException($"Parent list '{plc}' does not exist.");

        if (req.OrderMode is not ("Entered" or "Alphabetical"))
            throw new DomainRuleException("OrderMode must be Entered or Alphabetical.");
        var now = clock.UtcNow;
        var list = new CustomList { Code = code, Name = req.Name.Trim(), Description = req.Description, ParentListCode = req.ParentListCode, OrderMode = req.OrderMode, IsSystem = false, CreatedUtc = now, UpdatedUtc = now };
        db.CustomLists.Add(list);
        await db.SaveChangesAsync(ct);
        return ToDto(list);
    }

    public async Task<CustomListDto> UpdateListAsync(string code, UpdateCustomListRequest req, CancellationToken ct = default)
    {
        var list = await LoadList(code, ct);
        if (string.IsNullOrWhiteSpace(req.Name))
            throw new DomainRuleException("A list needs a name.");
        if (req.OrderMode is not ("Entered" or "Alphabetical"))
            throw new DomainRuleException("OrderMode must be Entered or Alphabetical.");
        list.Name = req.Name.Trim();
        list.Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim();
        list.OrderMode = req.OrderMode;
        list.UpdatedUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToDto(list);
    }

    public async Task<CustomListDto> SetListActiveAsync(string code, bool active, CancellationToken ct = default)
    {
        var list = await LoadList(code, ct);
        if (list.IsSystem && !active)
            throw new DomainRuleException($"'{list.Name}' is a system list — native fields source it; it cannot be deactivated.");
        list.Active = active;
        list.UpdatedUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToDto(list);
    }

    /// <summary>CF1-T2: guarded list delete, the A2F-T3 discipline at list grain — a
    /// referenced list DEACTIVATES (a stored code never degrades), a clean one hard-deletes.</summary>
    public async Task<CustomListDto?> DeleteListAsync(string code, CancellationToken ct = default)
    {
        var list = await LoadList(code, ct);
        if (list.IsSystem)
            throw new DomainRuleException($"'{list.Name}' is a system list — native fields source it; it cannot be deleted.");

        var bound = await db.CustomFieldDefs.AsNoTracking().AnyAsync(d => d.CustomListId == list.Id, ct);
        var anyValueReferenced = false;
        foreach (var v in list.Values)
            if (await IsReferencedAsync(list.Code, v.Code, ct)) { anyValueReferenced = true; break; }

        if (bound || anyValueReferenced)
        {
            list.Active = false;
            list.UpdatedUtc = clock.UtcNow;
            await db.SaveChangesAsync(ct);
            return ToDto(list);
        }

        db.CustomListValues.RemoveRange(list.Values);
        db.CustomLists.Remove(list);
        await db.SaveChangesAsync(ct);
        return null;
    }

    public async Task<CustomListValueDto> AddValueAsync(string listCode, AddCustomListValueRequest req, CancellationToken ct = default)
    {
        var list = await LoadList(listCode, ct);
        // CF-FIX1-T6: value internal ids are AUTOMATIC — 1, 2, 3… in entry order. The user types
        // only the label. Explicit codes remain accepted (seeds/system lists keep their string
        // codes like "MY"/"NET30" — nothing stored is ever renumbered, so no orphaned references).
        var code = req.Code?.Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            var next = list.Values
                .Select(v => int.TryParse(v.Code, out var n) ? n : 0)
                .DefaultIfEmpty(0).Max() + 1;
            code = next.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        if (list.Values.Any(v => v.Code == code)) throw new DomainRuleException($"Value '{code}' already exists in {list.Name}.");
        await ValidateParentValueAsync(list, req.ParentValueCode, selfCode: null, ct);

        var value = new CustomListValue
        {
            CustomListId = list.Id, Code = code, Label = req.Label.Trim(), ParentValueCode = req.ParentValueCode,
            Sort = list.Values.Count == 0 ? 0 : list.Values.Max(v => v.Sort) + 1, Active = true,
        };
        db.CustomListValues.Add(value);
        list.UpdatedUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToDto(value);
    }

    public async Task<CustomListValueDto> UpdateValueAsync(Guid valueId, UpdateCustomListValueRequest req, CancellationToken ct = default)
    {
        var value = await db.CustomListValues.FirstOrDefaultAsync(v => v.Id == valueId, ct)
            ?? throw new NotFoundException($"List value {valueId} not found.");
        value.Label = req.Label.Trim();
        var ownerList = await db.CustomLists.AsNoTracking().Include(l => l.Values)
            .FirstAsync(l => l.Id == value.CustomListId, ct);
        await ValidateParentValueAsync(ownerList, req.ParentValueCode, selfCode: value.Code, ct);
        value.ParentValueCode = req.ParentValueCode;
        value.Sort = req.Sort;
        value.Active = req.Active;
        await db.SaveChangesAsync(ct);
        return ToDto(value);
    }

    public async Task<CustomListValueDto?> DeleteValueAsync(Guid valueId, CancellationToken ct = default)
    {
        var value = await db.CustomListValues.FirstOrDefaultAsync(v => v.Id == valueId, ct)
            ?? throw new NotFoundException($"List value {valueId} not found.");
        var list = await db.CustomLists.AsNoTracking().FirstAsync(l => l.Id == value.CustomListId, ct);

        // CF-FIX3-T4: the three-tier rule replaces the silent deactivate-fallback. Delete
        // requires zero config references (every registered provider), zero LIVE usage,
        // zero historical usage (historical → the governed Purge) and no dependent children.
        // The old IsReferencedAsync probes now live inside the registered data providers.
        var report = await GetValueReferencesForAsync(list, value, ct);
        if (!report.CanDelete)
            throw new DomainRuleException(report.BlockedReason ?? "This value cannot be deleted — check its impact report.");
        if (await HasDependentChildrenAsync(list, value.Code, ct))
            throw new DomainRuleException("Other values point at this one (dependent or tree children) — repoint or delete them first, or deactivate this value.");

        db.CustomListValues.Remove(value);
        await db.SaveChangesAsync(ct);
        return null;
    }

    public async Task<Application.CustomFields.ImpactReportDto> GetValueReferencesAsync(Guid valueId, CancellationToken ct = default)
    {
        var value = await db.CustomListValues.AsNoTracking().FirstOrDefaultAsync(v => v.Id == valueId, ct)
            ?? throw new NotFoundException($"List value {valueId} not found.");
        var list = await db.CustomLists.AsNoTracking().FirstAsync(l => l.Id == value.CustomListId, ct);
        return await GetValueReferencesForAsync(list, value, ct);
    }

    private async Task<Application.CustomFields.ImpactReportDto> GetValueReferencesForAsync(
        CustomList list, CustomListValue value, CancellationToken ct)
    {
        var refs = new List<Application.CustomFields.FieldReference>();
        foreach (var p in valueRefProviders ?? [])
            refs.AddRange(await p.FindReferencesAsync(list.Id, list.Code, value.Code, ct));
        var data = new List<Application.CustomFields.DataReferenceSummary>();
        foreach (var p in valueDataProviders ?? [])
            data.Add(await p.CountValueUsageAsync(list.Id, list.Code, value.Code, ct));
        var live = data.Sum(d => d.LiveCount);
        var historical = data.Sum(d => d.HistoricalCount);
        var blocked =
            refs.Count > 0 ? $"Still referenced by {string.Join(", ", refs.Select(r => r.ConsumerName).Distinct())} — clear those first."
            : live > 0 ? $"Held by {live} live record(s)/master rows — a value in live use is never deleted."
            : null;
        return new Application.CustomFields.ImpactReportDto(refs, data, live, historical,
            CanDelete: refs.Count == 0 && live == 0 && historical == 0,
            CanPurge: refs.Count == 0 && live == 0 && historical > 0,
            blocked ?? (historical > 0 ? $"{historical} value(s) remain on closed/historical records — Purge (governed) removes them with a snapshot." : null));
    }

    /// <summary>CF-FIX3-T4 Tier 3 for a LIST VALUE — same contract as the field purge:
    /// one transaction, re-verified INSIDE it, a snapshot per removed value, then the value
    /// row itself goes. Gated on PurgeCustomFieldHistory (A73) at the endpoint.</summary>
    public async Task PurgeValueAsync(Guid valueId, CancellationToken ct = default)
    {
        var value = await db.CustomListValues.FirstOrDefaultAsync(v => v.Id == valueId, ct)
            ?? throw new NotFoundException($"List value {valueId} not found.");
        var list = await db.CustomLists.AsNoTracking().FirstAsync(l => l.Id == value.CustomListId, ct);
        var log = audit ?? throw new InvalidOperationException("Purge requires the audit log.");
        var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            var report = await GetValueReferencesForAsync(list, value, ct);
            if (!report.CanPurge)
                throw new DomainRuleException(report.BlockedReason ?? "Purge needs clear references, no live usage and historical usage present.");
            if (await HasDependentChildrenAsync(list, value.Code, ct))
                throw new DomainRuleException("Other values point at this one — repoint or delete them first.");

            var totalRemoved = 0;
            foreach (var p in valueDataProviders ?? [])
            {
                var snapshot = await p.PurgeHistoricalAsync(list.Id, list.Code, value.Code, ct);
                foreach (var v in snapshot.Removed)
                    await log.WriteAsync("CustomListValue", $"{list.Code}:{value.Code}", "Purged historical value",
                        before: $"{v.RecordType} {v.RecordLabel}{(v.LineId is not null ? $" line {v.LineId}" : "")}",
                        after: v.Value, ct: ct);
                totalRemoved += snapshot.Removed.Count;
            }
            db.CustomListValues.Remove(value);
            await db.SaveChangesAsync(ct);
            await log.WriteAsync("CustomListValue", $"{list.Code}:{value.Code}", "Purged and deleted",
                after: $"{totalRemoved} historical value(s) removed (snapshotted above); value '{value.Label}' deleted", ct: ct);
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

    /// <summary>Every store a list value's CODE can live in — the universal custom-field
    /// probe plus, per SYSTEM list, the native code-holding columns it sources. A probe
    /// that over-matches merely deactivates (conservative + reversible via UpdateValueAsync);
    /// a miss would strand a dangling code, so new native list-sourced columns must be
    /// added here. Onboarding questionnaire ANSWERS are deliberately not probed: they store
    /// free values per question order, not typed list references (matching on raw string
    /// equality would over-claim every coincidental text answer).</summary>
    /// <summary>CF-FIX1-T9/T10: the ONE parent-value validation — BOTH meanings of
    /// ParentValueCode (mutually exclusive by construction):
    ///   • DEPENDENT list (list has ParentListCode): the parent value must exist in the
    ///     PARENT list (B3 — Selangor points at COUNTRY's MY).
    ///   • FLAT list (no ParentListCode): the parent must be another value of the SAME list
    ///     (B5 — a category tree). Self-parent and cycles (walked to the root) reject.</summary>
    private async Task ValidateParentValueAsync(CustomList list, string? parentValueCode, string? selfCode, CancellationToken ct)
    {
        if (parentValueCode is null) return;
        if (list.ParentListCode is { } plc)
        {
            var parentList = await db.CustomLists.AsNoTracking().Include(l => l.Values)
                .FirstOrDefaultAsync(l => l.Code == plc, ct)
                ?? throw new DomainRuleException($"Parent list '{plc}' does not exist.");
            if (!parentList.Values.Any(v => v.Code == parentValueCode))
                throw new DomainRuleException($"'{parentValueCode}' is not a value of the parent list {parentList.Name}.");
            return;
        }
        if (selfCode is not null && string.Equals(parentValueCode, selfCode, StringComparison.Ordinal))
            throw new DomainRuleException("A value cannot be its own parent.");
        var byCode = list.Values.ToDictionary(v => v.Code, v => v.ParentValueCode);
        if (!byCode.ContainsKey(parentValueCode))
            throw new DomainRuleException($"'{parentValueCode}' is not an existing value of this list — pick a previously-entered value.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var cursor = (string?)parentValueCode;
        while (cursor is not null)
        {
            if (selfCode is not null && cursor == selfCode)
                throw new DomainRuleException("That parent would create a cycle — a value cannot be its own ancestor.");
            if (!seen.Add(cursor))
                throw new DomainRuleException("That parent chain already contains a cycle — fix the list values first.");
            cursor = byCode.GetValueOrDefault(cursor);
        }
    }

    /// <summary>Values that point AT this code: same-list tree children (B5), or — when a
    /// child list depends on this one — the child list's values (B3).</summary>
    private async Task<bool> HasDependentChildrenAsync(CustomList list, string valueCode, CancellationToken ct)
    {
        if (await db.CustomListValues.AnyAsync(v => v.CustomListId == list.Id && v.ParentValueCode == valueCode, ct))
            return true;
        var childListIds = await db.CustomLists.Where(l => l.ParentListCode == list.Code).Select(l => l.Id).ToListAsync(ct);
        return childListIds.Count > 0
            && await db.CustomListValues.AnyAsync(v => childListIds.Contains(v.CustomListId) && v.ParentValueCode == valueCode, ct);
    }

    private async Task<bool> IsReferencedAsync(string listCode, string valueCode, CancellationToken ct)
    {
        // Custom-field values: defs bound to this list, storing this code (D5's ValueListCode).
        var defIds = await db.CustomFieldDefs.AsNoTracking()
            .Where(d => d.CustomListId != null && db.CustomLists.Any(l => l.Id == d.CustomListId && l.Code == listCode))
            .Select(d => d.Id).ToListAsync(ct);
        if (defIds.Count > 0 &&
            await db.CustomFieldValues.AsNoTracking().AnyAsync(v => defIds.Contains(v.FieldDefId) && v.ValueListCode == valueCode, ct))
            return true;

        return listCode switch
        {
            "COUNTRY" => await db.Vendors.AnyAsync(v => v.Country == valueCode || v.Addresses.Any(a => a.Country == valueCode), ct),
            "STATE" => await db.Vendors.AnyAsync(v => v.State == valueCode || v.Addresses.Any(a => a.State == valueCode), ct),
            "CITY" => await db.Vendors.AnyAsync(v => v.City == valueCode || v.Addresses.Any(a => a.City == valueCode), ct),
            "BANK" => await db.Vendors.AnyAsync(v => v.BankAccounts.Any(b => b.Bank == valueCode), ct),
            "CURRENCY" => await db.Vendors.AnyAsync(v => v.Currencies.Any(c => c.Code == valueCode), ct),
            "PAYMENT_TERMS" => await db.Vendors.AnyAsync(v => v.PaymentTerms == valueCode, ct),
            "RFQ_DECLINE_REASON" => await db.Rfqs.AnyAsync(r => r.Invitations.Any(i => i.DeclineReasonCode == valueCode), ct)
                                    || await db.RfqEvents.AnyAsync(e => e.ReasonCode == valueCode, ct),
            "RFQ_RESCIND_REASON" => await db.Rfqs.AnyAsync(r => r.Invitations.Any(i => i.RescindReasonCode == valueCode), ct)
                                    || await db.RfqEvents.AnyAsync(e => e.ReasonCode == valueCode, ct),
            "RFQ_EXTENSION_REASON" => await db.RfqEvents.AnyAsync(e => e.ReasonCode == valueCode, ct),
            _ => false,   // user-created lists: only the custom-field store can reference them
        };
    }

    private async Task<CustomList> LoadList(string code, CancellationToken ct) =>
        await db.CustomLists.Include(l => l.Values).FirstOrDefaultAsync(l => l.Code == code, ct)
        ?? throw new NotFoundException($"Custom list '{code}' not found.");

    private static CustomListDto ToDto(CustomList l) => new(l.Id, l.Code, l.Name, l.Description, l.ParentListCode, l.IsSystem,
        [.. l.Values.OrderBy(v => v.Sort).Select(ToDto)], l.OrderMode, l.Active);
    private static CustomListValueDto ToDto(CustomListValue v) => new(v.Id, v.Code, v.Label, v.ParentValueCode, v.Sort, v.Active);
}
