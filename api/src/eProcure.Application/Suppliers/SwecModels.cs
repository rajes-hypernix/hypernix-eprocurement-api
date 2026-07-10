namespace eProcure.Application.Suppliers;

public sealed record SwecCategoryDto(
    string Code,
    string Name,
    string? ParentCode,
    int Level,
    bool IsLeaf,
    string PathText);

public interface ISwecService
{
    Task<IReadOnlyList<SwecCategoryDto>> ListAsync(CancellationToken ct = default);
}
