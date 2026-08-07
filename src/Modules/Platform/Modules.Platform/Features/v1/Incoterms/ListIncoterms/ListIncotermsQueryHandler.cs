using FSH.Modules.Platform.Contracts.Dtos;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using FSH.Modules.Platform.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.Incoterms.ListIncoterms;

public sealed class ListIncotermsQueryHandler(PlatformDbContext dbContext)
    : IQueryHandler<ListIncotermsQuery, IReadOnlyList<IncotermDto>>
{
    public async ValueTask<IReadOnlyList<IncotermDto>> Handle(ListIncotermsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var q = dbContext.Incoterms.AsNoTracking().AsQueryable();
        if (query.ActiveOnly)
            q = q.Where(i => i.IsActive);

        return await q.OrderBy(i => i.Code)
            .Select(i => new IncotermDto(i.Id, i.Code, i.Name, i.IsActive, i.CreatedOnUtc))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
