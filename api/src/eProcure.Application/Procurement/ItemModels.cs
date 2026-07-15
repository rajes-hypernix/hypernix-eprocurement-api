namespace eProcure.Application.Procurement;

public sealed record ItemDto(Guid Id, string ItemCode, string Description, string Uom, bool Active);
public sealed record SaveItemRequest(string ItemCode, string Description, string Uom);

/// <summary>
/// CFH-T4: the Item Master — a small reusable lookup source. Lines store ItemCode as a plain
/// string (no FK, no re-keying); the entry form picks from this master and auto-fills
/// Description + UoM. ItemCode is the unique business key; deletion never orphans a line.
/// </summary>
public interface IItemService
{
    Task<IReadOnlyList<ItemDto>> ListAsync(bool activeOnly = false, CancellationToken ct = default);
    Task<ItemDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<ItemDto> CreateAsync(SaveItemRequest req, CancellationToken ct = default);
    Task<ItemDto> UpdateAsync(Guid id, SaveItemRequest req, CancellationToken ct = default);
    Task<ItemDto> SetActiveAsync(Guid id, bool active, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
