using System.Text.RegularExpressions;
using eProcure.Application.Abstractions;
using eProcure.Application.Forms;
using eProcure.Domain.Views;
using eProcure.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eProcure.Infrastructure.Services;

/// <summary>
/// Numbering as Setup configuration (D7, A70). Schemes shape the FORMAT at mint time
/// only — history is untouched and, because sequences are keyed (prefix, bucket) and
/// never reset, a format change can never re-issue an existing code (OD-D7-6, pinned
/// by NumberingTests). NextPreview peeks the counter WITHOUT consuming it.
/// </summary>
public sealed partial class NumberingService(AppDbContext db, IClock clock) : INumberingService
{
    [GeneratedRegex("^[A-Z0-9][A-Z0-9-]{0,11}$")]
    private static partial Regex PrefixShape();

    public async Task<IReadOnlyList<NumberingSchemeDto>> ListAsync(CancellationToken ct = default)
    {
        var schemes = await db.NumberingSchemes.AsNoTracking().ToListAsync(ct);
        var result = new List<NumberingSchemeDto>(schemes.Count);
        foreach (var s in schemes.OrderBy(s => s.RecordType.ToString()))
            result.Add(await ToDtoAsync(s, ct));
        return result;
    }

    public async Task<NumberingSchemeDto> UpdateAsync(string recordType, SaveNumberingSchemeRequest req, CancellationToken ct = default)
    {
        if (!Enum.TryParse<RecordType>(recordType, ignoreCase: true, out var type))
            throw new FormValidationException($"Unknown record type '{recordType}'.");
        var prefix = req.Prefix.Trim().ToUpperInvariant();
        if (!PrefixShape().IsMatch(prefix))
            throw new FormValidationException("Prefix must be 1–12 characters of A–Z, 0–9 or dash, starting alphanumeric.");
        if (req.Digits is < 3 or > 6)
            throw new FormValidationException("Digits must be between 3 and 6.");

        var scheme = await db.NumberingSchemes.SingleAsync(s => s.RecordType == type, ct);
        scheme.Prefix = prefix;
        scheme.YearSegment = req.YearSegment;
        scheme.Digits = req.Digits;
        scheme.UpdatedUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(scheme, ct);
    }

    private async Task<NumberingSchemeDto> ToDtoAsync(Domain.Forms.NumberingScheme s, CancellationToken ct)
    {
        var year = clock.UtcNow.Year;
        var bucket = s.YearSegment ? year : 0;
        var last = await db.NumberSequences.AsNoTracking()
            .Where(q => q.Prefix == s.Prefix && q.Year == bucket)
            .Select(q => (int?)q.LastValue).FirstOrDefaultAsync(ct) ?? 0;
        var padded = (last + 1).ToString($"D{s.Digits}");
        var preview = s.YearSegment ? $"{s.Prefix}-{year}-{padded}" : $"{s.Prefix}-{padded}";
        return new NumberingSchemeDto(s.RecordType.ToString(), s.Prefix, s.YearSegment, s.Digits, preview);
    }
}
