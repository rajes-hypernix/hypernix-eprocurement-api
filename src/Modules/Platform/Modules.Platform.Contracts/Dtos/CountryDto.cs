namespace FSH.Modules.Platform.Contracts.Dtos;

public sealed record CountryDto(Guid Id, string Code, string Name, bool IsActive, DateTimeOffset CreatedOnUtc);

public sealed record StateDto(Guid Id, Guid CountryId, string Code, string Name, bool IsActive, DateTimeOffset CreatedOnUtc);

public sealed record CityDto(Guid Id, Guid StateId, string Name, bool IsActive, DateTimeOffset CreatedOnUtc);

/// <summary>
/// Bank list/catalog row. Audit actor stamps are not included — open History (Auditing) on demand.
/// </summary>
public sealed record BankDto(
    Guid Id,
    string Name,
    string? SwiftCode,
    string CountryCode,
    bool IsActive,
    DateTimeOffset CreatedOnUtc);

public sealed record CustomListDto(Guid Id, string Key, string Name, bool IsActive, DateTimeOffset CreatedOnUtc);

public sealed record CustomListItemDto(
    Guid Id,
    Guid ListId,
    string Code,
    string Label,
    int SortOrder,
    bool IsActive,
    DateTimeOffset CreatedOnUtc);

public sealed record CityLookupDto(Guid Id, string Name);

public sealed record StateLookupDto(Guid Id, string Code, string Name, IReadOnlyList<CityLookupDto> Cities);

public sealed record CountryLookupDto(Guid Id, string Code, string Name, IReadOnlyList<StateLookupDto> States);

public sealed record GeoCatalogDto(
    IReadOnlyList<CountryLookupDto> Countries,
    IReadOnlyList<BankDto> Banks);

public sealed record OrgUnitDto(
    Guid Id,
    string Code,
    string Name,
    string Type,
    Guid? ParentId,
    bool IsActive,
    DateTimeOffset CreatedOnUtc);

public sealed record OrgCatalogDto(IReadOnlyList<OrgUnitDto> Units);

public sealed record FormTemplateQuestionDto(
    Guid Id,
    int Order,
    string Label,
    string Type,
    bool Required,
    string? ConfigJson,
    string? Help);

public sealed record FormTemplateDto(
    Guid Id,
    string Key,
    string Name,
    bool IsActive,
    IReadOnlyList<FormTemplateQuestionDto> Questions);

public sealed record FormTemplateListItemDto(Guid Id, string Key, string Name, bool IsActive, int QuestionCount);
