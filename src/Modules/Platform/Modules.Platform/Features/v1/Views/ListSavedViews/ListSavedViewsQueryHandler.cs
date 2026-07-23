using FSH.Framework.Core.Context;
using FSH.Modules.Platform.Contracts.Dtos;
using FSH.Modules.Platform.Contracts.v1.Views;
using FSH.Modules.Platform.Data;
using FSH.Modules.Platform.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.Views.ListSavedViews;

public sealed class ListSavedViewsQueryHandler(PlatformDbContext dbContext, ICurrentUser currentUser)
    : IQueryHandler<ListSavedViewsQuery, IReadOnlyList<SavedViewDto>>
{
    public async ValueTask<IReadOnlyList<SavedViewDto>> Handle(ListSavedViewsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        string userId = currentUser.GetUserId().ToString();

        var q = dbContext.SavedViews
            .AsNoTracking()
            .Include(v => v.Filters)
            .Include(v => v.Columns)
            .Where(v => v.IsSystem || v.IsShared || v.OwnerUserId == userId);

        if (!string.IsNullOrWhiteSpace(query.RecordType) && Enum.TryParse<ViewRecordType>(query.RecordType, ignoreCase: true, out var recordType))
            q = q.Where(v => v.RecordType == recordType);

        var views = await q.OrderBy(v => v.IsSystem ? 0 : 1).ThenBy(v => v.Name).ToListAsync(cancellationToken).ConfigureAwait(false);
        return [.. views.Select(PlatformDtoMapper.ToDto)];
    }
}
