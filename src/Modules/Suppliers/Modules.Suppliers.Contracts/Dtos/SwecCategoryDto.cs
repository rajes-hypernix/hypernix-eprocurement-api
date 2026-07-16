namespace FSH.Modules.Suppliers.Contracts.Dtos;

public sealed record SwecCategoryDto(
    string Code,
    string Name,
    string? ParentCode,
    int Level,
    bool IsLeaf,
    string PathText);
