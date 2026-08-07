using FSH.Modules.Platform.Contracts.Dtos;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using FSH.Modules.Platform.Data;
using FSH.Modules.Platform.Services;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.Numbering.ListNumberingSchemes;

public sealed class ListNumberingSchemesQueryHandler(PlatformDbContext dbContext, IDocumentNumberGenerator generator)
    : IQueryHandler<ListNumberingSchemesQuery, IReadOnlyList<NumberingSchemeDto>>
{
    public async ValueTask<IReadOnlyList<NumberingSchemeDto>> Handle(
        ListNumberingSchemesQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var schemes = await dbContext.NumberingSchemes.AsNoTracking()
            .OrderBy(s => s.RecordType)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var result = new List<NumberingSchemeDto>(schemes.Count);
        foreach (var scheme in schemes)
        {
            var preview = await generator.PeekAsync(scheme.RecordType, cancellationToken).ConfigureAwait(false);
            result.Add(new NumberingSchemeDto(
                scheme.Id, scheme.RecordType, scheme.Prefix, scheme.YearSegment, scheme.Digits, preview, scheme.CreatedOnUtc));
        }

        return result;
    }
}
