namespace eProcure.Application.Views;

/// <summary>An invalid view definition (unknown FieldKey, operator/DataType mismatch,
/// unparsable value) → HTTP 400 per the D3 ruling — loud on save AND on run, never a
/// silently dropped filter.</summary>
public sealed class ViewValidationException(string message) : Exception(message);

public sealed record SavedViewFilterDto(string FieldKey, string Operator, string Value, string? Value2);

public sealed record SavedViewColumnDto(string FieldKey, string? Label, string? SortDirection);

public sealed record SavedViewDto(
    Guid Id, string Code, string Name, string RecordType, string? OwnerUserId, bool IsShared, bool IsSystem,
    IReadOnlyList<SavedViewFilterDto> Filters, IReadOnlyList<SavedViewColumnDto> Columns);

public sealed record SaveViewRequest(
    string Name, string RecordType, List<SavedViewFilterDto> Filters, List<SavedViewColumnDto> Columns);

public sealed record ShareViewRequest(bool IsShared);

/// <summary>A registry field as the ViewBuilder consumes it: DataType picks the D1 value
/// primitive; Options populate the select for Enum fields (from ViewVocabulary, code not blobs).</summary>
public sealed record ViewFieldDto(string FieldKey, string Label, string DataType, IReadOnlyList<string>? Options);

public sealed record ViewRunColumn(string FieldKey, string Label, string DataType);

/// <summary>Typed rows shaped by the view's columns (+ implicit Id for navigation/actions).
/// Rows only — aggregation is D4's documented seam; nothing built here.</summary>
public sealed record ViewRunResult(
    Guid ViewId, string Name, string RecordType,
    IReadOnlyList<ViewRunColumn> Columns, IReadOnlyList<Dictionary<string, object?>> Rows);

public interface ISavedViewService
{
    Task<IReadOnlyList<SavedViewDto>> ListVisibleAsync(string? recordType, CancellationToken ct = default);
    Task<IReadOnlyList<ViewFieldDto>> FieldsAsync(string recordType, CancellationToken ct = default);
    Task<SavedViewDto> CreateAsync(SaveViewRequest req, CancellationToken ct = default);
    Task<SavedViewDto> UpdateAsync(Guid id, SaveViewRequest req, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<SavedViewDto> ShareAsync(Guid id, bool isShared, CancellationToken ct = default);
    Task<ViewRunResult> RunAsync(Guid id, CancellationToken ct = default);
    // D4 aggregation seam — same visibility, same View* check, same scoped sources as the run.
    Task<Dashboards.ViewAggregateResult> AggregateAsync(Guid id, string fn, string? fieldKey, CancellationToken ct = default);
    Task<Dashboards.ViewSeriesResult> SeriesAsync(Guid id, string fn, string? fieldKey, string bucketField, int months, CancellationToken ct = default);
}
