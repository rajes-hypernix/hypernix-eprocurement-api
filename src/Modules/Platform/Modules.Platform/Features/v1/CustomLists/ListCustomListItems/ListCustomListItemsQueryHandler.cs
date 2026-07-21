using FSH.Framework.Core.Exceptions;
using FSH.Modules.Platform.Contracts.Dtos;
using FSH.Modules.Platform.Contracts.v1.CustomLists;
using FSH.Modules.Platform.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.CustomLists.ListCustomListItems;

public sealed class ListCustomListItemsQueryHandler(PlatformDbContext dbContext)
    : IQueryHandler<ListCustomListItemsQuery, IReadOnlyList<CustomListItemDto>>
{
    public async ValueTask<IReadOnlyList<CustomListItemDto>> Handle(
        ListCustomListItemsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var key = query.ListKey.Trim();
        var list = await dbContext.CustomLists
            .AsNoTracking()
            .Include(l => l.Items)
            .FirstOrDefaultAsync(l => l.Key == key, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Custom list '{key}' not found.");

        var items = list.Items.AsEnumerable();
        if (query.ActiveOnly)
            items = items.Where(i => i.IsActive);

        return [.. items
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.Code)
            .Select(i => new CustomListItemDto(i.Id, i.ListId, i.Code, i.Label, i.SortOrder, i.IsActive))];
    }
}
