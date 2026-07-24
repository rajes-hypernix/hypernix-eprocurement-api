using FSH.Framework.Core.Exceptions;
using FSH.Modules.Platform.Contracts.Dtos;
using FSH.Modules.Platform.Contracts.v1.EntryForms;
using FSH.Modules.Platform.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.EntryForms.GetEntryForm;

public sealed class GetEntryFormQueryHandler(PlatformDbContext dbContext)
    : IQueryHandler<GetEntryFormQuery, EntryFormDetailDto>
{
    public async ValueTask<EntryFormDetailDto> Handle(GetEntryFormQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var form = await dbContext.EntryFormDefs
            .AsNoTracking()
            .Include(f => f.Groups)
            .Include(f => f.Fields)
            .FirstOrDefaultAsync(f => f.Id == query.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Entry form {query.Id} not found.");

        return new EntryFormDetailDto(
            form.Id, form.Code, form.Name, form.RecordType.ToString(), form.IsSystem, form.IsActive,
            [.. form.Groups.OrderBy(g => g.Sort).Select(g => new EntryFormGroupDto(g.Id, g.Title, g.Sort))],
            [.. form.Fields.OrderBy(f => f.Sort).Select(f => new EntryFormFieldDto(f.Id, f.FieldKey, f.GroupId, f.Sort, f.RequiredOnForm, f.FullWidth))]);
    }
}
