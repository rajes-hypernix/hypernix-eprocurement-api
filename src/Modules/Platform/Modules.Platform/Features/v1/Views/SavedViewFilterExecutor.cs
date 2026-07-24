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
    /// <summary>Phase 7 tokens: <c>@me</c> resolves server-side to the caller's own user id (e.g.
    /// <c>OwnerUserId Eq @me</c> for "my RFQs"); <c>@empty</c> resolves to an empty string, letting a
    /// value-carrying operator (Eq/Neq) express "blank" without needing IsEmpty/IsNotEmpty. Resolved
    /// once per filter, before <see cref="Matches"/> ever sees the value, so every operator gets it
    /// for free with no per-operator token awareness.</summary>
    private const string CurrentUserToken = "@me";
    private const string EmptyValueToken = "@empty";

    public static List<IDictionary<string, object?>> Apply(
        IReadOnlyList<IDictionary<string, object?>> rows,
        IReadOnlyList<SavedViewFilter> filters,
        string currentUserId)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(filters);
        if (filters.Count == 0)
            return [.. rows];

        var groups = filters.GroupBy(f => f.GroupIndex).ToList();
        return [.. rows.Where(row => groups.All(g => g.Key >= 1
            ? g.Any(f => Matches(row, f, currentUserId))
            : g.All(f => Matches(row, f, currentUserId))))];
    }

    private static string? ResolveToken(string? value, string currentUserId) => value switch
    {
        CurrentUserToken => currentUserId,
        EmptyValueToken => string.Empty,
        _ => value,
    };

    private static bool Matches(IDictionary<string, object?> row, SavedViewFilter filter, string currentUserId)
    {
        row.TryGetValue(filter.FieldKey, out var raw);
        string value = ResolveToken(filter.Value, currentUserId) ?? string.Empty;
        string? value2 = ResolveToken(filter.Value2, currentUserId);
        return filter.Operator switch
        {
            ViewOperator.Eq => AreEqual(raw, value),
            ViewOperator.Neq => raw is not null && !AreEqual(raw, value),
            ViewOperator.In => SplitValues(value).Any(v => AreEqual(raw, v)),
            ViewOperator.Contains => ToText(raw).Contains(value, StringComparison.OrdinalIgnoreCase),
            ViewOperator.StartsWith => ToText(raw).StartsWith(value, StringComparison.OrdinalIgnoreCase),
            ViewOperator.IsEmpty => IsEmpty(raw),
            ViewOperator.IsNotEmpty => !IsEmpty(raw),
            ViewOperator.Gte => Compare(raw, value) is { } c1 && c1 >= 0,
            ViewOperator.Lte => Compare(raw, value) is { } c2 && c2 <= 0,
            ViewOperator.Between => Compare(raw, value) is { } lo && lo >= 0
                && Compare(raw, value2 ?? value) is { } hi && hi <= 0,
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
