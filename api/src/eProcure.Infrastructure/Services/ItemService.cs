using eProcure.Application;
using eProcure.Application.Abstractions;
using eProcure.Application.Procurement;
using eProcure.Domain;
using eProcure.Domain.Procurement;
using eProcure.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eProcure.Infrastructure.Services;

/// <summary>
/// CFH-T4: the Item Master service — a pure lookup source. Lines across the app store ItemCode as
/// a plain string (no FK), so items hard-delete without orphaning anything. ItemCode is the unique
/// business key (case-insensitive uniqueness enforced here; a DB unique index backs it).
/// </summary>
public sealed class ItemService(AppDbContext db, IClock clock) : IItemService
{
    public async Task<IReadOnlyList<ItemDto>> ListAsync(bool activeOnly = false, CancellationToken ct = default)
    {
        var q = db.Items.AsNoTracking();
        if (activeOnly) q = q.Where(i => i.Active);
        var items = await q.OrderBy(i => i.ItemCode).ToListAsync(ct);
        return [.. items.Select(ToDto)];
    }

    public async Task<ItemDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var item = await db.Items.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, ct);
        return item is null ? null : ToDto(item);
    }

    public async Task<ItemDto> CreateAsync(SaveItemRequest req, CancellationToken ct = default)
    {
        var code = req.ItemCode?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(code))
            throw new DomainRuleException("An item needs an Item Code.");
        var description = req.Description?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(description))
            throw new DomainRuleException("An item needs a Description.");
        if (await db.Items.AnyAsync(i => i.ItemCode.ToLower() == code.ToLower(), ct))
            throw new DomainRuleException($"An item with code '{code}' already exists.");

        var uom = string.IsNullOrWhiteSpace(req.Uom) ? "Unit" : req.Uom.Trim();
        var now = clock.UtcNow;
        var item = new Item
        {
            ItemCode = code,
            Description = description,
            Uom = uom,
            Active = true,
            CreatedUtc = now,
            UpdatedUtc = now,
        };
        db.Items.Add(item);
        await db.SaveChangesAsync(ct);
        return ToDto(item);
    }

    public async Task<ItemDto> UpdateAsync(Guid id, SaveItemRequest req, CancellationToken ct = default)
    {
        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new NotFoundException($"Item {id} not found.");
        var code = req.ItemCode?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(code))
            throw new DomainRuleException("An item needs an Item Code.");
        var description = req.Description?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(description))
            throw new DomainRuleException("An item needs a Description.");
        if (!string.Equals(code, item.ItemCode, StringComparison.OrdinalIgnoreCase)
            && await db.Items.AnyAsync(i => i.Id != id && i.ItemCode.ToLower() == code.ToLower(), ct))
            throw new DomainRuleException($"An item with code '{code}' already exists.");

        item.ItemCode = code;
        item.Description = description;
        item.Uom = string.IsNullOrWhiteSpace(req.Uom) ? "Unit" : req.Uom.Trim();
        item.UpdatedUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToDto(item);
    }

    public async Task<ItemDto> SetActiveAsync(Guid id, bool active, CancellationToken ct = default)
    {
        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new NotFoundException($"Item {id} not found.");
        item.Active = active;
        item.UpdatedUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToDto(item);
    }

    /// <summary>Hard delete — items are a pure lookup source; lines store ItemCode as a string,
    /// so there is no FK and deletion never orphans a record.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new NotFoundException($"Item {id} not found.");
        db.Items.Remove(item);
        await db.SaveChangesAsync(ct);
    }

    private static ItemDto ToDto(Item i) => new(i.Id, i.ItemCode, i.Description, i.Uom, i.Active);
}
