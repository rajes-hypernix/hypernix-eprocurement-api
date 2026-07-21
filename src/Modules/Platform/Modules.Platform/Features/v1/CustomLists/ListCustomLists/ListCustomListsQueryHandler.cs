using FSH.Modules.Platform.Contracts.Dtos;
using FSH.Modules.Platform.Contracts.v1.CustomLists;
using FSH.Modules.Platform.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.CustomLists.ListCustomLists;

public sealed class ListCustomListsQueryHandler(PlatformDbContext dbContext)
    : IQueryHandler<ListCustomListsQuery, IReadOnlyList<CustomListDto>>
{
    public async ValueTask<IReadOnlyList<CustomListDto>> Handle(ListCustomListsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var q = dbContext.CustomLists.AsNoTracking().AsQueryable();
        if (query.ActiveOnly)
            q = q.Where(l => l.IsActive);

        return await q.OrderBy(l => l.Key)
            .Select(l => new CustomListDto(l.Id, l.Key, l.Name, l.IsActive))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
