using FSH.Modules.Platform.Contracts.Dtos;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using FSH.Modules.Platform.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.Items.ListItems;

public sealed class ListItemsQueryHandler(PlatformDbContext dbContext)
    : IQueryHandler<ListItemsQuery, IReadOnlyList<ItemDto>>
{
    public async ValueTask<IReadOnlyList<ItemDto>> Handle(ListItemsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var q = dbContext.Items.AsNoTracking().AsQueryable();
        if (query.ActiveOnly)
            q = q.Where(i => i.IsActive);

        return await q.OrderBy(i => i.ItemCode)
            .Select(i => new ItemDto(i.Id, i.ItemCode, i.Description, i.Uom, i.IsActive))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
