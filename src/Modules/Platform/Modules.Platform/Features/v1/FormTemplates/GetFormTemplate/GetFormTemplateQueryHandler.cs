using FSH.Modules.Platform.Contracts.Dtos;
using FSH.Modules.Platform.Contracts.v1.FormTemplates;
using FSH.Modules.Platform.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.FormTemplates.GetFormTemplate;

public sealed class GetFormTemplateQueryHandler(PlatformDbContext dbContext)
    : IQueryHandler<GetFormTemplateQuery, FormTemplateDto?>
{
    public async ValueTask<FormTemplateDto?> Handle(GetFormTemplateQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var template = await dbContext.FormTemplates
            .AsNoTracking()
            .Include(t => t.Questions)
            .FirstOrDefaultAsync(t => t.Id == query.Id, cancellationToken)
            .ConfigureAwait(false);
        return template is null ? null : PlatformDtoMapper.ToDto(template);
    }
}

public sealed class ListFormTemplatesByIdsQueryHandler(PlatformDbContext dbContext)
    : IQueryHandler<ListFormTemplatesByIdsQuery, IReadOnlyList<FormTemplateDto>>
{
    public async ValueTask<IReadOnlyList<FormTemplateDto>> Handle(
        ListFormTemplatesByIdsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.Ids.Count == 0)
            return [];

        var templates = await dbContext.FormTemplates
            .AsNoTracking()
            .Include(t => t.Questions)
            .Where(t => query.Ids.Contains(t.Id) && t.IsActive)
            .OrderBy(t => t.Key)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. templates.Select(PlatformDtoMapper.ToDto)];
    }
}
