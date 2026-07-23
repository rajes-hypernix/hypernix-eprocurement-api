namespace FSH.Modules.Platform.Domain;

/// <summary>One output column of the view. Grain: one row per column position.</summary>
public sealed class SavedViewColumn
{
    public Guid Id { get; private set; }
    public Guid SavedViewId { get; private set; }
    public string FieldKey { get; private set; } = default!;
    public string? Label { get; private set; }
    public int Sort { get; private set; }
    public ViewSortDirection? SortDirection { get; private set; }

    private SavedViewColumn() { }

    internal static SavedViewColumn Create(
        Guid savedViewId,
        string fieldKey,
        string? label,
        int sort,
        ViewSortDirection? sortDirection)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldKey);
        return new SavedViewColumn
        {
            Id = Guid.CreateVersion7(),
            SavedViewId = savedViewId,
            FieldKey = fieldKey.Trim(),
            Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim(),
            Sort = sort,
            SortDirection = sortDirection,
        };
    }
}
