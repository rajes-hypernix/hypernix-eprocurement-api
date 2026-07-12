using eProcure.Application.Abstractions;
using eProcure.Domain;
using eProcure.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eProcure.Infrastructure.Services;

/// <summary>
/// Sequence-backed code generator. Produces "{PREFIX}-{YEAR}-{0000}" from a per-(prefix, year)
/// counter row, so codes are deterministic and gap-free per type (BUSINESS-RULES [$]).
/// </summary>
public sealed class CodeGenerator(AppDbContext db, IClock clock) : ICodeGenerator
{
    public async Task<string> NextAsync(string prefix, CancellationToken ct = default)
    {
        var year = clock.UtcNow.Year;
        var value = await NextValueAsync(prefix.ToUpperInvariant(), year, ct);
        return $"{prefix.ToUpperInvariant()}-{year}-{value:D4}";
    }

    /// <summary>D7 (OD-D7-6): scheme-consulting mint for the seven registry record types.
    /// The scheme shapes the FORMAT only; the sequence machinery is byte-identical.
    /// YearSegment=false buckets the sequence under year 0 (one continuous counter), so
    /// year-less codes cannot collide across years by construction. Sequences are keyed
    /// (prefix, bucket) and never reset — a prefix change starts (or REATTACHES to) its
    /// own counter, so history is never re-issued.</summary>
    public async Task<string> NextAsync(Domain.Views.RecordType type, CancellationToken ct = default)
    {
        var scheme = await db.NumberingSchemes.AsNoTracking().SingleAsync(s => s.RecordType == type, ct);
        var year = clock.UtcNow.Year;
        var bucket = scheme.YearSegment ? year : 0;
        var value = await NextValueAsync(scheme.Prefix.ToUpperInvariant(), bucket, ct);
        var padded = value.ToString($"D{scheme.Digits}");
        return scheme.YearSegment ? $"{scheme.Prefix.ToUpperInvariant()}-{year}-{padded}" : $"{scheme.Prefix.ToUpperInvariant()}-{padded}";
    }

    private async Task<int> NextValueAsync(string p, int year, CancellationToken ct)
    {
        int value;

        if (db.Database.IsNpgsql())
        {
            // PRG-2: read-increment-save was racy — two concurrent callers could read the same counter
            // and mint the same code. Replace it with one atomic, row-locked upsert: the ON CONFLICT
            // DO UPDATE takes a row lock on the (prefix, year) NumberSequences row, so concurrent calls
            // serialise and each gets a distinct, gap-free value. This is row-level locking on that row
            // (the SELECT ... FOR UPDATE guarantee, in a single statement) — NOT a native sequence,
            // which would permit gaps.
            // ToList (not Single) so EF runs the DML-with-RETURNING as-is — INSERT..RETURNING is not a
            // composable SELECT, so a server-side Single()/LIMIT wrapper is rejected.
            value = (await db.Database.SqlQuery<int>(
                $@"INSERT INTO ""NumberSequences"" (""Prefix"", ""Year"", ""LastValue"") VALUES ({p}, {year}, 1)
                   ON CONFLICT (""Prefix"", ""Year"") DO UPDATE SET ""LastValue"" = ""NumberSequences"".""LastValue"" + 1
                   RETURNING ""LastValue"" AS ""Value""").ToListAsync(ct)).Single();
        }
        else
        {
            // In-memory provider (unit tests) can't run raw SQL and is single-threaded, so the simple
            // read-increment-save is safe there.
            var seq = await db.NumberSequences.FirstOrDefaultAsync(s => s.Prefix == p && s.Year == year, ct);
            if (seq is null)
            {
                seq = new NumberSequence(p, year);
                db.NumberSequences.Add(seq);
            }
            value = seq.Next();
            await db.SaveChangesAsync(ct);
        }

        return value;
    }
}
