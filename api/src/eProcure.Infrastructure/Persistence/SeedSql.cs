using System.Globalization;
using System.Text;

namespace eProcure.Infrastructure.Persistence;

/// <summary>
/// Idempotent INSERT builder for migrations that seed FROM a live code list (FieldRegistrySeed,
/// DashboardSeed — the ruled single sources). Those lists GROW in later slices, so an older
/// migration replayed on a fresh database would insert rows a newer migration also inserts:
/// ON CONFLICT ("Id") DO NOTHING makes every such seed replay-safe (deterministic ids make the
/// conflict target exact). Surfaced by D4 Phase 3's seed append breaking CI's fresh-DB migrate.
/// </summary>
internal static class SeedSql
{
    public static string InsertDoNothing(string table, string[] columns, object?[] values)
    {
        var sb = new StringBuilder();
        sb.Append("INSERT INTO \"").Append(table).Append("\" (");
        sb.Append(string.Join(", ", columns.Select(c => $"\"{c}\"")));
        sb.Append(") VALUES (");
        sb.Append(string.Join(", ", values.Select(Literal)));
        sb.Append(") ON CONFLICT (\"Id\") DO NOTHING;");
        return sb.ToString();
    }

    private static string Literal(object? v) => v switch
    {
        null => "NULL",
        bool b => b ? "TRUE" : "FALSE",
        Guid g => $"'{g}'",
        short s => s.ToString(CultureInfo.InvariantCulture),
        int i => i.ToString(CultureInfo.InvariantCulture),
        DateTime dt => $"TIMESTAMPTZ '{dt:yyyy-MM-dd HH:mm:ssK}'",
        string s => $"'{s.Replace("'", "''")}'",
        _ => throw new InvalidOperationException($"SeedSql cannot render a {v.GetType().Name} literal."),
    };
}
