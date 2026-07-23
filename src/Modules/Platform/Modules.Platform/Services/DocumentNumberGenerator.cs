using FSH.Framework.Core.Exceptions;
using FSH.Modules.Platform.Data;
using FSH.Modules.Platform.Domain;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Services;

/// <summary>Mints/peeks sequential document numbers from <see cref="NumberingScheme"/> + <see cref="NumberSequence"/>.</summary>
public interface IDocumentNumberGenerator
{
    /// <summary>Returns what the next number would look like, without consuming it.</summary>
    Task<string> PeekAsync(string recordType, CancellationToken cancellationToken = default);

    /// <summary>Atomically consumes the next number for the record type.</summary>
    Task<string> MintAsync(string recordType, CancellationToken cancellationToken = default);
}

public sealed class DocumentNumberGenerator(PlatformDbContext dbContext) : IDocumentNumberGenerator
{
    public async Task<string> PeekAsync(string recordType, CancellationToken cancellationToken = default)
    {
        var scheme = await GetSchemeAsync(recordType, cancellationToken).ConfigureAwait(false);
        var year = await ResolveYearAsync(cancellationToken).ConfigureAwait(false);
        var bucketYear = scheme.YearSegment ? year : 0;

        var sequence = await dbContext.NumberSequences.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Prefix == scheme.Prefix && s.Year == bucketYear, cancellationToken)
            .ConfigureAwait(false);

        var next = (sequence?.LastValue ?? 0) + 1;
        return scheme.FormatPreview(next, year);
    }

    public async Task<string> MintAsync(string recordType, CancellationToken cancellationToken = default)
    {
        var scheme = await GetSchemeAsync(recordType, cancellationToken).ConfigureAwait(false);
        var year = await ResolveYearAsync(cancellationToken).ConfigureAwait(false);
        var bucketYear = scheme.YearSegment ? year : 0;

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        var sequence = await dbContext.NumberSequences
            .FirstOrDefaultAsync(s => s.Prefix == scheme.Prefix && s.Year == bucketYear, cancellationToken)
            .ConfigureAwait(false);

        if (sequence is null)
        {
            sequence = NumberSequence.Create(scheme.Prefix, bucketYear);
            dbContext.NumberSequences.Add(sequence);
        }

        var next = sequence.Next();
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return scheme.FormatPreview(next, year);
    }

    private async Task<NumberingScheme> GetSchemeAsync(string recordType, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recordType);
        var type = recordType.Trim().ToUpperInvariant();
        return await dbContext.NumberingSchemes
            .FirstOrDefaultAsync(s => s.RecordType == type, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Numbering scheme '{type}' not found.");
    }

    private async Task<int> ResolveYearAsync(CancellationToken cancellationToken)
    {
        var now = TimeProvider.System.GetUtcNow();

        var timeZoneId = await dbContext.Settings.AsNoTracking()
            .Where(s => s.Key == SettingKeys.TimeZone)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(timeZoneId))
            return now.UtcDateTime.Year;

        try
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return TimeZoneInfo.ConvertTime(now, timeZone).Year;
        }
        catch (TimeZoneNotFoundException)
        {
            return now.UtcDateTime.Year;
        }
        catch (InvalidTimeZoneException)
        {
            return now.UtcDateTime.Year;
        }
    }
}
