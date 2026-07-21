using FSH.Modules.Platform.Contracts.Dtos;
using FSH.Modules.Platform.Contracts.v1.FormTemplates;
using FSH.Modules.Platform.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.FormTemplates.ListFormTemplates;

public sealed class ListFormTemplatesQueryHandler(PlatformDbContext dbContext)
    : IQueryHandler<ListFormTemplatesQuery, IReadOnlyList<FormTemplateListItemDto>>
{
    public async ValueTask<IReadOnlyList<FormTemplateListItemDto>> Handle(
        ListFormTemplatesQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var q = dbContext.FormTemplates.AsNoTracking().Include(t => t.Questions).AsQueryable();
        if (query.ActiveOnly)
            q = q.Where(t => t.IsActive);

        var templates = await q.OrderBy(t => t.Key).ToListAsync(cancellationToken).ConfigureAwait(false);
        return [.. templates.Select(PlatformDtoMapper.ToListItem)];
    }
}
