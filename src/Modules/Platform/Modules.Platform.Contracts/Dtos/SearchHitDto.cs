using FSH.Modules.Platform.Contracts.v1.Search;

namespace FSH.Modules.Platform.Contracts.Dtos;

public sealed record SearchHitDto(
    SearchHitType Type,
    Guid Id,
    string Code,
    string Title,
    string? Subtitle = null);
