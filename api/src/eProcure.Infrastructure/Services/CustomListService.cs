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

    public async Task DeleteValueAsync(Guid valueId, CancellationToken ct = default)
    {
        var value = await db.CustomListValues.FirstOrDefaultAsync(v => v.Id == valueId, ct)
            ?? throw new NotFoundException($"List value {valueId} not found.");
        db.CustomListValues.Remove(value);
        await db.SaveChangesAsync(ct);
    }

    private async Task<CustomList> LoadList(string code, CancellationToken ct) =>
        await db.CustomLists.Include(l => l.Values).FirstOrDefaultAsync(l => l.Code == code, ct)
        ?? throw new NotFoundException($"Custom list '{code}' not found.");

    private static CustomListDto ToDto(CustomList l) => new(l.Id, l.Code, l.Name, l.Description, l.ParentListCode, l.IsSystem,
        [.. l.Values.OrderBy(v => v.Sort).Select(ToDto)]);
    private static CustomListValueDto ToDto(CustomListValue v) => new(v.Id, v.Code, v.Label, v.ParentValueCode, v.Sort, v.Active);
}
