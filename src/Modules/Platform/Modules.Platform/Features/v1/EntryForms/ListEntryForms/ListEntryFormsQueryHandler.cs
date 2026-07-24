using FSH.Modules.Platform.Contracts.Dtos;
using FSH.Modules.Platform.Contracts.v1.EntryForms;
using FSH.Modules.Platform.Data;
using FSH.Modules.Platform.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.EntryForms.ListEntryForms;

public sealed class ListEntryFormsQueryHandler(PlatformDbContext dbContext)
    : IQueryHandler<ListEntryFormsQuery, IReadOnlyList<EntryFormListItemDto>>
{
    public async ValueTask<IReadOnlyList<EntryFormListItemDto>> Handle(ListEntryFormsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var forms = dbContext.EntryFormDefs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.RecordType))
        {
            var recordType = CustomFieldMapping.ParseEnum<PlatformRecordType>(query.RecordType, "record type");
            forms = forms.Where(f => f.RecordType == recordType);
        }

        var list = await forms.OrderBy(f => f.Name).ToListAsync(cancellationToken).ConfigureAwait(false);
        return [.. list.Select(f => new EntryFormListItemDto(f.Id, f.Code, f.Name, f.RecordType.ToString(), f.IsSystem, f.IsActive))];
    }
}
