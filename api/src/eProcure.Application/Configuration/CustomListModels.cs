namespace eProcure.Application.Configuration;

public sealed record CustomListValueDto(Guid Id, string Code, string Label, string? ParentValueCode, int Sort, bool Active);

public sealed record CustomListDto(
    Guid Id, string Code, string Name, string? Description, string? ParentListCode, bool IsSystem,
    IReadOnlyList<CustomListValueDto> Values);

public sealed record CreateCustomListRequest(string Code, string Name, string? Description, string? ParentListCode);
public sealed record AddCustomListValueRequest(string Code, string Label, string? ParentValueCode);
public sealed record UpdateCustomListValueRequest(string Label, string? ParentValueCode, int Sort, bool Active);

/// <summary>
/// Custom Lists (NetSuite-style): the reusable coded value sets any field is tagged to. Serves the
/// lists to the forms and lets an admin maintain values without a code change (data-driven).
/// </summary>
public interface ICustomListService
{
    Task<IReadOnlyList<CustomListDto>> ListAsync(CancellationToken ct = default);
    Task<CustomListDto?> GetAsync(string code, CancellationToken ct = default);
    Task<CustomListDto> CreateListAsync(CreateCustomListRequest req, CancellationToken ct = default);
    Task<CustomListValueDto> AddValueAsync(string listCode, AddCustomListValueRequest req, CancellationToken ct = default);
    Task<CustomListValueDto> UpdateValueAsync(Guid valueId, UpdateCustomListValueRequest req, CancellationToken ct = default);
    Task DeleteValueAsync(Guid valueId, CancellationToken ct = default);
}
