using FSH.Modules.Platform.Contracts.Dtos;
using FSH.Modules.Platform.Contracts.v1.EntryForms;
using FSH.Modules.Platform.Data;
using FSH.Modules.Platform.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.EntryForms.ListEntryFormRoleMaps;

public sealed class ListEntryFormRoleMapsQueryHandler(PlatformDbContext dbContext)
    : IQueryHandler<ListEntryFormRoleMapsQuery, IReadOnlyList<EntryFormRoleMapDto>>
{
    public async ValueTask<IReadOnlyList<EntryFormRoleMapDto>> Handle(ListEntryFormRoleMapsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var maps = dbContext.EntryFormRoleMaps.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.RecordType))
        {
            var recordType = CustomFieldMapping.ParseEnum<PlatformRecordType>(query.RecordType, "record type");
            maps = maps.Where(m => m.RecordType == recordType);
        }

        var list = await maps.ToListAsync(cancellationToken).ConfigureAwait(false);
        if (list.Count == 0)
        {
            return [];
        }

        var formIds = list.Select(m => m.EntryFormDefId).Distinct().ToList();
        var forms = await dbContext.EntryFormDefs
            .AsNoTracking()
            .Where(f => formIds.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, cancellationToken)
            .ConfigureAwait(false);

        return [.. list.Select(m => new EntryFormRoleMapDto(
            m.Id, m.RecordType.ToString(), m.Role, m.EntryFormDefId,
            forms.TryGetValue(m.EntryFormDefId, out var form) ? form.Code : string.Empty))];
    }
}
