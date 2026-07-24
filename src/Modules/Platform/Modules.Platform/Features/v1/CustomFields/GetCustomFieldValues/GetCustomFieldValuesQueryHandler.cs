using FSH.Modules.Platform.Contracts.Dtos;
using FSH.Modules.Platform.Contracts.v1.CustomFields;
using FSH.Modules.Platform.Data;
using FSH.Modules.Platform.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.CustomFields.GetCustomFieldValues;

/// <summary>
/// Returns every custom field applicable to this record type joined with whatever header-level
/// value exists for this record instance (missing rows come back with a null value, not omitted —
/// so the caller always sees the full set of fields it should render).
/// </summary>
public sealed class GetCustomFieldValuesQueryHandler(PlatformDbContext dbContext)
    : IQueryHandler<GetCustomFieldValuesQuery, IReadOnlyList<CustomFieldValueDto>>
{
    public async ValueTask<IReadOnlyList<CustomFieldValueDto>> Handle(GetCustomFieldValuesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var recordType = CustomFieldMapping.ParseEnum<PlatformRecordType>(query.RecordType, "record type");

        var defs = await dbContext.CustomFieldDefs
            .AsNoTracking()
            .Include(d => d.Applications)
            .Where(d => d.IsActive && d.Applications.Any(a => a.RecordType == recordType))
            .OrderBy(d => d.Label)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var values = await dbContext.CustomFieldValues
            .AsNoTracking()
            .Where(v => v.RecordType == recordType && v.RecordId == query.RecordId && v.LineId == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. defs.Select(def => CustomFieldMapping.ToValueDto(def, values.FirstOrDefault(v => v.CustomFieldDefId == def.Id)))];
    }
}
