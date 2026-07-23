namespace FSH.Modules.Platform.Contracts.Dtos;

public sealed record SavedViewFilterDto(
    string FieldKey,
    string Operator,
    int GroupIndex,
    string? Value,
    string? Value2,
    int Sort);

public sealed record SavedViewColumnDto(
    string FieldKey,
    string? Label,
    int Sort,
    string? SortDirection);

public sealed record SavedViewDto(
    Guid Id,
    string Code,
    string Name,
    string RecordType,
    string? OwnerUserId,
    bool IsShared,
    bool IsSystem,
    IReadOnlyList<SavedViewFilterDto> Filters,
    IReadOnlyList<SavedViewColumnDto> Columns,
    DateTimeOffset CreatedOnUtc,
    DateTimeOffset? LastModifiedOnUtc);

/// <summary>One row in a record type's field palette — registry entry shaped for the ViewBuilder.</summary>
public sealed record ViewFieldDto(string FieldKey, string Kind, string Label, string DataType);

/// <summary>Rows are keyed by field key (PascalCase), always including "Id" for client-side row identity.</summary>
public sealed record ViewRunResult(
    IReadOnlyList<IDictionary<string, object?>> Rows,
    int TotalCount,
    int Page,
    int Size);

public sealed record SaveViewRequest(
    string Name,
    string RecordType,
    IReadOnlyList<SavedViewFilterDto> Filters,
    IReadOnlyList<SavedViewColumnDto> Columns);
