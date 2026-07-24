namespace FSH.Modules.Platform.Contracts.Dtos;

public sealed record EntryFormListItemDto(Guid Id, string Code, string Name, string RecordType, bool IsSystem, bool IsActive);

public sealed record EntryFormGroupDto(Guid Id, string Title, int Sort);

public sealed record EntryFormFieldDto(Guid Id, string FieldKey, Guid GroupId, int Sort, bool RequiredOnForm, bool FullWidth);

public sealed record EntryFormDetailDto(
    Guid Id,
    string Code,
    string Name,
    string RecordType,
    bool IsSystem,
    bool IsActive,
    IReadOnlyList<EntryFormGroupDto> Groups,
    IReadOnlyList<EntryFormFieldDto> Fields);

/// <summary>A field placement resolved with its registry metadata folded in, ready to render — the
/// dynamic-form-rendering read model consumed by <c>GetEntryFormForRoleQuery</c>.</summary>
public sealed record ResolvedFormFieldDto(
    string FieldKey,
    string GroupTitle,
    int GroupSort,
    int Sort,
    bool RequiredOnForm,
    bool FullWidth,
    string Label,
    string DataType,
    string? ListKey,
    string? RefEntity,
    bool IsCustomField,
    Guid? CustomFieldDefId);

public sealed record ResolvedEntryFormDto(Guid EntryFormDefId, string Code, string Name, IReadOnlyList<ResolvedFormFieldDto> Fields);

public sealed record EntryFormRoleMapDto(Guid Id, string RecordType, string Role, Guid EntryFormDefId, string EntryFormCode);
