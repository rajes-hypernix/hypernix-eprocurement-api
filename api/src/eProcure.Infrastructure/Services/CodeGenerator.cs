using eProcure.Application.Abstractions;
using eProcure.Domain;
using eProcure.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eProcure.Infrastructure.Services;

/// <summary>
/// Sequence-backed code generator. Produces "{PREFIX}-{YEAR}-{0000}" from a
/// per-(prefix, year) counter row, so codes are deterministic and gap-free per
/// type rather than length- or random-based (BUSINESS-RULES [$]).
/// </summary>
public sealed class CodeGenerator(AppDbContext db, IClock clock) : ICodeGenerator
{
    public async Task<string> NextAsync(string prefix, CancellationToken ct = default)
    {
        var p = prefix.ToUpperInvariant();
        var year = clock.UtcNow.Year;

        var seq = await db.NumberSequences
            .FirstOrDefaultAsync(s => s.Prefix == p && s.Year == year, ct);

        if (seq is null)
        {
            seq = new NumberSequence(p, year);
            db.NumberSequences.Add(seq);
        }

        var value = seq.Next();
        await db.SaveChangesAsync(ct);

        return $"{p}-{year}-{value:D4}";
    }
}
