using System.Globalization;
using FSH.Modules.Platform.Domain;

namespace FSH.Modules.Platform.Features.v1.Views;

/// <summary>
/// Wave-1 in-memory filter executor: rows are loaded whole from the row source, then filtered
/// here. Rows sharing a <see cref="SavedViewFilter.GroupIndex"/> &gt;= 1 OR together (membership);
/// everything else (group 0) ANDs; groups AND each other.
/// </summary>
internal static class SavedViewFilterExecutor
{
    public static List<IDictionary<string, object?>> Apply(
        IReadOnlyList<IDictionary<string, object?>> rows,
        IReadOnlyList<SavedViewFilter> filters)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(filters);
        if (filters.Count == 0)
            return [.. rows];

        var groups = filters.GroupBy(f => f.GroupIndex).ToList();
        return [.. rows.Where(row => groups.All(g => g.Key >= 1
            ? g.Any(f => Matches(row, f))
            : g.All(f => Matches(row, f))))];
    }

    private static bool Matches(IDictionary<string, object?> row, SavedViewFilter filter)
    {
        row.TryGetValue(filter.FieldKey, out var raw);
        return filter.Operator switch
        {
            ViewOperator.Eq => AreEqual(raw, filter.Value),
            ViewOperator.Neq => raw is not null && !AreEqual(raw, filter.Value),
            ViewOperator.In => SplitValues(filter.Value).Any(v => AreEqual(raw, v)),
            ViewOperator.Contains => ToText(raw).Contains(filter.Value, StringComparison.OrdinalIgnoreCase),
            ViewOperator.StartsWith => ToText(raw).StartsWith(filter.Value, StringComparison.OrdinalIgnoreCase),
            ViewOperator.IsEmpty => IsEmpty(raw),
            ViewOperator.IsNotEmpty => !IsEmpty(raw),
            ViewOperator.Gte => Compare(raw, filter.Value) is { } c1 && c1 >= 0,
            ViewOperator.Lte => Compare(raw, filter.Value) is { } c2 && c2 <= 0,
            ViewOperator.Between => Compare(raw, filter.Value) is { } lo && lo >= 0
                && Compare(raw, filter.Value2 ?? filter.Value) is { } hi && hi <= 0,
            _ => false,
        };
    }

    private static IEnumerable<string> SplitValues(string value) =>
        value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    private static bool IsEmpty(object? raw) =>
        raw is null || (raw is string s && string.IsNullOrWhiteSpace(s));

    private static string ToText(object? raw) => raw?.ToString() ?? string.Empty;

    private static bool AreEqual(object? raw, string value)
    {
        if (raw is null)
            return string.IsNullOrEmpty(value);

        return raw switch
        {
            bool b => bool.TryParse(value, out var bv) && b == bv,
            DateTime dt => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dtv) && dt == dtv,
            DateOnly d => DateOnly.TryParse(value, CultureInfo.InvariantCulture, out var dv) && d == dv,
            decimal or double or float or int or long => decimal.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, out var rawNum)
                && decimal.TryParse(value, CultureInfo.InvariantCulture, out var valNum) && rawNum == valNum,
            _ => string.Equals(raw.ToString(), value, StringComparison.OrdinalIgnoreCase),
        };
    }

    /// <summary>Type-aware ordering compare; null when either side can't be parsed (never matches Gte/Lte/Between).</summary>
    private static int? Compare(object? raw, string value) =>
        raw switch
        {
            null => null,
            DateTime dt when DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dtv) => dt.CompareTo(dtv),
            DateOnly d when DateOnly.TryParse(value, CultureInfo.InvariantCulture, out var dv) => d.CompareTo(dv),
            decimal or double or float or int or long
                when decimal.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, out var rawNum)
                    && decimal.TryParse(value, CultureInfo.InvariantCulture, out var valNum) => rawNum.CompareTo(valNum),
            string s => string.Compare(s, value, StringComparison.OrdinalIgnoreCase),
            _ => null,
        };
}
