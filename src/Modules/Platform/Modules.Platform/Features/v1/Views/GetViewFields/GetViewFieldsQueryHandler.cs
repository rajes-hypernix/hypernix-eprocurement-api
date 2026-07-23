using FSH.Modules.Platform.Contracts.Dtos;
using FSH.Modules.Platform.Contracts.v1.Views;
using FSH.Modules.Platform.Data;
using FSH.Modules.Platform.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.Views.GetViewFields;

public sealed class GetViewFieldsQueryHandler(PlatformDbContext dbContext)
    : IQueryHandler<GetViewFieldsQuery, IReadOnlyList<ViewFieldDto>>
{
    public async ValueTask<IReadOnlyList<ViewFieldDto>> Handle(GetViewFieldsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!Enum.TryParse<ViewRecordType>(query.RecordType, ignoreCase: true, out var recordType))
            throw new PlatformRuleException($"'{query.RecordType}' is not a recognised saved-view record type.");

        var entries = await dbContext.FieldRegistryEntries
            .AsNoTracking()
            .Where(f => f.RecordType == recordType)
            .OrderBy(f => f.FieldKey)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. entries.Select(PlatformDtoMapper.ToDto)];
    }
}
