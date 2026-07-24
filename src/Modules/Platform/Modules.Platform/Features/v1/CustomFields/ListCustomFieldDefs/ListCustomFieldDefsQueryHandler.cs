using FSH.Modules.Platform.Contracts.Dtos;
using FSH.Modules.Platform.Contracts.v1.CustomFields;
using FSH.Modules.Platform.Data;
using FSH.Modules.Platform.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.CustomFields.ListCustomFieldDefs;

public sealed class ListCustomFieldDefsQueryHandler(PlatformDbContext dbContext)
    : IQueryHandler<ListCustomFieldDefsQuery, IReadOnlyList<CustomFieldDefDto>>
{
    public async ValueTask<IReadOnlyList<CustomFieldDefDto>> Handle(ListCustomFieldDefsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var defs = await dbContext.CustomFieldDefs
            .AsNoTracking()
            .Include(d => d.Applications)
            .OrderBy(d => d.Label)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(query.RecordType))
        {
            var recordType = CustomFieldMapping.ParseEnum<PlatformRecordType>(query.RecordType, "record type");
            defs = [.. defs.Where(d => d.Applications.Any(a => a.RecordType == recordType))];
        }

        return [.. defs.Select(CustomFieldMapping.ToDto)];
    }
}
