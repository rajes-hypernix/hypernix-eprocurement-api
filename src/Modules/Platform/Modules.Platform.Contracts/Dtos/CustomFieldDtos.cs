namespace FSH.Modules.Platform.Contracts.Dtos;

public sealed record CustomFieldDefDto(
    Guid Id,
    string Code,
    string Label,
    string DataType,
    string? RefEntity,
    string? ListKey,
    string Scope,
    string DisplayType,
    bool ShowInList,
    bool IsRequired,
    string? HelpText,
    bool IsActive,
    IReadOnlyList<string> AppliesTo);

/// <summary>One field's definition metadata joined with its (possibly absent) value for one record instance.</summary>
public sealed record CustomFieldValueDto(
    Guid CustomFieldDefId,
    string Code,
    string Label,
    string DataType,
    string DisplayType,
    bool IsRequired,
    string? ListKey,
    string? RefEntity,
    Guid? LineId,
    string? ValueText,
    decimal? ValueNumber,
    DateOnly? ValueDate,
    DateTime? ValueDateTime,
    bool? ValueBool,
    string? ValueListCode,
    Guid? ValueRefId,
    string? ValueLabel);

public sealed record CustomFieldValueInput(
    Guid CustomFieldDefId,
    Guid? LineId,
    string? ValueText,
    decimal? ValueNumber,
    DateOnly? ValueDate,
    DateTime? ValueDateTime,
    bool? ValueBool,
    string? ValueListCode,
    Guid? ValueRefId,
    string? ValueLabel);
