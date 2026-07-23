namespace FSH.Modules.Platform.Domain;

/// <summary>
/// One typed criterion row. Composition rule: rows sharing a <see cref="GroupIndex"/> &gt;= 1
/// OR together (membership within the group); everything else (GroupIndex 0) ANDs. Value2 is
/// only meaningful for <see cref="ViewOperator.Between"/>. Grain: one row per criterion member.
/// </summary>
public sealed class SavedViewFilter
{
    public Guid Id { get; private set; }
    public Guid SavedViewId { get; private set; }
    public string FieldKey { get; private set; } = default!;
    public ViewOperator Operator { get; private set; }
    public int GroupIndex { get; private set; }
    public string Value { get; private set; } = string.Empty;
    public string? Value2 { get; private set; }
    public int Sort { get; private set; }

    private SavedViewFilter() { }

    internal static SavedViewFilter Create(
        Guid savedViewId,
        string fieldKey,
        ViewOperator @operator,
        int groupIndex,
        string? value,
        string? value2,
        int sort)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldKey);
        return new SavedViewFilter
        {
            Id = Guid.CreateVersion7(),
            SavedViewId = savedViewId,
            FieldKey = fieldKey.Trim(),
            Operator = @operator,
            GroupIndex = groupIndex,
            Value = value ?? string.Empty,
            Value2 = string.IsNullOrWhiteSpace(value2) ? null : value2,
            Sort = sort,
        };
    }
}
