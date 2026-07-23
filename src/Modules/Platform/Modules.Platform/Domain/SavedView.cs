using FSH.Framework.Core.Domain;

namespace FSH.Modules.Platform.Domain;

/// <summary>Filter/column shape used when (re)building a view's definition — kept in the
/// Domain namespace so <see cref="SavedView"/> doesn't need to depend on the Contracts DTOs.</summary>
public sealed record SavedViewFilterInput(string FieldKey, ViewOperator Operator, int GroupIndex, string? Value, string? Value2, int Sort);

public sealed record SavedViewColumnInput(string FieldKey, string? Label, int Sort, ViewSortDirection? SortDirection);

/// <summary>
/// A saved query definition: filters + columns over one record type. No filter blobs —
/// every criterion is a typed row. Grain: one row per view.
/// </summary>
public sealed class SavedView : AggregateRoot<Guid>
{
    private readonly List<SavedViewFilter> _filters = [];
    private readonly List<SavedViewColumn> _columns = [];

    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public ViewRecordType RecordType { get; private set; }

    /// <summary>Null for system views; otherwise the creating principal's user id.</summary>
    public string? OwnerUserId { get; private set; }

    public bool IsShared { get; private set; }
    public bool IsSystem { get; private set; }
    public DateTime CreatedUtc { get; private set; }
    public DateTime UpdatedUtc { get; private set; }

    public IReadOnlyList<SavedViewFilter> Filters => _filters;
    public IReadOnlyList<SavedViewColumn> Columns => _columns;

    private SavedView() { }

    public static SavedView Create(
        string code,
        string name,
        ViewRecordType recordType,
        string? ownerUserId,
        bool isShared,
        bool isSystem,
        IReadOnlyList<SavedViewFilterInput> filters,
        IReadOnlyList<SavedViewColumnInput> columns)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var now = DateTime.UtcNow;
        var view = new SavedView
        {
            Id = Guid.CreateVersion7(),
            Code = code.Trim(),
            Name = name.Trim(),
            RecordType = recordType,
            OwnerUserId = ownerUserId,
            IsShared = isShared,
            IsSystem = isSystem,
            CreatedUtc = now,
            UpdatedUtc = now,
        };
        view.ReplaceFilters(filters);
        view.ReplaceColumns(columns);
        return view;
    }

    public void UpdateDefinition(string name, IReadOnlyList<SavedViewFilterInput> filters, IReadOnlyList<SavedViewColumnInput> columns)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        ReplaceFilters(filters);
        ReplaceColumns(columns);
        UpdatedUtc = DateTime.UtcNow;
    }

    public void SetShared(bool isShared)
    {
        IsShared = isShared;
        UpdatedUtc = DateTime.UtcNow;
    }

    private void ReplaceFilters(IReadOnlyList<SavedViewFilterInput> filters)
    {
        ArgumentNullException.ThrowIfNull(filters);
        _filters.Clear();
        foreach (var f in filters)
            _filters.Add(SavedViewFilter.Create(Id, f.FieldKey, f.Operator, f.GroupIndex, f.Value, f.Value2, f.Sort));
    }

    private void ReplaceColumns(IReadOnlyList<SavedViewColumnInput> columns)
    {
        ArgumentNullException.ThrowIfNull(columns);
        _columns.Clear();
        foreach (var c in columns)
            _columns.Add(SavedViewColumn.Create(Id, c.FieldKey, c.Label, c.Sort, c.SortDirection));
    }
}
