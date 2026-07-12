using eProcure.Application;
using eProcure.Application.Abstractions;
using eProcure.Application.Configuration;
using eProcure.Domain;
using eProcure.Domain.Configuration;
using eProcure.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eProcure.Infrastructure.Services;

public sealed class CustomListService(AppDbContext db, IClock clock) : ICustomListService
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
        var code = req.Code.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code)) throw new DomainRuleException("A custom list needs a code.");
        if (await db.CustomLists.AnyAsync(l => l.Code == code, ct))
            throw new DomainRuleException($"A custom list with code '{code}' already exists.");

        var now = clock.UtcNow;
        var list = new CustomList { Code = code, Name = req.Name.Trim(), Description = req.Description, ParentListCode = req.ParentListCode, IsSystem = false, CreatedUtc = now, UpdatedUtc = now };
        db.CustomLists.Add(list);
        await db.SaveChangesAsync(ct);
        return ToDto(list);
    }

    public async Task<CustomListValueDto> AddValueAsync(string listCode, AddCustomListValueRequest req, CancellationToken ct = default)
    {
        var list = await LoadList(listCode, ct);
        var code = req.Code.Trim();
        if (string.IsNullOrWhiteSpace(code)) throw new DomainRuleException("A list value needs a code.");
        if (list.Values.Any(v => v.Code == code)) throw new DomainRuleException($"Value '{code}' already exists in {list.Name}.");

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

        // A2F-T3 (GAP-5): the "never silently drop a referenced value" discipline (the D5
        // custom-field-def rule, the D6 unapply rule). A referenced value DEACTIVATES —
        // gone from new-entry options (lookups filter Active), still resolving for display
        // of the records that hold its code. Only an unreferenced value hard-deletes.
        if (await IsReferencedAsync(list.Code, value.Code, ct))
        {
            value.Active = false;
            await db.SaveChangesAsync(ct);
            return ToDto(value);
        }

        db.CustomListValues.Remove(value);
        await db.SaveChangesAsync(ct);
        return null;
    }

    /// <summary>Every store a list value's CODE can live in — the universal custom-field
    /// probe plus, per SYSTEM list, the native code-holding columns it sources. A probe
    /// that over-matches merely deactivates (conservative + reversible via UpdateValueAsync);
    /// a miss would strand a dangling code, so new native list-sourced columns must be
    /// added here. Onboarding questionnaire ANSWERS are deliberately not probed: they store
    /// free values per question order, not typed list references (matching on raw string
    /// equality would over-claim every coincidental text answer).</summary>
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
        [.. l.Values.OrderBy(v => v.Sort).Select(ToDto)]);
    private static CustomListValueDto ToDto(CustomListValue v) => new(v.Id, v.Code, v.Label, v.ParentValueCode, v.Sort, v.Active);
}
